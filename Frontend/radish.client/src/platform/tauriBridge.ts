const RADISH_OIDC_CALLBACK_PATH = '/oidc/callback';

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

export function getOidcRedirectUri(): string {
  return isTauriRuntime() ? 'radish://oidc/callback' : `${window.location.origin}${RADISH_OIDC_CALLBACK_PATH}`;
}

export function getPostLogoutRedirectUri(): string {
  return isTauriRuntime() ? 'radish://oidc/logout-complete' : window.location.origin;
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
    return '/';
  }

  return null;
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
