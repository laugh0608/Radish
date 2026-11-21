export type HubName = 'notification' | 'chat' | 'comment';
export type HubCredential = 'web' | 'legacy' | 'anonymous';

export function selectHubCredential(name: HubName, mode: 'cookie' | 'legacy' | 'pending', authenticated: boolean): HubCredential | null {
  if (mode === 'legacy') return 'legacy';
  if (mode === 'cookie' && authenticated) return 'web';
  return name === 'comment' ? 'anonymous' : null;
}

const retryDelays = [1_000, 2_000, 5_000, 10_000, 30_000];

/** Fallback after SignalR ends automatic reconnection or receives a terminal close. */
export class WebHubReconnect {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private attempt = 0;

  schedule(isWanted: () => boolean, reconnect: () => void): void {
    if (this.timer !== null || !isWanted()) return;

    const delay = retryDelays[Math.min(this.attempt, retryDelays.length - 1)];
    this.attempt++;
    this.timer = setTimeout(() => {
      this.timer = null;
      if (isWanted()) reconnect();
    }, delay);
  }

  reset(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
    this.attempt = 0;
  }
}
