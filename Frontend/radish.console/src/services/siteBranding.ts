const DEFAULT_FAVICON_PATH = '/uploads/DefaultIco/bailuobo.ico';
const PUBLIC_SITE_SETTINGS_ENDPOINT = '/api/v1/SystemConfig/GetPublicSiteSettings';

interface PublicSiteSettingsVo {
  voSiteFaviconUrl?: string;
}

interface ApiResponse<T> {
  isSuccess?: boolean;
  responseData?: T;
}

function resolveAssetUrl(apiBaseUrl: string, rawUrl: string): string {
  const normalizedUrl = rawUrl.trim();
  if (!normalizedUrl) {
    return `${apiBaseUrl}${DEFAULT_FAVICON_PATH}`;
  }

  if (/^https?:\/\//i.test(normalizedUrl)) {
    return normalizedUrl;
  }

  return normalizedUrl.startsWith('/')
    ? `${apiBaseUrl}${normalizedUrl}`
    : `${apiBaseUrl}/${normalizedUrl}`;
}

function upsertFaviconLink(rel: string, href: string) {
  let link = document.head.querySelector(`link[rel="${rel}"]`) as HTMLLinkElement | null;
  if (!link) {
    link = document.createElement('link');
    link.rel = rel;
    document.head.appendChild(link);
  }

  link.type = href.toLowerCase().endsWith('.ico') ? 'image/x-icon' : 'image/png';
  link.href = href;
}

function applyFavicon(href: string) {
  upsertFaviconLink('icon', href);
  upsertFaviconLink('shortcut icon', href);
}

export async function applySiteBranding(apiBaseUrl: string): Promise<void> {
  const normalizedApiBaseUrl = apiBaseUrl.replace(/\/$/, '');
  const fallbackFaviconUrl = resolveAssetUrl(normalizedApiBaseUrl, DEFAULT_FAVICON_PATH);
  applyFavicon(fallbackFaviconUrl);

  try {
    const response = await fetch(`${normalizedApiBaseUrl}${PUBLIC_SITE_SETTINGS_ENDPOINT}`, {
      headers: {
        Accept: 'application/json'
      }
    });

    if (!response.ok) {
      return;
    }

    const payload = (await response.json()) as ApiResponse<PublicSiteSettingsVo>;
    if (!payload.isSuccess) {
      return;
    }

    const configuredFaviconUrl = payload.responseData?.voSiteFaviconUrl?.trim() || DEFAULT_FAVICON_PATH;
    applyFavicon(resolveAssetUrl(normalizedApiBaseUrl, configuredFaviconUrl));
  } catch {
    // 静默回退到默认 favicon，避免影响主流程。
  }
}
