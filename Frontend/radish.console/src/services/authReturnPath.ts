const CONSOLE_AUTH_RETURN_PATH_STORAGE_KEY = 'radish.console.authReturnPath';
const CONSOLE_AUTH_RETURN_PATH_BASE_URL = 'https://radish.local';
const FORBIDDEN_PATHS = new Set(['/login', '/callback']);
const FORBIDDEN_CREDENTIAL_QUERY_KEYS = new Set([
  'access_token',
  'authorization',
  'code',
  'id_token',
  'refresh_token',
  'state',
  'token',
]);

interface SessionStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

interface ConsoleLocationLike {
  pathname: string;
  search?: string;
  hash?: string;
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

function containsForbiddenCredentialParameter(parameters: URLSearchParams): boolean {
  return [...parameters.keys()].some((key) => FORBIDDEN_CREDENTIAL_QUERY_KEYS.has(key.toLowerCase()));
}

export function normalizeConsoleAuthReturnPath(value?: string | null): string | undefined {
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
    parsedUrl = new URL(normalized, CONSOLE_AUTH_RETURN_PATH_BASE_URL);
  } catch {
    return undefined;
  }

  const fragmentParameters = new URLSearchParams(parsedUrl.hash.slice(1).replace(/^\?/u, ''));

  if (
    parsedUrl.origin !== CONSOLE_AUTH_RETURN_PATH_BASE_URL
    || containsForbiddenCredentialParameter(parsedUrl.searchParams)
    || containsForbiddenCredentialParameter(fragmentParameters)
  ) {
    return undefined;
  }

  const lowerPathname = parsedUrl.pathname.toLowerCase();
  const pathname = lowerPathname === '/console'
    ? '/'
    : lowerPathname.startsWith('/console/')
      ? parsedUrl.pathname.slice('/console'.length)
      : parsedUrl.pathname;
  if (FORBIDDEN_PATHS.has(pathname.toLowerCase())) {
    return undefined;
  }

  return `${pathname}${parsedUrl.search}${parsedUrl.hash}`;
}

export function rememberConsoleAuthReturnPath(
  location: ConsoleLocationLike,
  storage: SessionStorageLike | null = getSessionStorage(),
): boolean {
  const returnPath = normalizeConsoleAuthReturnPath(
    `${location.pathname}${location.search ?? ''}${location.hash ?? ''}`,
  );
  if (!returnPath || !storage) {
    return false;
  }

  try {
    storage.setItem(CONSOLE_AUTH_RETURN_PATH_STORAGE_KEY, returnPath);
    return true;
  } catch {
    return false;
  }
}

export function consumeConsoleAuthReturnPath(
  storage: SessionStorageLike | null = getSessionStorage(),
  fallbackPath = '/',
): string {
  if (!storage) {
    return fallbackPath;
  }

  try {
    const storedPath = storage.getItem(CONSOLE_AUTH_RETURN_PATH_STORAGE_KEY);
    storage.removeItem(CONSOLE_AUTH_RETURN_PATH_STORAGE_KEY);
    return normalizeConsoleAuthReturnPath(storedPath) ?? fallbackPath;
  } catch {
    return fallbackPath;
  }
}
