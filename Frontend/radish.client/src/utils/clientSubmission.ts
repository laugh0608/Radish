export interface ClientSubmissionState {
  fingerprint: string;
  clientSubmissionId: string;
}

export function createClientSubmissionState(
  current: ClientSubmissionState | null,
  prefix: string,
  fingerprint: string
): ClientSubmissionState {
  if (current && current.fingerprint === fingerprint) {
    return current;
  }

  return {
    fingerprint,
    clientSubmissionId: `${prefix}:${createSubmissionId()}`
  };
}

function createSubmissionId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}
