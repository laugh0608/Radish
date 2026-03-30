const OIDC_CALLBACK_SESSION_PREFIX = 'radish:oidc:callback:';
const OIDC_CALLBACK_SESSION_TTL_MS = 5 * 60 * 1000;
const SENSITIVE_QUERY_PARAMS = ['code', 'state', 'iss', 'session_state'] as const;

const inFlightOidcRedemptions = new Map<string, Promise<OidcTokenResponse>>();

interface PendingOidcRedemption {
  code: string;
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
  missingAccessTokenMessage?: string;
  buildTokenRequestFailedMessage?: (details: OidcTokenRequestFailureDetails) => string;
}

export type OidcCallbackErrorCode =
  | 'missing_code'
  | 'stale_callback'
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

function buildSessionStorageKey(clientId: string): string {
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

  return window.sessionStorage;
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

  const storageKey = buildSessionStorageKey(clientId);
  const rawValue = storage.getItem(storageKey);
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

  storage.setItem(buildSessionStorageKey(clientId), JSON.stringify(pending));
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

  storage?.removeItem(buildSessionStorageKey(clientId));
}

function resolvePendingOidcRedemption(
  options: RedeemOidcAuthorizationCodeOptions,
  storage: Storage | null,
): PendingOidcRedemption {
  const locationHref = resolveLocationHref(options.locationHref);
  const currentUrl = new URL(locationHref);
  const codeFromUrl = currentUrl.searchParams.get('code')?.trim();

  if (codeFromUrl) {
    const resolvedHistory = resolveHistory(options.history);
    sanitizeOidcCallbackUrl(locationHref, resolvedHistory ?? undefined);

    const pending: PendingOidcRedemption = {
      code: codeFromUrl,
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
  const extraDetail = details.errorDescription ?? details.error;
  if (!extraDetail) {
    return `Token request failed: ${details.status} ${details.statusText}`;
  }

  return `Token request failed: ${details.status} ${details.statusText} (${extraDetail})`;
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

    const response = await fetchImpl(`${options.authServerBaseUrl}/connect/token`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body,
    });

    if (!response.ok) {
      const failureDetails = await extractFailureDetails(response);
      const message = options.buildTokenRequestFailedMessage
        ? options.buildTokenRequestFailedMessage(failureDetails)
        : buildDefaultTokenRequestFailedMessage(failureDetails);

      throw new OidcCallbackError('token_request_failed', message, failureDetails);
    }

    const tokenResponse = (await response.json()) as RawOidcTokenResponse;
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
