import { getApiBaseUrl } from '@/config/env';

export function resolveMediaUrl(url: string | null | undefined, apiBaseUrl: string = getApiBaseUrl()): string | null {
  const normalizedUrl = url?.trim();
  if (!normalizedUrl) {
    return null;
  }

  if (/^https?:\/\//i.test(normalizedUrl)) {
    return normalizedUrl;
  }

  if (normalizedUrl.startsWith('/')) {
    return `${apiBaseUrl}${normalizedUrl}`;
  }

  return `${apiBaseUrl}/${normalizedUrl}`;
}
