const AUTH_RETURN_PATH_STORAGE_KEY = 'radish:auth:return-path';
const AUTH_RETURN_PATH_BASE_URL = 'https://radish.local';

function getSessionStorage(): Storage | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

export function normalizeAuthReturnPath(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed || !trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.includes('\\')) {
    return null;
  }

  try {
    const url = new URL(trimmed, AUTH_RETURN_PATH_BASE_URL);
    const pathname = url.pathname.endsWith('/') && url.pathname !== '/'
      ? url.pathname.slice(0, -1)
      : url.pathname;

    if (pathname !== '/desktop') {
      return null;
    }

    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

export function rememberAuthReturnPath(returnPath: string | null | undefined, storage = getSessionStorage()): boolean {
  const normalized = normalizeAuthReturnPath(returnPath);
  if (!normalized || !storage) {
    return false;
  }

  storage.setItem(AUTH_RETURN_PATH_STORAGE_KEY, normalized);
  return true;
}

export function consumeAuthReturnPath(storage = getSessionStorage(), fallbackPath = '/'): string {
  if (!storage) {
    return fallbackPath;
  }

  const storedPath = storage.getItem(AUTH_RETURN_PATH_STORAGE_KEY);
  storage.removeItem(AUTH_RETURN_PATH_STORAGE_KEY);

  return normalizeAuthReturnPath(storedPath) ?? fallbackPath;
}

export function buildDesktopShopProductReturnPath(productId: string | number): string | null {
  const normalizedProductId = String(productId).trim();
  if (!/^[1-9]\d*$/.test(normalizedProductId)) {
    return null;
  }

  const query = new URLSearchParams({
    app: 'shop',
    productId: normalizedProductId,
  });

  return `/desktop?${query.toString()}`;
}

export function buildDesktopShopOrderReturnPath(orderId: string | number): string | null {
  const normalizedOrderId = String(orderId).trim();
  if (!/^[1-9]\d*$/.test(normalizedOrderId)) {
    return null;
  }

  const query = new URLSearchParams({
    app: 'shop',
    orderId: normalizedOrderId,
  });

  return `/desktop?${query.toString()}`;
}

export function buildDesktopShopPrivateViewReturnPath(view: 'orders' | 'inventory'): string {
  const query = new URLSearchParams({
    app: 'shop',
    view,
  });

  return `/desktop?${query.toString()}`;
}
