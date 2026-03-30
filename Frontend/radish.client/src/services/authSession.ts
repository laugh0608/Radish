export const AUTH_TOKEN_EXPIRED_EVENT = 'auth:token-expired';

export const TokenRefreshFailureReason = {
  MissingRefreshToken: 'missing_refresh_token',
  InvalidRefreshToken: 'invalid_refresh_token',
  NetworkError: 'network_error',
  ServerError: 'server_error',
  Unknown: 'unknown',
} as const;

export type TokenRefreshFailureReason = (typeof TokenRefreshFailureReason)[keyof typeof TokenRefreshFailureReason];

export function hasAuthenticatedSession(authenticated: boolean, userId: number): boolean {
  return authenticated && userId > 0;
}

export function classifyTokenRefreshFailure(status?: number, error?: unknown): TokenRefreshFailureReason {
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
  _reason: TokenRefreshFailureReason,
  isTokenExpired: boolean,
): boolean {
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
