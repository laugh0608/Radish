import { getApiClientConfig } from '@radish/http';

export type AttachmentMarkdownVariant = 'original' | 'thumbnail';

export interface MarkdownImageUploadResult {
  attachmentId: string | number;
  displayVariant?: AttachmentMarkdownVariant;
  previewUrl?: string;
  scalePercent?: number;
}

export interface MarkdownDocumentUploadResult {
  attachmentId: string | number;
  fileName: string;
}

export interface ParsedAttachmentMarkdownUrl {
  attachmentId: string;
  displayVariant: AttachmentMarkdownVariant;
  scalePercent?: number;
}

const ATTACHMENT_PROTOCOL = 'attachment://';
const RADISH_META_PREFIX = 'radish:';
const URI_SCHEME_PATTERN = /^[a-z][a-z\d+.-]*:/i;

export function resolveConfiguredMediaUrl(url: string): string {
  const normalizedUrl = url.trim();
  if (
    !normalizedUrl
    || normalizedUrl.startsWith('//')
    || normalizedUrl.startsWith('#')
    || normalizedUrl.startsWith('?')
    || URI_SCHEME_PATTERN.test(normalizedUrl)
  ) {
    return normalizedUrl;
  }

  const normalizedBaseUrl = (getApiClientConfig().baseUrl || '').trim().replace(/\/$/, '');
  if (!normalizedBaseUrl) {
    return normalizedUrl;
  }

  return normalizedUrl.startsWith('/')
    ? `${normalizedBaseUrl}${normalizedUrl}`
    : `${normalizedBaseUrl}/${normalizedUrl}`;
}

export function normalizeAttachmentId(value: string | number | null | undefined): string | null {
  if (typeof value === 'number') {
    if (!Number.isFinite(value) || value <= 0) {
      return null;
    }

    return String(Math.trunc(value));
  }

  if (typeof value === 'string') {
    const normalized = value.trim();
    return /^[1-9]\d*$/.test(normalized) ? normalized : null;
  }

  return null;
}

export function buildAttachmentAssetUrl(
  attachmentId: string | number,
  variant: AttachmentMarkdownVariant = 'original'
): string {
  const normalizedAttachmentId = normalizeAttachmentId(attachmentId);
  if (!normalizedAttachmentId) {
    throw new Error('attachmentId 无效');
  }

  const assetPath = variant === 'thumbnail'
    ? `/_assets/attachments/${normalizedAttachmentId}/thumbnail`
    : `/_assets/attachments/${normalizedAttachmentId}`;

  return resolveConfiguredMediaUrl(assetPath);
}

export function buildAttachmentMarkdownUrl(
  attachmentId: string | number,
  options?: {
    displayVariant?: AttachmentMarkdownVariant;
    scalePercent?: number;
  }
): string {
  const normalizedAttachmentId = normalizeAttachmentId(attachmentId);
  if (!normalizedAttachmentId) {
    throw new Error('attachmentId 无效');
  }

  const params = new URLSearchParams();
  if (options?.displayVariant === 'thumbnail') {
    params.set('display', 'thumbnail');
  }

  if (options?.scalePercent && Number.isFinite(options.scalePercent)) {
    const scalePercent = Math.min(Math.max(Math.round(options.scalePercent), 10), 100);
    params.set('scale', String(scalePercent));
  }

  const base = `${ATTACHMENT_PROTOCOL}${normalizedAttachmentId}`;
  const meta = params.toString();
  return meta ? `${base}#${RADISH_META_PREFIX}${meta}` : base;
}

export function parseAttachmentMarkdownUrl(src: string): ParsedAttachmentMarkdownUrl | null {
  const raw = src.trim();
  if (!raw.startsWith(ATTACHMENT_PROTOCOL)) {
    return null;
  }

  const withoutProtocol = raw.slice(ATTACHMENT_PROTOCOL.length);
  const [pathPart, hashPart] = withoutProtocol.split('#');
  const attachmentId = normalizeAttachmentId(decodeURIComponent(pathPart || '').split('/')[0]);
  if (!attachmentId) {
    return null;
  }

  const parsed: ParsedAttachmentMarkdownUrl = {
    attachmentId,
    displayVariant: 'original',
  };

  if (!hashPart || !hashPart.startsWith(RADISH_META_PREFIX)) {
    return parsed;
  }

  const params = new URLSearchParams(hashPart.slice(RADISH_META_PREFIX.length));
  if (params.get('display') === 'thumbnail') {
    parsed.displayVariant = 'thumbnail';
  }

  const scaleRaw = params.get('scale');
  const scaleNum = scaleRaw ? Number(scaleRaw) : undefined;
  if (Number.isFinite(scaleNum) && scaleNum! > 0) {
    parsed.scalePercent = Math.min(Math.max(Math.round(scaleNum!), 10), 100);
  }

  return parsed;
}
