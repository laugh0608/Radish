export const AUTH_TOKEN_EXPIRED_EVENT = 'auth:token-expired';
export const IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY = 'radish_client_session_last_active_at';
export const IDLE_SESSION_REFRESH_PARAMETER = 'radish_last_active_at';
export const DEFAULT_IDLE_SESSION_TIMEOUT_DAYS = 7;
export const DEFAULT_IDLE_SESSION_CLOCK_SKEW_SECONDS = 60;
const DEFAULT_ACTIVITY_WRITE_THROTTLE_MS = 60_000;
const DEFAULT_IDLE_SESSION_TIMEOUT_MS = DEFAULT_IDLE_SESSION_TIMEOUT_DAYS * 24 * 60 * 60 * 1000;
const DEFAULT_IDLE_SESSION_CLOCK_SKEW_MS = DEFAULT_IDLE_SESSION_CLOCK_SKEW_SECONDS * 1000;

export const TokenRefreshFailureReason = {
  MissingRefreshToken: 'missing_refresh_token',
  InvalidRefreshToken: 'invalid_refresh_token',
  IdleSessionExpired: 'idle_session_expired',
  NetworkError: 'network_error',
  ServerError: 'server_error',
  Unknown: 'unknown',
} as const;

export type TokenRefreshFailureReason = (typeof TokenRefreshFailureReason)[keyof typeof TokenRefreshFailureReason];

interface AuthSessionStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

interface RecordAuthSessionActivityOptions {
  force?: boolean;
  nowMs?: number;
  storage?: AuthSessionStorage | null;
  throttleMs?: number;
}

interface IsAuthSessionIdleExpiredOptions {
  nowMs?: number;
  storage?: AuthSessionStorage | null;
  timeoutMs?: number;
  clockSkewMs?: number;
}

interface StartAuthSessionActivityTrackingOptions {
  onIdleExpired?: () => void;
}

export function hasAuthenticatedSession(authenticated: boolean, userId: string | null | undefined): boolean {
  if (!authenticated) {
    return false;
  }

  if (typeof userId === 'number') {
    return Number.isSafeInteger(userId) && userId > 0;
  }

  if (typeof userId !== 'string') {
    return false;
  }

  return /^[1-9]\d*$/.test(userId.trim());
}

export function classifyTokenRefreshFailure(status?: number, error?: unknown): TokenRefreshFailureReason {
  if (error instanceof Error && error.message.includes('session_idle_expired')) {
    return TokenRefreshFailureReason.IdleSessionExpired;
  }

  if (status === 400 || status === 401) {
    return TokenRefreshFailureReason.InvalidRefreshToken;
  }

  if (status && status >= 500) {
    return TokenRefreshFailureReason.ServerError;
  }

  if (error instanceof TypeError && error.message.toLowerCase().includes('fetch')) {
    return TokenRefreshFailureReason.NetworkError;
  }

  return TokenRefreshFailureReason.Unknown;
}

export function shouldInvalidateSessionAfterRefreshFailure(
  reason: TokenRefreshFailureReason,
  isTokenExpired: boolean,
): boolean {
  if (reason === TokenRefreshFailureReason.IdleSessionExpired) {
    return true;
  }

  return isTokenExpired;
}

export function dispatchAuthTokenExpired(reason: TokenRefreshFailureReason): void {
  if (typeof window === 'undefined') {
    return;
  }

  window.dispatchEvent(new CustomEvent(AUTH_TOKEN_EXPIRED_EVENT, {
    detail: { reason },
  }));
}

export function parseAuthSessionEpochSeconds(value: string | null | undefined): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

export function getAuthSessionLastActiveAt(storage = getDefaultAuthSessionStorage()): number | null {
  return parseAuthSessionEpochSeconds(storage?.getItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY));
}

export function clearAuthSessionActivity(storage = getDefaultAuthSessionStorage()): void {
  storage?.removeItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY);
}

export function recordAuthSessionActivity(options: RecordAuthSessionActivityOptions = {}): number | null {
  const {
    force = false,
    nowMs = Date.now(),
    storage = getDefaultAuthSessionStorage(),
    throttleMs = DEFAULT_ACTIVITY_WRITE_THROTTLE_MS,
  } = options;

  if (!storage) {
    return null;
  }

  const currentSeconds = Math.floor(nowMs / 1000);
  const previousSeconds = getAuthSessionLastActiveAt(storage);
  if (!force && previousSeconds !== null && nowMs - previousSeconds * 1000 < throttleMs) {
    return previousSeconds;
  }

  storage.setItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY, String(currentSeconds));
  return currentSeconds;
}

export function isAuthSessionIdleExpired(options: IsAuthSessionIdleExpiredOptions = {}): boolean {
  const {
    nowMs = Date.now(),
    storage = getDefaultAuthSessionStorage(),
    timeoutMs = DEFAULT_IDLE_SESSION_TIMEOUT_MS,
    clockSkewMs = DEFAULT_IDLE_SESSION_CLOCK_SKEW_MS,
  } = options;

  const lastActiveAt = getAuthSessionLastActiveAt(storage);
  if (lastActiveAt === null) {
    return false;
  }

  return nowMs > lastActiveAt * 1000 + timeoutMs + clockSkewMs;
}

export function getAuthSessionRefreshParameters(
  storage = getDefaultAuthSessionStorage(),
): Record<string, string> {
  const lastActiveAt = getAuthSessionLastActiveAt(storage);
  return lastActiveAt === null
    ? {}
    : { [IDLE_SESSION_REFRESH_PARAMETER]: String(lastActiveAt) };
}

export function startAuthSessionActivityTracking(
  options: StartAuthSessionActivityTrackingOptions = {},
): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

  const handleActivity = () => {
    if (document.visibilityState === 'hidden') {
      return;
    }

    if (isAuthSessionIdleExpired()) {
      options.onIdleExpired?.();
      return;
    }

    recordAuthSessionActivity();
  };

  const handleVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      handleActivity();
    }
  };

  const passiveOptions: AddEventListenerOptions = { passive: true };
  window.addEventListener('focus', handleActivity);
  window.addEventListener('pageshow', handleActivity);
  window.addEventListener('pointerdown', handleActivity, passiveOptions);
  window.addEventListener('keydown', handleActivity);
  window.addEventListener('scroll', handleActivity, passiveOptions);
  window.addEventListener('touchstart', handleActivity, passiveOptions);
  document.addEventListener('visibilitychange', handleVisibilityChange);

  return () => {
    window.removeEventListener('focus', handleActivity);
    window.removeEventListener('pageshow', handleActivity);
    window.removeEventListener('pointerdown', handleActivity);
    window.removeEventListener('keydown', handleActivity);
    window.removeEventListener('scroll', handleActivity);
    window.removeEventListener('touchstart', handleActivity);
    document.removeEventListener('visibilitychange', handleVisibilityChange);
  };
}

function getDefaultAuthSessionStorage(): AuthSessionStorage | null {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.localStorage;
}
