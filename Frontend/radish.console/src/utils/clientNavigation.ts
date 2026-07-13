const CLIENT_BACK_TO_STORAGE_KEY = 'radish.console.clientBackTo';

const CLIENT_ROUTE_PREFIXES = [
  '/discover',
  '/forum',
  '/docs',
  '/u',
  '/leaderboard',
  '/shop',
  '/messages',
  '/notifications',
  '/circle',
  '/me',
  '/pet',
  '/workbench',
  '/desktop',
] as const;

const FORBIDDEN_CREDENTIAL_QUERY_KEYS = new Set([
  'access_token',
  'authorization',
  'code',
  'id_token',
  'refresh_token',
  'token',
]);

interface SessionStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function readStoredBackTo(storage: SessionStorageLike): string | null {
  try {
    return storage.getItem(CLIENT_BACK_TO_STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStoredBackTo(storage: SessionStorageLike, backTo: string): void {
  try {
    storage.setItem(CLIENT_BACK_TO_STORAGE_KEY, backTo);
  } catch {
    // 浏览器禁用或写满 sessionStorage 时退化为本次直接导航，不阻断 Console 首屏。
  }
}

function removeStoredBackTo(storage: SessionStorageLike): void {
  try {
    storage.removeItem(CLIENT_BACK_TO_STORAGE_KEY);
  } catch {
    // sessionStorage 不可用时无需再清理，返回入口会回落到 /workbench。
  }
}

function getSessionStorage(): SessionStorageLike | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

function containsControlCharacter(value: string): boolean {
  return [...value].some((character) => {
    const codePoint = character.codePointAt(0) ?? 0;
    return codePoint <= 31 || codePoint === 127;
  });
}

function matchesClientRoute(pathname: string): boolean {
  return CLIENT_ROUTE_PREFIXES.some((prefix) => (
    pathname === prefix || pathname.startsWith(`${prefix}/`)
  ));
}

export function normalizeClientBackTo(value?: string | null): string | undefined {
  const normalized = value?.trim();
  if (
    !normalized
    || !normalized.startsWith('/')
    || normalized.startsWith('//')
    || normalized.includes('\\')
    || containsControlCharacter(normalized)
  ) {
    return undefined;
  }

  let parsedUrl: URL;
  try {
    parsedUrl = new URL(normalized, 'https://radish.local');
  } catch {
    return undefined;
  }

  if (parsedUrl.origin !== 'https://radish.local' || !matchesClientRoute(parsedUrl.pathname)) {
    return undefined;
  }

  for (const key of parsedUrl.searchParams.keys()) {
    if (FORBIDDEN_CREDENTIAL_QUERY_KEYS.has(key.toLowerCase())) {
      return undefined;
    }
  }

  return `${parsedUrl.pathname}${parsedUrl.search}${parsedUrl.hash}`;
}

export function rememberClientBackTo(
  search: string,
  storage: SessionStorageLike | null = getSessionStorage(),
): string | undefined {
  const searchParams = new URLSearchParams(search);
  if (!searchParams.has('backTo')) {
    return getRememberedClientBackTo(storage);
  }

  const backTo = normalizeClientBackTo(searchParams.get('backTo'));
  if (!storage) {
    return backTo;
  }

  if (backTo) {
    writeStoredBackTo(storage, backTo);
  } else {
    removeStoredBackTo(storage);
  }

  return backTo;
}

export function getRememberedClientBackTo(
  storage: SessionStorageLike | null = getSessionStorage(),
): string | undefined {
  if (!storage) {
    return undefined;
  }

  return normalizeClientBackTo(readStoredBackTo(storage));
}

export function clearRememberedClientBackTo(
  storage: SessionStorageLike | null = getSessionStorage(),
): void {
  if (storage) {
    removeStoredBackTo(storage);
  }
}

export function resolveClientBackLabel(backTo: string): string {
  const pathname = new URL(backTo, 'https://radish.local').pathname;
  if (pathname.startsWith('/forum')) {
    return '返回论坛';
  }
  if (pathname.startsWith('/shop')) {
    return '返回商城';
  }
  if (pathname.startsWith('/messages')) {
    return '返回消息';
  }
  if (pathname.startsWith('/workbench')) {
    return '返回功能地图';
  }

  return '返回社区';
}
