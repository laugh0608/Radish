const OIDC_AUTHORIZATION_SESSION_PREFIX = 'radish:oidc:authorization:';
const OIDC_CALLBACK_SESSION_PREFIX = 'radish:oidc:callback:';
const OIDC_CALLBACK_SESSION_TTL_MS = 5 * 60 * 1000;
const SENSITIVE_QUERY_PARAMS = [
  'code',
  'state',
  'iss',
  'session_state',
  'error',
  'error_description',
  'error_uri',
] as const;

const inFlightOidcRedemptions = new Map<string, Promise<OidcTokenResponse>>();

interface PendingOidcRedemption {
  code: string;
  codeVerifier: string;
  redirectUri: string;
  startedAt: number;
}

interface PendingOidcAuthorization {
  state: string;
  codeVerifier: string;
  redirectUri: string;
  startedAt: number;
}

interface RawOidcTokenResponse {
  access_token?: string;
  refresh_token?: string;
  expires_in?: number;
  token_type?: string;
  [key: string]: unknown;
}

export interface OidcTokenResponse extends RawOidcTokenResponse {
  access_token: string;
}

export interface OidcTokenRequestFailureDetails {
  status: number;
  statusText: string;
  error?: string;
  errorDescription?: string;
}

export interface OidcAuthorizationErrorDetails {
  error: string;
  errorDescription?: string;
  errorUri?: string;
}

interface OidcCrypto {
  getRandomValues<T extends ArrayBufferView>(array: T): T;
  subtle: Pick<SubtleCrypto, 'digest'>;
}

export interface CreateOidcAuthorizationUrlOptions {
  clientId: string;
  authServerBaseUrl: string;
  redirectUri: string;
  scope: string;
  additionalParameters?: Readonly<Record<string, string | undefined>>;
  sessionStorage?: Storage;
  cryptoImpl?: OidcCrypto;
  now?: number;
}

export interface RedeemOidcAuthorizationCodeOptions {
  clientId: string;
  authServerBaseUrl: string;
  redirectUri: string;
  fetchImpl?: typeof fetch;
  locationHref?: string;
  sessionStorage?: Storage;
  history?: Pick<History, 'replaceState' | 'state'>;
  missingCodeMessage?: string;
  staleCallbackMessage?: string;
  stateMismatchMessage?: string;
  attemptMissingOrExpiredMessage?: string;
  buildAuthorizationErrorMessage?: (details: OidcAuthorizationErrorDetails) => string;
  tokenRequestNetworkErrorMessage?: string;
  invalidTokenResponseMessage?: string;
  missingAccessTokenMessage?: string;
  buildTokenRequestFailedMessage?: (details: OidcTokenRequestFailureDetails) => string;
}

export type OidcCallbackErrorCode =
  | 'missing_code'
  | 'stale_callback'
  | 'state_mismatch'
  | 'attempt_missing_or_expired'
  | 'authorization_error'
  | 'crypto_unavailable'
  | 'session_storage_unavailable'
  | 'token_request_failed'
  | 'missing_access_token';

export class OidcCallbackError extends Error {
  readonly code: OidcCallbackErrorCode;
  readonly details?: unknown;

  constructor(code: OidcCallbackErrorCode, message: string, details?: unknown) {
    super(message);
    this.name = 'OidcCallbackError';
    this.code = code;
    this.details = details;
  }
}

function buildAuthorizationStorageKey(clientId: string): string {
  return `${OIDC_AUTHORIZATION_SESSION_PREFIX}${clientId}`;
}

function buildRedemptionStorageKey(clientId: string): string {
  return `${OIDC_CALLBACK_SESSION_PREFIX}${clientId}`;
}

function buildRequestKey(clientId: string, redirectUri: string, code: string): string {
  return `${encodeURIComponent(clientId)}|${encodeURIComponent(redirectUri)}|${encodeURIComponent(code)}`;
}

function resolveSessionStorage(storage?: Storage): Storage | null {
  if (storage) {
    return storage;
  }

  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

function resolveCrypto(cryptoImpl?: OidcCrypto): OidcCrypto {
  if (cryptoImpl) {
    return cryptoImpl;
  }

  if (typeof globalThis.crypto !== 'undefined') {
    return globalThis.crypto;
  }

  throw new OidcCallbackError(
    'crypto_unavailable',
    'Secure browser cryptography is unavailable. Cannot start OIDC sign-in.',
  );
}

function encodeBase64Url(bytes: Uint8Array): string {
  let binary = '';
  bytes.forEach((value) => {
    binary += String.fromCharCode(value);
  });

  return btoa(binary)
    .replace(/\+/gu, '-')
    .replace(/\//gu, '_')
    .replace(/=+$/u, '');
}

function createRandomBase64Url(cryptoImpl: OidcCrypto): string {
  const bytes = new Uint8Array(32);
  cryptoImpl.getRandomValues(bytes);
  return encodeBase64Url(bytes);
}

async function createCodeChallenge(cryptoImpl: OidcCrypto, codeVerifier: string): Promise<string> {
  const digest = await cryptoImpl.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier));
  return encodeBase64Url(new Uint8Array(digest));
}

function writePendingOidcAuthorization(
  storage: Storage | null,
  clientId: string,
  pending: PendingOidcAuthorization,
): void {
  if (!storage) {
    throw new OidcCallbackError(
      'session_storage_unavailable',
      'Session storage is unavailable. Cannot start OIDC sign-in safely.',
    );
  }

  try {
    storage.setItem(buildAuthorizationStorageKey(clientId), JSON.stringify(pending));
  } catch {
    throw new OidcCallbackError(
      'session_storage_unavailable',
      'Session storage is unavailable. Cannot start OIDC sign-in safely.',
    );
  }
}

function readPendingOidcAuthorization(
  storage: Storage | null,
  clientId: string,
  redirectUri: string,
): PendingOidcAuthorization | null {
  if (!storage) {
    return null;
  }

  const storageKey = buildAuthorizationStorageKey(clientId);
  let rawValue: string | null;
  try {
    rawValue = storage.getItem(storageKey);
  } catch {
    return null;
  }

  if (!rawValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(rawValue) as PendingOidcAuthorization;
    const isFresh = typeof parsed.startedAt === 'number' && Date.now() - parsed.startedAt <= OIDC_CALLBACK_SESSION_TTL_MS;
    const isValid =
      typeof parsed.state === 'string'
      && parsed.state.trim() !== ''
      && typeof parsed.codeVerifier === 'string'
      && parsed.codeVerifier.length >= 43
      && typeof parsed.redirectUri === 'string'
      && parsed.redirectUri === redirectUri;

    if (!isFresh || !isValid) {
      storage.removeItem(storageKey);
      return null;
    }

    return parsed;
  } catch {
    try {
      storage.removeItem(storageKey);
    } catch {
      // The callback will fail closed when storage cannot be cleaned.
    }
    return null;
  }
}

function clearPendingOidcAuthorization(storage: Storage | null, clientId: string): void {
  try {
    storage?.removeItem(buildAuthorizationStorageKey(clientId));
  } catch {
    // The current callback still fails closed; no fallback state is accepted.
  }
}

export async function createOidcAuthorizationUrl(
  options: CreateOidcAuthorizationUrlOptions,
): Promise<string> {
  const cryptoImpl = resolveCrypto(options.cryptoImpl);
  const storage = resolveSessionStorage(options.sessionStorage);
  const state = createRandomBase64Url(cryptoImpl);
  const codeVerifier = createRandomBase64Url(cryptoImpl);
  const codeChallenge = await createCodeChallenge(cryptoImpl, codeVerifier);

  writePendingOidcAuthorization(storage, options.clientId, {
    state,
    codeVerifier,
    redirectUri: options.redirectUri,
    startedAt: options.now ?? Date.now(),
  });

  const authorizeUrl = new URL(`${options.authServerBaseUrl.replace(/\/$/u, '')}/connect/authorize`);
  Object.entries(options.additionalParameters ?? {}).forEach(([key, value]) => {
    if (value !== undefined) {
      authorizeUrl.searchParams.set(key, value);
    }
  });

  authorizeUrl.searchParams.set('client_id', options.clientId);
  authorizeUrl.searchParams.set('response_type', 'code');
  authorizeUrl.searchParams.set('redirect_uri', options.redirectUri);
  authorizeUrl.searchParams.set('scope', options.scope);
  authorizeUrl.searchParams.set('state', state);
  authorizeUrl.searchParams.set('code_challenge', codeChallenge);
  authorizeUrl.searchParams.set('code_challenge_method', 'S256');

  return authorizeUrl.toString();
}

function resolveLocationHref(locationHref?: string): string {
  if (locationHref) {
    return locationHref;
  }

  if (typeof window === 'undefined') {
    throw new OidcCallbackError('missing_code', 'OIDC callback requires a browser environment.');
  }

  return window.location.href;
}

function resolveHistory(history?: Pick<History, 'replaceState' | 'state'>): Pick<History, 'replaceState' | 'state'> | null {
  if (history) {
    return history;
  }

  if (typeof window === 'undefined') {
    return null;
  }

  return window.history;
}

function sanitizeOidcCallbackUrl(
  locationHref: string,
  history?: Pick<History, 'replaceState' | 'state'>,
): void {
  const currentUrl = new URL(locationHref);
  let mutated = false;

  SENSITIVE_QUERY_PARAMS.forEach((param) => {
    if (currentUrl.searchParams.has(param)) {
      currentUrl.searchParams.delete(param);
      mutated = true;
    }
  });

  if (!mutated) {
    return;
  }

  history?.replaceState(history.state ?? null, '', currentUrl.toString());
}

function readPendingOidcRedemption(
  storage: Storage | null,
  clientId: string,
  redirectUri: string,
): PendingOidcRedemption | null {
  if (!storage) {
    return null;
  }

  const storageKey = buildRedemptionStorageKey(clientId);
  let rawValue: string | null;
  try {
    rawValue = storage.getItem(storageKey);
  } catch {
    return null;
  }
  if (!rawValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(rawValue) as PendingOidcRedemption;
    const isFresh = typeof parsed.startedAt === 'number' && Date.now() - parsed.startedAt <= OIDC_CALLBACK_SESSION_TTL_MS;
    const isValid =
      parsed &&
      typeof parsed.code === 'string' &&
      parsed.code.trim() !== '' &&
      typeof parsed.codeVerifier === 'string' &&
      parsed.codeVerifier.length >= 43 &&
      typeof parsed.redirectUri === 'string' &&
      parsed.redirectUri === redirectUri;

    if (!isFresh || !isValid) {
      storage.removeItem(storageKey);
      return null;
    }

    return parsed;
  } catch {
    storage.removeItem(storageKey);
    return null;
  }
}

function writePendingOidcRedemption(
  storage: Storage | null,
  clientId: string,
  pending: PendingOidcRedemption,
): void {
  if (!storage) {
    return;
  }

  storage.setItem(buildRedemptionStorageKey(clientId), JSON.stringify(pending));
}

function clearPendingOidcRedemption(
  storage: Storage | null,
  clientId: string,
  redirectUri: string,
  code: string,
): void {
  const pending = readPendingOidcRedemption(storage, clientId, redirectUri);
  if (!pending || pending.code !== code) {
    return;
  }

  storage?.removeItem(buildRedemptionStorageKey(clientId));
}

function resolvePendingOidcRedemption(
  options: RedeemOidcAuthorizationCodeOptions,
  storage: Storage | null,
): PendingOidcRedemption {
  const locationHref = resolveLocationHref(options.locationHref);
  const currentUrl = new URL(locationHref);
  const codeFromUrl = currentUrl.searchParams.get('code')?.trim();
  const stateFromUrl = currentUrl.searchParams.get('state')?.trim();
  const authorizationError = currentUrl.searchParams.get('error')?.trim();
  const hasCallbackParameters = SENSITIVE_QUERY_PARAMS.some((param) => currentUrl.searchParams.has(param));

  if (hasCallbackParameters) {
    const resolvedHistory = resolveHistory(options.history);
    sanitizeOidcCallbackUrl(locationHref, resolvedHistory ?? undefined);
    const attempt = readPendingOidcAuthorization(storage, options.clientId, options.redirectUri);
    clearPendingOidcAuthorization(storage, options.clientId);

    if (!attempt) {
      throw new OidcCallbackError(
        'attempt_missing_or_expired',
        options.attemptMissingOrExpiredMessage ?? 'OIDC sign-in attempt is missing or has expired.',
      );
    }

    if (!stateFromUrl || stateFromUrl !== attempt.state) {
      throw new OidcCallbackError(
        'state_mismatch',
        options.stateMismatchMessage ?? 'OIDC callback state validation failed.',
      );
    }

    if (authorizationError) {
      const details: OidcAuthorizationErrorDetails = {
        error: authorizationError,
        errorDescription: currentUrl.searchParams.get('error_description')?.trim() || undefined,
        errorUri: currentUrl.searchParams.get('error_uri')?.trim() || undefined,
      };
      const message = options.buildAuthorizationErrorMessage?.(details)
        ?? `Authorization failed: ${details.error}`;
      throw new OidcCallbackError('authorization_error', message, details);
    }

    if (!codeFromUrl) {
      throw new OidcCallbackError(
        'missing_code',
        options.missingCodeMessage ?? 'Missing authorization code.',
      );
    }

    const pending: PendingOidcRedemption = {
      code: codeFromUrl,
      codeVerifier: attempt.codeVerifier,
      redirectUri: options.redirectUri,
      startedAt: Date.now(),
    };

    writePendingOidcRedemption(storage, options.clientId, pending);
    return pending;
  }

  const pending = readPendingOidcRedemption(storage, options.clientId, options.redirectUri);
  if (!pending) {
    throw new OidcCallbackError(
      'missing_code',
      options.missingCodeMessage ?? 'Missing authorization code.',
    );
  }

  const requestKey = buildRequestKey(options.clientId, options.redirectUri, pending.code);
  if (!inFlightOidcRedemptions.has(requestKey)) {
    clearPendingOidcRedemption(storage, options.clientId, options.redirectUri, pending.code);
    throw new OidcCallbackError(
      'stale_callback',
      options.staleCallbackMessage ?? 'OIDC callback has expired. Please start the sign-in flow again.',
    );
  }

  return pending;
}

async function extractFailureDetails(response: Response): Promise<OidcTokenRequestFailureDetails> {
  const details: OidcTokenRequestFailureDetails = {
    status: response.status,
    statusText: response.statusText,
  };

  try {
    const payload = (await response.json()) as {
      error?: unknown;
      error_description?: unknown;
    };

    if (typeof payload.error === 'string' && payload.error.trim() !== '') {
      details.error = payload.error.trim();
    }

    if (typeof payload.error_description === 'string' && payload.error_description.trim() !== '') {
      details.errorDescription = payload.error_description.trim();
    }
  } catch {
    // ignore malformed or empty response bodies
  }

  return details;
}

function buildDefaultTokenRequestFailedMessage(details: OidcTokenRequestFailureDetails): string {
  return `Token request failed: ${details.status} ${details.statusText}`;
}

export async function redeemOidcAuthorizationCode(
  options: RedeemOidcAuthorizationCodeOptions,
): Promise<OidcTokenResponse> {
  const storage = resolveSessionStorage(options.sessionStorage);
  const pending = resolvePendingOidcRedemption(options, storage);
  const requestKey = buildRequestKey(options.clientId, options.redirectUri, pending.code);
  const existingRequest = inFlightOidcRedemptions.get(requestKey);
  if (existingRequest) {
    return existingRequest;
  }

  const fetchImpl = options.fetchImpl ?? fetch;
  const requestPromise = (async () => {
    const body = new URLSearchParams();
    body.set('grant_type', 'authorization_code');
    body.set('client_id', options.clientId);
    body.set('code', pending.code);
    body.set('redirect_uri', options.redirectUri);
    body.set('code_verifier', pending.codeVerifier);

    let response: Response;
    try {
      response = await fetchImpl(`${options.authServerBaseUrl}/connect/token`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body,
      });
    } catch (error) {
      throw new OidcCallbackError(
        'token_request_failed',
        options.tokenRequestNetworkErrorMessage ?? 'Token request could not be completed.',
        error,
      );
    }

    if (!response.ok) {
      const failureDetails = await extractFailureDetails(response);
      const message = options.buildTokenRequestFailedMessage
        ? options.buildTokenRequestFailedMessage(failureDetails)
        : buildDefaultTokenRequestFailedMessage(failureDetails);

      throw new OidcCallbackError('token_request_failed', message, failureDetails);
    }

    let tokenResponse: RawOidcTokenResponse;
    try {
      tokenResponse = (await response.json()) as RawOidcTokenResponse;
    } catch (error) {
      throw new OidcCallbackError(
        'token_request_failed',
        options.invalidTokenResponseMessage ?? 'Token response is invalid.',
        error,
      );
    }
    if (!tokenResponse.access_token) {
      throw new OidcCallbackError(
        'missing_access_token',
        options.missingAccessTokenMessage ?? 'Token response does not contain access_token.',
        tokenResponse,
      );
    }

    return tokenResponse as OidcTokenResponse;
  })().finally(() => {
    inFlightOidcRedemptions.delete(requestKey);
    clearPendingOidcRedemption(storage, options.clientId, options.redirectUri, pending.code);
  });

  inFlightOidcRedemptions.set(requestKey, requestPromise);
  return requestPromise;
}
