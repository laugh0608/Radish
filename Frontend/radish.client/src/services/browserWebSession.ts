import { createWebSessionApi, type WebSessionApi } from '@radish/http';
import { getApiBaseUrl, getAuthBaseUrl } from '@/config/env';
import { isTauriRuntime } from '@/platform/tauriBridge';
import { dispatchAuthTokenExpired, TokenRefreshFailureReason } from './authSession';
import { WebSession } from './webSession';

const CHANNEL_NAME = 'radish-web-session';
let channel: BroadcastChannel | null = null;
let listening = false;

export function isSameOriginUrl(url: string): boolean {
  try {
    return new URL(url, window.location.origin).origin === window.location.origin;
  } catch {
    return false;
  }
}

export function canUseCookieSession(): boolean {
  if (typeof window === 'undefined' || window.location.protocol !== 'https:' || isTauriRuntime() ||
      window.Capacitor?.isNativePlatform?.()) return false;

  const path = window.location.pathname.replace(/\/+$/, '').toLowerCase();
  return path !== '/desktop' && path !== '/oidc/callback' && path !== '/oidc/logout-complete' &&
    isSameOriginUrl(getAuthBaseUrl()) && isSameOriginUrl(getApiBaseUrl());
}

function api(): WebSessionApi {
  return createWebSessionApi(window.location.origin);
}

const transport: WebSessionApi = {
  status: () => api().status(),
  token: (proof) => api().token(proof),
  activity: (proof) => api().activity(proof),
  login: (returnPath) => api().login(returnPath),
  logout: (proof) => api().logout(proof),
};

export const browserWebSession = new WebSession(transport, (publish) => {
  if (publish) channel?.postMessage('signed-out');
  if (canUseCookieSession()) dispatchAuthTokenExpired(TokenRefreshFailureReason.InvalidRefreshToken);
});

export function listenForWebSessionSignOut(): void {
  if (listening || !canUseCookieSession()) return;
  listening = true;
  if (typeof BroadcastChannel === 'undefined') return;

  channel = new BroadcastChannel(CHANNEL_NAME);
  channel.onmessage = (event) => {
    if (event.data === 'signed-out') browserWebSession.receivedSignOut();
  };
}
