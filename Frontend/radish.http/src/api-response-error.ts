import type { ParsedApiResponse } from './types';

export class ApiResponseError extends Error {
  readonly code?: string;
  readonly messageKey?: string;
  readonly messageArguments?: unknown[];
  readonly messageInfo?: string;
  readonly statusCode?: number;
  readonly httpStatus?: number;
  readonly traceId?: string;

  constructor(response: ParsedApiResponse<unknown>, fallbackMessage: string) {
    super(response.message || fallbackMessage);
    this.name = 'ApiResponseError';
    this.code = response.code;
    this.messageKey = response.messageKey;
    this.messageArguments = response.messageArguments;
    this.messageInfo = response.messageInfo;
    this.statusCode = response.statusCode;
    this.httpStatus = response.httpStatus;
    this.traceId = response.traceId;
  }
}

export function createApiResponseError<T>(
  response: ParsedApiResponse<T>,
  fallbackMessage: string
): ApiResponseError {
  return new ApiResponseError(response, fallbackMessage);
}

export function isApiResponseNotFoundError(error: unknown): boolean {
  if (!(error instanceof ApiResponseError)) {
    return false;
  }

  if ([404, 410].includes(error.httpStatus ?? 0) || [404, 410].includes(error.statusCode ?? 0)) {
    return true;
  }

  const code = error.code?.trim().toLowerCase();
  const messageKey = error.messageKey?.trim().toLowerCase();
  return code?.endsWith('not_found') === true || messageKey?.endsWith('not_found') === true;
}
