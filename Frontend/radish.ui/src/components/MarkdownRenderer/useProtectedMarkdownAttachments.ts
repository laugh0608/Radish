import { useEffect, useMemo, useState } from 'react';
import {
  parseAttachmentMarkdownUrl,
  type AttachmentMarkdownVariant,
} from '../../utils/attachmentMarkdown';

export interface ProtectedMarkdownAttachmentLabels {
  loading: string;
  loadFailed: string;
  retry: string;
  download: string;
  openImage: string;
  lightboxClose: string;
  lightboxPrevious: string;
  lightboxNext: string;
}

export type ProtectedMarkdownAttachmentBlobLoader = (
  attachmentId: string,
  variant: AttachmentMarkdownVariant,
  signal: AbortSignal,
) => Promise<Blob>;

export interface ProtectedMarkdownAttachmentOptions {
  loadBlob: ProtectedMarkdownAttachmentBlobLoader;
  labels: ProtectedMarkdownAttachmentLabels;
  /** 账号、文档或 Revision 等宿主身份；变化时立即隔离上一代结果。 */
  scopeKey: string;
}

export interface ProtectedMarkdownAttachmentAsset {
  status: 'loading' | 'ready' | 'error';
  url?: string;
}

interface ProtectedMarkdownAttachmentRequest {
  attachmentId: string;
  variant: AttachmentMarkdownVariant;
}

interface ProtectedMarkdownAttachmentSnapshot {
  signature: string;
  assets: ReadonlyMap<string, ProtectedMarkdownAttachmentAsset>;
}

const ATTACHMENT_REFERENCE_PATTERN = /attachment:\/\/[1-9]\d*(?:#radish:[^)\s"'<>]+)?/gi;

export function buildProtectedMarkdownAttachmentKey(
  attachmentId: string,
  variant: AttachmentMarkdownVariant,
): string {
  return `${attachmentId}:${variant}`;
}

export function collectProtectedMarkdownAttachmentRequests(
  content: string,
): ProtectedMarkdownAttachmentRequest[] {
  const requests = new Map<string, ProtectedMarkdownAttachmentRequest>();
  const matches = content.match(ATTACHMENT_REFERENCE_PATTERN) ?? [];

  for (const match of matches) {
    const parsed = parseAttachmentMarkdownUrl(match.toLowerCase().startsWith('attachment://')
      ? `attachment://${match.slice('attachment://'.length)}`
      : match);
    if (!parsed) {
      continue;
    }

    const originalRequest: ProtectedMarkdownAttachmentRequest = {
      attachmentId: parsed.attachmentId,
      variant: 'original',
    };
    requests.set(
      buildProtectedMarkdownAttachmentKey(originalRequest.attachmentId, originalRequest.variant),
      originalRequest,
    );

    if (parsed.displayVariant === 'thumbnail') {
      const thumbnailRequest: ProtectedMarkdownAttachmentRequest = {
        attachmentId: parsed.attachmentId,
        variant: 'thumbnail',
      };
      requests.set(
        buildProtectedMarkdownAttachmentKey(thumbnailRequest.attachmentId, thumbnailRequest.variant),
        thumbnailRequest,
      );
    }
  }

  return [...requests.values()];
}

export function useProtectedMarkdownAttachments(
  content: string,
  options: ProtectedMarkdownAttachmentOptions | undefined,
  reloadToken: number,
): ReadonlyMap<string, ProtectedMarkdownAttachmentAsset> {
  const requests = useMemo(
    () => options ? collectProtectedMarkdownAttachmentRequests(content) : [],
    [content, options],
  );
  const signature = options
    ? `${options.scopeKey}\u0000${reloadToken}\u0000${content}`
    : '';
  const pendingAssets = useMemo(() => new Map(
    requests.map((request) => [
      buildProtectedMarkdownAttachmentKey(request.attachmentId, request.variant),
      { status: 'loading' as const },
    ]),
  ), [requests]);
  const [snapshot, setSnapshot] = useState<ProtectedMarkdownAttachmentSnapshot>({
    signature: '',
    assets: new Map(),
  });
  const loadBlob = options?.loadBlob;

  useEffect(() => {
    if (!loadBlob || !options) {
      setSnapshot({ signature: '', assets: new Map() });
      return;
    }

    const controller = new AbortController();
    const objectUrls = new Map<string, string>();
    const failedAttachmentIds = new Set<string>();
    let disposed = false;

    setSnapshot({
      signature,
      assets: pendingAssets,
    });

    for (const request of requests) {
      const key = buildProtectedMarkdownAttachmentKey(request.attachmentId, request.variant);
      void loadBlob(request.attachmentId, request.variant, controller.signal)
        .then((blob) => {
          if (blob.size === 0) {
            throw new Error('Protected attachment response was empty');
          }

          const objectUrl = URL.createObjectURL(blob);
          if (disposed || failedAttachmentIds.has(request.attachmentId)) {
            URL.revokeObjectURL(objectUrl);
            return;
          }

          objectUrls.set(key, objectUrl);
          setSnapshot((current) => {
            if (current.signature !== signature) {
              URL.revokeObjectURL(objectUrl);
              objectUrls.delete(key);
              return current;
            }

            const assets = new Map(current.assets);
            assets.set(key, { status: 'ready', url: objectUrl });
            return { signature, assets };
          });
        })
        .catch(() => {
          if (disposed || controller.signal.aborted) {
            return;
          }

          failedAttachmentIds.add(request.attachmentId);
          for (const [assetKey, objectUrl] of objectUrls) {
            if (assetKey.startsWith(`${request.attachmentId}:`)) {
              URL.revokeObjectURL(objectUrl);
              objectUrls.delete(assetKey);
            }
          }

          setSnapshot((current) => {
            if (current.signature !== signature) {
              return current;
            }

            const assets = new Map(current.assets);
            for (const assetKey of assets.keys()) {
              if (assetKey.startsWith(`${request.attachmentId}:`)) {
                assets.set(assetKey, { status: 'error' });
              }
            }
            return { signature, assets };
          });
        });
    }

    return () => {
      disposed = true;
      controller.abort();
      for (const objectUrl of objectUrls.values()) {
        URL.revokeObjectURL(objectUrl);
      }
      objectUrls.clear();
    };
  }, [loadBlob, options, pendingAssets, requests, signature]);

  return snapshot.signature === signature ? snapshot.assets : pendingAssets;
}
