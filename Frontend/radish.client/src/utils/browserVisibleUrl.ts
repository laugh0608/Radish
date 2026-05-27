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

export function normalizeBrowserVisibleUrl(rawUrl: string, currentOrigin: string | null = getCurrentOrigin()): string {
  const normalizedUrl = rawUrl.trim();
  if (!normalizedUrl || !currentOrigin || !/^https?:\/\//i.test(normalizedUrl)) {
    return normalizedUrl;
  }

  let targetUrl: URL;
  let originUrl: URL;
  try {
    targetUrl = new URL(normalizedUrl);
    originUrl = new URL(currentOrigin);
  } catch {
    return normalizedUrl;
  }

  if (
    originUrl.protocol !== 'https:'
    || targetUrl.protocol !== 'http:'
    || !isLocalHostname(originUrl.hostname)
    || !isLocalHostname(targetUrl.hostname)
  ) {
    return normalizedUrl;
  }

  return `${originUrl.origin}${targetUrl.pathname}${targetUrl.search}${targetUrl.hash}`;
}
