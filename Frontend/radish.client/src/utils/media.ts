import { getApiBaseUrl } from '@/config/env';
import { normalizeBrowserVisibleUrl } from './browserVisibleUrl';

export function resolveMediaUrl(url: string | null | undefined, apiBaseUrl: string = getApiBaseUrl()): string | null {
  const normalizedUrl = url?.trim();
  if (!normalizedUrl) {
    return null;
  }

  if (/^https?:\/\//i.test(normalizedUrl)) {
    return normalizeBrowserVisibleUrl(normalizedUrl);
  }

  if (normalizedUrl.startsWith('/')) {
    return normalizeBrowserVisibleUrl(`${apiBaseUrl}${normalizedUrl}`);
  }

  return normalizeBrowserVisibleUrl(`${apiBaseUrl}/${normalizedUrl}`);
}
