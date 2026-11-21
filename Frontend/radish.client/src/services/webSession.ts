import type { WebSessionApi, WebSessionResponse } from '@radish/http';

export type WebSessionMode = 'pending' | 'legacy' | 'cookie';

const RENEW_BEFORE_MS = 30_000;
const ACTIVITY_INTERVAL_MS = 60_000;
const RETRY_MS = 5_000;

/** Browser credentials and CSRF proof belong to the current document only. */
export class WebSession {
  mode: WebSessionMode = 'pending';
  generation = 0;
  private state: 'unknown' | 'active' | 'signed-out' = 'unknown';
  private proof: string | null = null;
  private bearer: { value: string; expiresAt: number } | null = null;
  private retryAt = 0;
  private nextActivityAt = 0;
  private statusRequest: Promise<void> | null = null;
  private tokenRequest: Promise<string | null> | null = null;
  private logoutRequest: Promise<string | null> | null = null;
  private readonly api: WebSessionApi;
  private readonly onEnded: (publish: boolean) => void;
  private readonly now: () => number;

  constructor(api: WebSessionApi, onEnded: (publish: boolean) => void, now: () => number = Date.now) {
    this.api = api;
    this.onEnded = onEnded;
    this.now = now;
  }

  get signedOut(): boolean {
    return this.state === 'signed-out';
  }

  getAccessToken(): string | null {
    return this.bearer && this.bearer.expiresAt > this.now() ? this.bearer.value : null;
  }

  async resolveMode(): Promise<WebSessionMode> {
    if (this.mode === 'pending') await this.checkStatus();
    return this.mode;
  }

  getToken(options: { rejectedToken?: string; recheck?: boolean } = {}): Promise<string | null> {
    if (this.logoutRequest) return Promise.resolve(null);
    if (this.tokenRequest) return this.tokenRequest;

    const request = this.acquire(options).finally(() => {
      if (this.tokenRequest === request) this.tokenRequest = null;
    });
    this.tokenRequest = request;
    return request;
  }

  async login(returnPath: string): Promise<string | null> {
    if (await this.resolveMode() !== 'cookie') return null;

    try {
      const response = await this.api.login(returnPath);
      if (response.mode === 'cookie' && response.status === 200 && typeof response.data?.voAuthorizeUrl === 'string') {
        return response.data.voAuthorizeUrl;
      }

      this.deferRetry(response);
    } catch {
      this.deferRetry();
    }
    return null;
  }

  logout(): Promise<string | null> {
    if (this.logoutRequest) return this.logoutRequest;

    // A refresh started before logout must not repopulate this document.
    this.generation++;
    this.tokenRequest = null;
    const request = this.endSession().finally(() => {
      if (this.logoutRequest === request) this.logoutRequest = null;
    });
    this.logoutRequest = request;
    return request;
  }

  async recordActivity(): Promise<void> {
    if (this.mode !== 'cookie' || this.state !== 'active' || !this.proof || this.logoutRequest || this.now() < this.nextActivityAt) return;

    const generation = this.generation;
    this.nextActivityAt = this.now() + ACTIVITY_INTERVAL_MS;
    try {
      const response = await this.api.activity(this.proof);
      if (generation !== this.generation) return;
      if (response.mode === 'cookie' && response.status === 401) this.clear(true);
      if (response.status === 403) this.proof = null;
      if (response.status !== 204) this.nextActivityAt = this.now() + RETRY_MS;
    } catch {
      if (generation === this.generation) this.nextActivityAt = this.now() + RETRY_MS;
    }
  }

  forget(): void {
    this.generation++;
    this.bearer = null;
    this.proof = null;
    this.state = 'signed-out';
    this.nextActivityAt = 0;
    this.retryAt = 0;
  }

  receivedSignOut(): void {
    if (this.mode === 'cookie') this.clear(false);
  }

  private async acquire({ rejectedToken, recheck }: { rejectedToken?: string; recheck?: boolean }): Promise<string | null> {
    if (await this.resolveMode() !== 'cookie' || this.logoutRequest) return null;

    const cached = this.getAccessToken();
    if (this.now() < this.retryAt) return cached === rejectedToken ? null : cached;
    if (recheck) {
      await this.checkStatus();
    } else if (this.signedOut) {
      return null;
    }
    if (this.logoutRequest || this.signedOut) return null;

    const current = this.getAccessToken();
    if (current && current !== rejectedToken && this.bearer && this.bearer.expiresAt > this.now() + RENEW_BEFORE_MS) {
      return current;
    }

    if (!this.proof && !recheck && this.now() >= this.retryAt) await this.checkStatus();
    if (!this.proof || this.now() < this.retryAt || this.logoutRequest) return current === rejectedToken ? null : current;

    try {
      const response = await this.withProof((proof) => this.api.token(proof));
      if (!response || this.logoutRequest) return null;
      if (response.mode === 'cookie' && response.status === 401) {
        this.clear(true);
        return null;
      }

      const value = response.data?.voAccessToken;
      const expiresAt = Date.parse(response.data?.voExpiresAt ?? '');
      if (response.mode === 'cookie' && response.status === 200 && typeof value === 'string' && value && expiresAt > this.now()) {
        this.bearer = { value, expiresAt };
        this.state = 'active';
        if (value === rejectedToken) {
          this.deferRetry();
          return null;
        }
        return value;
      }

      this.deferRetry(response);
    } catch {
      this.deferRetry();
    }
    const remaining = this.getAccessToken();
    return remaining === rejectedToken ? null : remaining;
  }

  private async endSession(): Promise<string | null> {
    if (await this.resolveMode() !== 'cookie') return null;

    if (!this.proof) {
      await this.statusRequest;
      await this.checkStatus();
    }
    if (this.signedOut) return '/';
    if (!this.proof) return null;

    try {
      const result = await this.withProof((proof) => this.api.logout(proof));
      if (result?.mode === 'cookie' && result.status === 401) {
        this.clear(true);
        return '/';
      }
      if (result?.mode === 'cookie' && result.status === 200 && typeof result.data?.voEndSessionUrl === 'string') {
        this.clear(true);
        return result.data.voEndSessionUrl;
      }
      if (this.signedOut) return '/';
    } catch {
      // Keep the proof and bearer for the next explicit logout attempt.
    }
    return null;
  }

  private async withProof<T>(send: (proof: string) => Promise<WebSessionResponse<T>>): Promise<WebSessionResponse<T> | null> {
    if (!this.proof) return null;
    let generation = this.generation;
    let response = await send(this.proof);
    if (generation !== this.generation) return null;
    if (response.status === 403) {
      await this.checkStatus(true);
      // An expired or replaced session never retries a write with its old proof.
      if (!this.proof || this.signedOut || generation !== this.generation) return null;
      generation = this.generation;
      response = await send(this.proof);
    }
    return generation === this.generation ? response : null;
  }

  private checkStatus(ignoreBackoff = false): Promise<void> {
    if (this.statusRequest) return this.statusRequest;
    if (!ignoreBackoff && this.now() < this.retryAt) return Promise.resolve();

    const generation = this.generation;
    const request = this.api.status().then((result) => {
      if (generation !== this.generation) return;
      if (this.mode === 'pending' && result.mode === 'legacy' && result.status === 404) {
        this.mode = 'legacy';
        return;
      }
      if (result.mode === 'cookie') {
        this.mode = 'cookie';
        if (result.status === 401) {
          this.clear(true);
          return;
        }
        if (result.status === 200 && typeof result.data?.voCsrfToken === 'string' && result.data.voCsrfToken) {
          if (this.proof && this.proof !== result.data.voCsrfToken) {
            this.clear(false);
          }
          this.proof = result.data.voCsrfToken;
          this.state = 'active';
          this.retryAt = 0;
          return;
        }
      }
      this.deferRetry(result);
    }).catch(() => {
      if (generation === this.generation) this.deferRetry();
    }).finally(() => {
      if (this.statusRequest === request) this.statusRequest = null;
    });
    this.statusRequest = request;
    return request;
  }

  private clear(publish: boolean): void {
    const wasActive = this.state === 'active';
    this.forget();
    if (wasActive) this.onEnded(publish);
  }

  private deferRetry(response?: WebSessionResponse<unknown>): void {
    this.retryAt = this.now() + Math.min(60_000, Math.max(1000, response?.retryAfterMs ?? RETRY_MS));
  }
}
