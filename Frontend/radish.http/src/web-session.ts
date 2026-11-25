import { apiFetch } from './client';

export interface WebSessionResponse<T = undefined> {
  status: number;
  mode: 'cookie' | 'legacy' | null;
  retryAfterMs: number | null;
  data?: T;
}

export interface WebSessionApi {
  status(): Promise<WebSessionResponse<{ voCsrfToken: string }>>;
  token(proof: string): Promise<WebSessionResponse<{ voAccessToken: string; voExpiresAt: string }>>;
  activity(proof: string): Promise<WebSessionResponse>;
  login(returnPath: string): Promise<WebSessionResponse<{ voAuthorizeUrl: string }>>;
  logout(proof: string): Promise<WebSessionResponse<{ voEndSessionUrl: string }>>;
}

export function createWebSessionApi(origin: string): WebSessionApi {
  async function send<T>(action: string, method: 'GET' | 'POST', proof?: string, returnPath?: string): Promise<WebSessionResponse<T>> {
    const headers = new Headers();
    if (proof) headers.set('X-Radish-Web-Session', proof);
    if (returnPath !== undefined) headers.set('Content-Type', 'application/json');
    const response = await apiFetch(new URL(`/web/session/${action}`, origin), {
      method,
      headers,
      body: returnPath === undefined ? undefined : JSON.stringify({ returnPath }),
      credentials: 'same-origin',
      redirect: 'error',
      cache: 'no-store',
      timeout: 8_000,
    });
    const mode = response.headers.get('X-Radish-Web-Session-Mode');
    const retry = response.headers.get('Retry-After');
    return {
      status: response.status,
      mode: mode === 'legacy' || mode === 'cookie' ? mode : null,
      retryAfterMs: retry && /^\d+$/.test(retry) ? Number(retry) * 1000 : null,
      data: response.status === 200 && response.headers.get('Content-Type')?.includes('application/json')
        ? await response.json() as T
        : undefined,
    };
  }

  return {
    status: () => send('status', 'GET'),
    token: (proof) => send('token', 'POST', proof),
    activity: (proof) => send('activity', 'POST', proof),
    login: (returnPath) => send('login', 'POST', undefined, returnPath),
    logout: (proof) => send('logout', 'POST', proof),
  };
}
