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
const LOCAL_HOSTNAMES = new Set(['localhost', '127.0.0.1', '[::1]']);

function getCurrentOrigin(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.location.origin;
}

function isLocalHostname(hostname: string): boolean {
  return LOCAL_HOSTNAMES.has(hostname.toLowerCase());
}

function normalizeBrowserVisibleAssetUrl(rawUrl: string, currentOrigin: string | null = getCurrentOrigin()): string {
  if (!rawUrl || !currentOrigin || !/^https?:\/\//i.test(rawUrl)) {
    return rawUrl;
  }

  let targetUrl: URL;
  let originUrl: URL;
  try {
    targetUrl = new URL(rawUrl);
    originUrl = new URL(currentOrigin);
  } catch {
    return rawUrl;
  }

  if (
    originUrl.protocol !== 'https:'
    || targetUrl.protocol !== 'http:'
    || !isLocalHostname(originUrl.hostname)
    || !isLocalHostname(targetUrl.hostname)
  ) {
    return rawUrl;
  }

  return `${originUrl.origin}${targetUrl.pathname}${targetUrl.search}${targetUrl.hash}`;
}

export function resolveConfiguredMediaUrl(url: string): string {
  const normalizedUrl = url.trim();
  if (
    !normalizedUrl
    || normalizedUrl.startsWith('//')
    || normalizedUrl.startsWith('#')
    || normalizedUrl.startsWith('?')
  ) {
    return normalizedUrl;
  }

  if (URI_SCHEME_PATTERN.test(normalizedUrl)) {
    return normalizeBrowserVisibleAssetUrl(normalizedUrl);
  }

  const normalizedBaseUrl = (getApiClientConfig().baseUrl || '').trim().replace(/\/$/, '');
  if (!normalizedBaseUrl) {
    return normalizedUrl;
  }

  const resolvedUrl = normalizedUrl.startsWith('/')
    ? `${normalizedBaseUrl}${normalizedUrl}`
    : `${normalizedBaseUrl}/${normalizedUrl}`;

  return normalizeBrowserVisibleAssetUrl(resolvedUrl);
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
