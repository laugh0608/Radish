const AUTH_RETURN_PATH_STORAGE_KEY = 'radish:auth:return-path';
const AUTH_RETURN_PATH_BASE_URL = 'https://radish.local';
const CIRCLE_RETURN_TABS = new Set(['feed', 'following', 'followers']);

interface AuthReturnLocation {
  pathname: string;
  search?: string;
  hash?: string;
}

export type CircleReturnTab = 'feed' | 'following' | 'followers';

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

    if (pathname === '/circle') {
      return normalizeCircleReturnPath(url);
    }

    if (pathname !== '/desktop') {
      return null;
    }

    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

function normalizeCircleReturnPath(url: URL): string | null {
  if (url.hash) {
    return null;
  }

  const normalized = new URLSearchParams();
  for (const key of url.searchParams.keys()) {
    if (key !== 'tab' && key !== 'page') {
      return null;
    }
  }

  const tab = url.searchParams.get('tab');
  if (tab) {
    if (!CIRCLE_RETURN_TABS.has(tab)) {
      return null;
    }

    if (tab !== 'feed') {
      normalized.set('tab', tab);
    }
  }

  const page = url.searchParams.get('page');
  if (page) {
    if (!/^[1-9]\d*$/.test(page)) {
      return null;
    }

    if (page !== '1') {
      normalized.set('page', page);
    }
  }

  const query = normalized.toString();
  return query ? `/circle?${query}` : '/circle';
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

export function buildCurrentDesktopReturnPath(
  location: AuthReturnLocation | null | undefined = typeof window === 'undefined' ? null : window.location,
): string | null {
  if (!location) {
    return null;
  }

  return normalizeAuthReturnPath(`${location.pathname}${location.search ?? ''}${location.hash ?? ''}`);
}

export function buildCircleReturnPath(options: { tab?: CircleReturnTab; page?: number | string } = {}): string | null {
  const query = new URLSearchParams();
  if (options.tab && options.tab !== 'feed') {
    if (!CIRCLE_RETURN_TABS.has(options.tab)) {
      return null;
    }

    query.set('tab', options.tab);
  }

  if (options.page != null) {
    const normalizedPage = String(options.page).trim();
    if (!/^[1-9]\d*$/.test(normalizedPage)) {
      return null;
    }

    if (normalizedPage !== '1') {
      query.set('page', normalizedPage);
    }
  }

  const search = query.toString();
  return search ? `/circle?${search}` : '/circle';
}

export function buildDesktopShopProductReturnPath(
  productId: string,
  options: { intent?: 'purchase' | null } = {},
): string | null {
  const normalizedProductId = String(productId).trim();
  if (!/^[1-9]\d*$/.test(normalizedProductId)) {
    return null;
  }

  const query = new URLSearchParams({
    app: 'shop',
    productId: normalizedProductId,
  });
  if (options.intent === 'purchase') {
    query.set('intent', 'purchase');
  }

  return `/desktop?${query.toString()}`;
}

export function buildDesktopShopOrderReturnPath(orderId: string): string | null {
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

export function buildDesktopForumReturnPath(): string {
  const query = new URLSearchParams({
    app: 'forum',
  });

  return `/desktop?${query.toString()}`;
}

export function buildDesktopForumPostReturnPath(target: {
  postId?: string | null;
  postPublicId?: string | null;
  commentId?: string | null;
  intent?: 'comment' | 'quickReply' | null;
}): string | null {
  const normalizedPostPublicId = target.postPublicId?.trim().toLowerCase();
  const normalizedPostId = target.postId == null ? '' : String(target.postId).trim();
  const normalizedCommentId = target.commentId == null ? '' : String(target.commentId).trim();
  const hasPostPublicId = normalizedPostPublicId != null
    && /^pst_[a-f0-9]{32}$/.test(normalizedPostPublicId);
  const hasPostId = /^[1-9]\d*$/.test(normalizedPostId);

  if (!hasPostPublicId && !hasPostId) {
    return null;
  }

  if (normalizedCommentId && !/^[1-9]\d*$/.test(normalizedCommentId)) {
    return null;
  }

  const query = new URLSearchParams({
    app: 'forum',
  });
  if (hasPostPublicId && normalizedPostPublicId) {
    query.set('postPublicId', normalizedPostPublicId);
  } else {
    query.set('postId', normalizedPostId);
  }
  if (normalizedCommentId) {
    query.set('commentId', normalizedCommentId);
  }
  if (target.intent === 'comment' || target.intent === 'quickReply') {
    query.set('intent', target.intent);
  }

  return `/desktop?${query.toString()}`;
}
