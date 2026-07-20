import { useEffect, useState } from 'react';
import { loadAttachmentObjectUrl } from '@/api/attachment';
import { normalizeEntityId, type EntityIdValue } from '@/types/chat';
import { log } from '@/utils/logger';

interface ChatProtectedImageProps {
  attachmentId: EntityIdValue | null | undefined;
  fallbackUrl: string | null;
  variant?: 'original' | 'thumbnail';
  className: string;
  alt: string;
  loading?: 'eager' | 'lazy';
}

export const ChatProtectedImage = ({
  attachmentId,
  fallbackUrl,
  variant = 'thumbnail',
  className,
  alt,
  loading = 'lazy',
}: ChatProtectedImageProps) => {
  const attachmentIdKey = normalizeEntityId(attachmentId);
  const immediateFallback = fallbackUrl?.startsWith('blob:') || fallbackUrl?.startsWith('data:')
    ? fallbackUrl
    : null;
  const [sourceUrl, setSourceUrl] = useState<string | null>(immediateFallback);

  useEffect(() => {
    if (!attachmentIdKey || attachmentIdKey === '0' || attachmentIdKey.startsWith('-')) {
      setSourceUrl(fallbackUrl);
      return;
    }

    const controller = new AbortController();
    let objectUrl: string | null = null;
    let disposed = false;
    setSourceUrl(immediateFallback);

    void loadAttachmentObjectUrl(attachmentIdKey, variant, controller.signal)
      .then((url) => {
        if (disposed) {
          URL.revokeObjectURL(url);
          return;
        }

        objectUrl = url;
        setSourceUrl(url);
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          log.warn('ChatProtectedImage', '聊天室附件加载失败', error);
          setSourceUrl(immediateFallback);
        }
      });

    return () => {
      disposed = true;
      controller.abort();
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [attachmentIdKey, fallbackUrl, immediateFallback, variant]);

  return sourceUrl
    ? <img className={className} src={sourceUrl} alt={alt} loading={loading} />
    : null;
};
