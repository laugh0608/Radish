import type { TFunction } from 'i18next';

export function getLocalizedApiErrorMessage(
  error: unknown,
  t: TFunction,
  fallbackKey: string,
): string {
  if (!(error instanceof Error)) {
    return t(fallbackKey);
  }

  const message = error.message.trim();
  if (!message || message === fallbackKey) {
    return t(fallbackKey);
  }

  return message;
}
