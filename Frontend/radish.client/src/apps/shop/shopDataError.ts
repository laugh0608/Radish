import {
  ApiResponseError,
  createApiResponseError,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';

export type ShopLoadScope =
  | 'categories'
  | 'products'
  | 'featured-products'
  | 'product-detail'
  | 'orders'
  | 'order-detail'
  | 'inventory';

export interface ShopLoadError {
  scope: ShopLoadScope;
  message: string;
  code?: string;
  messageKey?: string;
  statusCode?: number;
  httpStatus?: number;
  traceId?: string;
  target?: Record<string, string | number | boolean | null | undefined>;
}

export function createShopResponseError(
  response: ParsedApiResponse<unknown>,
  fallbackMessage: string,
): ApiResponseError {
  return createApiResponseError(
    response.messageKey ? { ...response, message: undefined } : response,
    fallbackMessage,
  );
}

export function createShopLoadError(
  scope: ShopLoadScope,
  error: unknown,
  t: TFunction,
  fallbackKey: string,
  target?: ShopLoadError['target'],
): ShopLoadError {
  const fallbackMessage = t(fallbackKey);
  const localizedMessage = error instanceof ApiResponseError && error.messageKey
    ? t(error.messageKey, { defaultValue: fallbackMessage })
    : fallbackMessage;

  return {
    scope,
    message: localizedMessage,
    ...(error instanceof ApiResponseError
      ? {
          code: error.code,
          messageKey: error.messageKey,
          statusCode: error.statusCode,
          httpStatus: error.httpStatus,
          traceId: error.traceId,
        }
      : {}),
    ...(target ? { target } : {}),
  };
}
