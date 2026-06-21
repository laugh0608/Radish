export const ME_ENTRY_PATH = '/me';
export const ME_ASSETS_PATH = '/me/assets';
export const ME_ASSET_TRANSACTIONS_PATH = '/me/assets/transactions';
export const ME_CONTENT_PATH = '/me/content';
export const ME_HISTORY_PATH = '/me/history';
export const ME_ATTACHMENTS_PATH = '/me/attachments';
export const ME_EXPERIENCE_PATH = '/me/experience';

export type MeContentTab = 'posts' | 'comments' | 'quick-replies';
export type MeAttachmentBusinessType = 'All' | 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

export interface MeDashboardRoute {
  kind: 'dashboard';
}

export interface MeAssetsRoute {
  kind: 'assets';
}

export interface MeAssetTransactionsRoute {
  kind: 'assets-transactions';
}

export interface MeContentRoute {
  kind: 'content';
  tab: MeContentTab;
  page: number;
}

export interface MeHistoryRoute {
  kind: 'history';
  page: number;
}

export interface MeAttachmentsRoute {
  kind: 'attachments';
  businessType: MeAttachmentBusinessType;
  keyword: string;
  page: number;
}

export interface MeExperienceRoute {
  kind: 'experience';
  page: number;
}

export type MeRoute =
  | MeDashboardRoute
  | MeAssetsRoute
  | MeAssetTransactionsRoute
  | MeContentRoute
  | MeHistoryRoute
  | MeAttachmentsRoute
  | MeExperienceRoute;

const CONTENT_TABS = new Set<MeContentTab>(['posts', 'comments', 'quick-replies']);
const ATTACHMENT_BUSINESS_TYPES = new Set<MeAttachmentBusinessType>([
  'All',
  'General',
  'Post',
  'Comment',
  'Avatar',
  'Document'
]);

export function createDefaultMeRoute(): MeDashboardRoute {
  return {
    kind: 'dashboard'
  };
}

function normalizePage(value: string | null | undefined): number {
  const normalized = value?.trim();
  if (!normalized || !/^[1-9]\d*$/.test(normalized)) {
    return 1;
  }

  const parsed = Number(normalized);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function normalizeContentTab(value: string | null | undefined): MeContentTab {
  return value && CONTENT_TABS.has(value as MeContentTab) ? value as MeContentTab : 'posts';
}

function normalizeAttachmentBusinessType(value: string | null | undefined): MeAttachmentBusinessType {
  return value && ATTACHMENT_BUSINESS_TYPES.has(value as MeAttachmentBusinessType)
    ? value as MeAttachmentBusinessType
    : 'All';
}

function normalizeKeyword(value: string | null | undefined): string {
  return value?.trim() ?? '';
}

function buildQuery(params: Record<string, string | number | null | undefined>): string {
  const search = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value == null || value === '' || value === 1 || value === 'All' || value === 'posts') {
      return;
    }

    search.set(key, String(value));
  });

  const query = search.toString();
  return query ? `?${query}` : '';
}

export function parseMeRoute(pathname: string, search: string = ''): MeRoute | null {
  const params = new URLSearchParams(search);

  if (pathname === ME_ENTRY_PATH || pathname === `${ME_ENTRY_PATH}/`) {
    return createDefaultMeRoute();
  }

  if (pathname === ME_ASSETS_PATH || pathname === `${ME_ASSETS_PATH}/`) {
    return {
      kind: 'assets'
    };
  }

  if (pathname === ME_ASSET_TRANSACTIONS_PATH || pathname === `${ME_ASSET_TRANSACTIONS_PATH}/`) {
    return {
      kind: 'assets-transactions'
    };
  }

  if (pathname === ME_CONTENT_PATH || pathname === `${ME_CONTENT_PATH}/`) {
    return {
      kind: 'content',
      tab: normalizeContentTab(params.get('tab')),
      page: normalizePage(params.get('page'))
    };
  }

  if (pathname === ME_HISTORY_PATH || pathname === `${ME_HISTORY_PATH}/`) {
    return {
      kind: 'history',
      page: normalizePage(params.get('page'))
    };
  }

  if (pathname === ME_ATTACHMENTS_PATH || pathname === `${ME_ATTACHMENTS_PATH}/`) {
    return {
      kind: 'attachments',
      businessType: normalizeAttachmentBusinessType(params.get('businessType')),
      keyword: normalizeKeyword(params.get('keyword')),
      page: normalizePage(params.get('page'))
    };
  }

  if (pathname === ME_EXPERIENCE_PATH || pathname === `${ME_EXPERIENCE_PATH}/`) {
    return {
      kind: 'experience',
      page: normalizePage(params.get('page'))
    };
  }

  return null;
}

export function buildMePath(route: MeRoute = createDefaultMeRoute()): string {
  if (route.kind === 'assets') {
    return ME_ASSETS_PATH;
  }

  if (route.kind === 'assets-transactions') {
    return ME_ASSET_TRANSACTIONS_PATH;
  }

  if (route.kind === 'content') {
    return `${ME_CONTENT_PATH}${buildQuery({
      tab: route.tab,
      page: route.page
    })}`;
  }

  if (route.kind === 'history') {
    return `${ME_HISTORY_PATH}${buildQuery({
      page: route.page
    })}`;
  }

  if (route.kind === 'attachments') {
    return `${ME_ATTACHMENTS_PATH}${buildQuery({
      businessType: route.businessType,
      keyword: route.keyword,
      page: route.page
    })}`;
  }

  if (route.kind === 'experience') {
    return `${ME_EXPERIENCE_PATH}${buildQuery({
      page: route.page
    })}`;
  }

  return ME_ENTRY_PATH;
}

export function isMePathname(pathname: string): boolean {
  return parseMeRoute(pathname) !== null;
}
