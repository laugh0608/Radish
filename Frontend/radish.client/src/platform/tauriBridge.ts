const RADISH_OIDC_CALLBACK_PATH = '/oidc/callback';
const RADISH_OIDC_LOGOUT_COMPLETE_PATH = '/oidc/logout-complete';
const TAURI_OIDC_REDIRECT_URI_STORAGE_KEY = 'radish:tauri:oidc:redirect-uri';
const TAURI_OIDC_LOOPBACK_BASE = 'http://127.0.0.1:48801';
export const TAURI_DESKTOP_ENTRY_PATH = '/desktop';

type TauriInvoke = <T>(command: string, args?: Record<string, unknown>) => Promise<T>;
type TauriListen = <T>(
  event: string,
  handler: (event: { payload: T }) => void,
) => Promise<() => void>;

interface TauriGlobalApi {
  core?: {
    invoke?: TauriInvoke;
  };
  event?: {
    listen?: TauriListen;
  };
}

interface TauriDeepLinkPayload {
  urls?: string[];
}

interface InitializeTauriBridgeOptions {
  onDeepLink: (url: string) => void;
}

interface TauriSpikeInfo {
  app: string;
  shell: string;
}

interface TauriOidcLoopbackListenerInfo {
  redirectUri: string;
}

function getTauriApi(): TauriGlobalApi | undefined {
  if (typeof window === 'undefined') {
    return undefined;
  }

  return window.__TAURI__;
}

function getTauriInvoke(): TauriInvoke | undefined {
  return getTauriApi()?.core?.invoke;
}

function getTauriListen(): TauriListen | undefined {
  return getTauriApi()?.event?.listen;
}

export function isTauriRuntime(): boolean {
  return typeof getTauriInvoke() === 'function';
}

function getBrowserOidcRedirectUri(): string {
  return `${window.location.origin}${RADISH_OIDC_CALLBACK_PATH}`;
}

function getTauriFallbackOidcRedirectUri(): string {
  return `${TAURI_OIDC_LOOPBACK_BASE}${RADISH_OIDC_CALLBACK_PATH}`;
}

function getTauriFallbackPostLogoutRedirectUri(): string {
  return `${TAURI_OIDC_LOOPBACK_BASE}${RADISH_OIDC_LOGOUT_COMPLETE_PATH}`;
}

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

function rememberTauriOidcRedirectUri(redirectUri: string): void {
  getSessionStorage()?.setItem(TAURI_OIDC_REDIRECT_URI_STORAGE_KEY, redirectUri);
}

function readRememberedTauriOidcRedirectUri(): string | null {
  return getSessionStorage()?.getItem(TAURI_OIDC_REDIRECT_URI_STORAGE_KEY) ?? null;
}

export function getOidcRedirectUri(): string {
  if (!isTauriRuntime()) {
    return getBrowserOidcRedirectUri();
  }

  return readRememberedTauriOidcRedirectUri() ?? getTauriFallbackOidcRedirectUri();
}

export function getPostLogoutRedirectUri(): string {
  return isTauriRuntime() ? getTauriFallbackPostLogoutRedirectUri() : window.location.origin;
}

async function startOidcLoopbackListener(path: string): Promise<string> {
  const invoke = getTauriInvoke();
  if (!invoke) {
    throw new Error('Tauri invoke API is not available.');
  }

  const info = await invoke<TauriOidcLoopbackListenerInfo>('start_oidc_loopback_listener', { path });
  return info.redirectUri;
}

export async function prepareOidcRedirectUri(): Promise<string> {
  if (!isTauriRuntime()) {
    return getBrowserOidcRedirectUri();
  }

  const redirectUri = await startOidcLoopbackListener(RADISH_OIDC_CALLBACK_PATH);
  rememberTauriOidcRedirectUri(redirectUri);
  return redirectUri;
}

export async function preparePostLogoutRedirectUri(): Promise<string> {
  if (!isTauriRuntime()) {
    return window.location.origin;
  }

  return startOidcLoopbackListener(RADISH_OIDC_LOGOUT_COMPLETE_PATH);
}

export async function openExternalUrl(url: string): Promise<void> {
  const invoke = getTauriInvoke();
  if (!invoke) {
    window.location.href = url;
    return;
  }

  await invoke('open_external_url', { url });
}

export function rewriteRadishDeepLinkToBrowserPath(url: string): string | null {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  if (parsed.protocol !== 'radish:') {
    return null;
  }

  if (parsed.hostname === 'oidc' && parsed.pathname === '/callback') {
    return `${RADISH_OIDC_CALLBACK_PATH}${parsed.search}${parsed.hash}`;
  }

  if (parsed.hostname === 'oidc' && parsed.pathname === '/logout-complete') {
    return TAURI_DESKTOP_ENTRY_PATH;
  }

  return null;
}

export function rewriteOidcLoopbackToBrowserPath(url: string): string | null {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  const isLoopbackHost = parsed.hostname === '127.0.0.1' || parsed.hostname === 'localhost';
  if (parsed.protocol !== 'http:' || !isLoopbackHost) {
    return null;
  }

  if (parsed.pathname === RADISH_OIDC_CALLBACK_PATH) {
    return `${RADISH_OIDC_CALLBACK_PATH}${parsed.search}${parsed.hash}`;
  }

  if (parsed.pathname === RADISH_OIDC_LOGOUT_COMPLETE_PATH) {
    return TAURI_DESKTOP_ENTRY_PATH;
  }

  return null;
}

export function rewriteDesktopOidcReturnToBrowserPath(url: string): string | null {
  return rewriteRadishDeepLinkToBrowserPath(url) ?? rewriteOidcLoopbackToBrowserPath(url);
}

async function drainPendingDeepLinks(onDeepLink: (url: string) => void): Promise<void> {
  const invoke = getTauriInvoke();
  if (!invoke) {
    return;
  }

  const pendingUrls = await invoke<string[]>('take_pending_deep_links').catch(() => []);
  pendingUrls.forEach(onDeepLink);
}

async function registerDeepLinkListener(onDeepLink: (url: string) => void): Promise<void> {
  const listen = getTauriListen();
  if (!listen) {
    return;
  }

  await listen<TauriDeepLinkPayload>('radish-deep-link', (event) => {
    event.payload.urls?.forEach(onDeepLink);
  });

  await listen<TauriDeepLinkPayload>('radish-oidc-loopback', (event) => {
    event.payload.urls?.forEach(onDeepLink);
  });
}

async function probeTauriSpikeInfo(): Promise<void> {
  const invoke = getTauriInvoke();
  if (!invoke) {
    return;
  }

  await invoke<TauriSpikeInfo>('get_tauri_spike_info').catch(() => undefined);
}

export function initializeTauriBridge(options: InitializeTauriBridgeOptions): void {
  if (!isTauriRuntime()) {
    return;
  }

  void probeTauriSpikeInfo();
  void registerDeepLinkListener(options.onDeepLink);
  void drainPendingDeepLinks(options.onDeepLink);
}
