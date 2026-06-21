import { buildMessagesPath, MESSAGES_ENTRY_PATH, type MessagesRoute } from '../messages/messagesRouteState.ts';
import { PET_ENTRY_PATH } from '../pet/petRouteState.ts';

const AUTH_RETURN_PATH_STORAGE_KEY = 'radish:auth:return-path';
const AUTH_RETURN_PATH_BASE_URL = 'https://radish.local';
const ME_RETURN_PATH = '/me';
const ME_ASSETS_RETURN_PATH = '/me/assets';
const ME_ASSET_TRANSACTIONS_RETURN_PATH = '/me/assets/transactions';
const NOTIFICATIONS_RETURN_PATH = '/notifications';
const CIRCLE_RETURN_TABS = new Set(['feed', 'following', 'followers']);
const PUBLIC_FORUM_POST_PUBLIC_ID_PATTERN = /^pst_[a-f0-9]{32}$/;
const POSITIVE_LONG_ID_PATTERN = /^[1-9]\d*$/;

interface AuthReturnLocation {
  pathname: string;
  search?: string;
  hash?: string;
}

export type CircleReturnTab = 'feed' | 'following' | 'followers';
export type PublicForumPostReturnIntent = 'comment' | 'quickReply';

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

    if (pathname === NOTIFICATIONS_RETURN_PATH) {
      return normalizeNotificationsReturnPath(url);
    }

    if (pathname === MESSAGES_ENTRY_PATH) {
      return normalizeMessagesReturnPath(url);
    }

    if (pathname === ME_RETURN_PATH) {
      return normalizeMeReturnPath(url);
    }

    if (pathname === ME_ASSETS_RETURN_PATH || pathname === ME_ASSET_TRANSACTIONS_RETURN_PATH) {
      return normalizeMeAssetsReturnPath(url, pathname);
    }

    if (pathname === PET_ENTRY_PATH) {
      return normalizePetReturnPath(url);
    }

    if (pathname.startsWith('/forum/post/')) {
      return normalizePublicForumPostReturnPath(url, pathname);
    }

    if (pathname.startsWith('/shop/')) {
      return normalizeShopReturnPath(url, pathname);
    }

    if (pathname !== '/desktop') {
      return null;
    }

    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

function normalizeForumPostIdentifier(value: string | null | undefined): string | null {
  const normalized = value?.trim();
  if (!normalized) {
    return null;
  }

  const lowered = normalized.toLowerCase();
  if (PUBLIC_FORUM_POST_PUBLIC_ID_PATTERN.test(lowered)) {
    return lowered;
  }

  return POSITIVE_LONG_ID_PATTERN.test(normalized) ? normalized : null;
}

function normalizePublicForumPostReturnPath(url: URL, normalizedPathname: string): string | null {
  if (url.hash) {
    return null;
  }

  const matched = normalizedPathname.match(/^\/forum\/post\/([^/]+)$/);
  if (!matched) {
    return null;
  }

  let rawPostIdentifier = '';
  try {
    rawPostIdentifier = decodeURIComponent(matched[1]);
  } catch {
    return null;
  }

  const postIdentifier = normalizeForumPostIdentifier(rawPostIdentifier);
  if (!postIdentifier) {
    return null;
  }

  for (const key of url.searchParams.keys()) {
    if (key !== 'commentId' && key !== 'intent') {
      return null;
    }
  }

  if (url.searchParams.getAll('commentId').length > 1 || url.searchParams.getAll('intent').length !== 1) {
    return null;
  }

  const commentId = url.searchParams.get('commentId');
  if (commentId && !POSITIVE_LONG_ID_PATTERN.test(commentId)) {
    return null;
  }

  const intent = url.searchParams.get('intent');
  if (intent !== 'comment' && intent !== 'quickReply') {
    return null;
  }

  const query = new URLSearchParams();
  if (commentId) {
    query.set('commentId', commentId);
  }
  query.set('intent', intent);

  return `/forum/post/${encodeURIComponent(postIdentifier)}?${query.toString()}`;
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

  if (url.searchParams.getAll('tab').length > 1 || url.searchParams.getAll('page').length > 1) {
    return null;
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

function normalizeNotificationsReturnPath(url: URL): string | null {
  if (url.search || url.hash) {
    return null;
  }

  return NOTIFICATIONS_RETURN_PATH;
}

function normalizeMessagesReturnPath(url: URL): string | null {
  if (url.hash) {
    return null;
  }

  for (const key of url.searchParams.keys()) {
    if (key !== 'channelId' && key !== 'messageId') {
      return null;
    }
  }

  if (url.searchParams.getAll('channelId').length > 1 || url.searchParams.getAll('messageId').length > 1) {
    return null;
  }

  const hasChannelId = url.searchParams.has('channelId');
  const hasMessageId = url.searchParams.has('messageId');
  if (!hasChannelId && !hasMessageId) {
    return MESSAGES_ENTRY_PATH;
  }

  if (!hasChannelId) {
    return null;
  }

  const normalized = buildMessagesReturnPath({
    channelId: url.searchParams.get('channelId') ?? undefined,
    messageId: url.searchParams.get('messageId') ?? undefined,
  });
  if (!normalized) {
    return null;
  }

  if (hasMessageId && !normalized.includes('messageId=')) {
    return null;
  }

  return normalized;
}

function normalizeMeReturnPath(url: URL): string | null {
  if (url.search || url.hash) {
    return null;
  }

  return ME_RETURN_PATH;
}

function normalizeMeAssetsReturnPath(url: URL, pathname: string): string | null {
  if (url.search || url.hash) {
    return null;
  }

  return pathname;
}

function normalizePetReturnPath(url: URL): string | null {
  if (url.search || url.hash) {
    return null;
  }

  return PET_ENTRY_PATH;
}

function normalizeShopReturnPath(url: URL, normalizedPathname: string): string | null {
  if (url.hash) {
    return null;
  }

  const productMatched = normalizedPathname.match(/^\/shop\/product\/([1-9]\d*)$/);
  if (productMatched) {
    for (const key of url.searchParams.keys()) {
      if (key !== 'intent') {
        return null;
      }
    }

    if (url.searchParams.getAll('intent').length !== 1 || url.searchParams.get('intent') !== 'purchase') {
      return null;
    }

    return `/shop/product/${productMatched[1]}?intent=purchase`;
  }

  if (normalizedPathname === '/shop/orders') {
    return url.search ? null : '/shop/orders';
  }

  if (normalizedPathname === '/shop/inventory') {
    return url.search ? null : '/shop/inventory';
  }

  const orderMatched = normalizedPathname.match(/^\/shop\/order\/([1-9]\d*)$/);
  if (orderMatched) {
    return url.search ? null : `/shop/order/${orderMatched[1]}`;
  }

  return null;
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

export function buildNotificationsReturnPath(): string {
  return NOTIFICATIONS_RETURN_PATH;
}

export function buildMessagesReturnPath(route: MessagesRoute = {}): string | null {
  const hasTarget = route.channelId != null || route.messageId != null;
  const path = buildMessagesPath(route);
  if (hasTarget && path === MESSAGES_ENTRY_PATH) {
    return null;
  }

  if (route.messageId != null && !path.includes('messageId=')) {
    return null;
  }

  return path;
}

export function buildMeReturnPath(): string {
  return ME_RETURN_PATH;
}

export function buildMeAssetsReturnPath(): string {
  return ME_ASSETS_RETURN_PATH;
}

export function buildMeAssetTransactionsReturnPath(): string {
  return ME_ASSET_TRANSACTIONS_RETURN_PATH;
}

export function buildPetReturnPath(): string {
  return PET_ENTRY_PATH;
}

export function buildShopProductPurchaseReturnPath(productId: string | number): string | null {
  const normalizedProductId = String(productId).trim();
  if (!POSITIVE_LONG_ID_PATTERN.test(normalizedProductId)) {
    return null;
  }

  return `/shop/product/${normalizedProductId}?intent=purchase`;
}

export function buildShopOrdersReturnPath(): string {
  return '/shop/orders';
}

export function buildShopOrderReturnPath(orderId: string | number): string | null {
  const normalizedOrderId = String(orderId).trim();
  if (!POSITIVE_LONG_ID_PATTERN.test(normalizedOrderId)) {
    return null;
  }

  return `/shop/order/${normalizedOrderId}`;
}

export function buildShopInventoryReturnPath(): string {
  return '/shop/inventory';
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

export function buildPublicForumPostReturnPath(target: {
  postId?: string | null;
  postPublicId?: string | null;
  commentId?: string | null;
  intent: PublicForumPostReturnIntent;
}): string | null {
  if (target.intent !== 'comment' && target.intent !== 'quickReply') {
    return null;
  }

  const normalizedPostPublicId = normalizeForumPostIdentifier(target.postPublicId);
  const normalizedPostId = normalizeForumPostIdentifier(target.postId);
  const normalizedCommentId = target.commentId == null ? '' : String(target.commentId).trim();
  const postIdentifier = normalizedPostPublicId ?? normalizedPostId;

  if (!postIdentifier) {
    return null;
  }

  if (normalizedCommentId && !POSITIVE_LONG_ID_PATTERN.test(normalizedCommentId)) {
    return null;
  }

  const query = new URLSearchParams();
  if (normalizedCommentId) {
    query.set('commentId', normalizedCommentId);
  }
  query.set('intent', target.intent);

  return normalizeAuthReturnPath(`/forum/post/${encodeURIComponent(postIdentifier)}?${query.toString()}`);
}
