import type { ParsedApiResponse } from './types';

export function createLocalizedRequestHeaders(
  headers: HeadersInit | undefined,
  language: string | null | undefined
): Headers {
  const result = new Headers(headers);
  if (!result.has('Accept')) {
    result.set('Accept', 'application/json');
  }

  const normalizedLanguage = language?.trim();
  if (normalizedLanguage && !result.has('Accept-Language')) {
    result.set('Accept-Language', normalizedLanguage);
  }

  return result;
}

export function localizeParsedApiResponse<T>(
  parsed: ParsedApiResponse<T>,
  translateMessage: ((key: string, messageArguments?: readonly unknown[]) => string | undefined) | undefined
): ParsedApiResponse<T> {
  if (parsed.ok || !parsed.messageKey || !translateMessage) {
    return parsed;
  }

  const localized = translateMessage(parsed.messageKey, parsed.messageArguments);
  if (!localized || localized === parsed.messageKey) {
    return parsed;
  }

  return { ...parsed, message: localized };
}
