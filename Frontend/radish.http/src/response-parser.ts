import type { ApiResponse, ParsedApiResponse } from './types';

/** 解析 API 响应体。 */
export function parseApiResponse<T>(response: ApiResponse<T>): ParsedApiResponse<T> {
  if (response.isSuccess) {
    return {
      ok: true,
      data: response.responseData,
      messageInfo: response.messageInfo,
      messageKey: response.messageKey,
      ...(response.messageArguments?.length
        ? { messageArguments: response.messageArguments }
        : {}),
      statusCode: response.statusCode,
      traceId: response.traceId,
    };
  }

  return {
    ok: false,
    message: response.messageInfo || '请求失败',
    messageInfo: response.messageInfo,
    messageKey: response.messageKey,
    ...(response.messageArguments?.length
      ? { messageArguments: response.messageArguments }
      : {}),
    code: response.code,
    statusCode: response.statusCode,
    traceId: response.traceId,
  };
}

/** 解析 API 响应体并优先使用客户端本地化文案。 */
export function parseApiResponseWithI18n<T>(
  response: ApiResponse<T>,
  t: (key: string, messageArguments?: readonly unknown[]) => string
): ParsedApiResponse<T> {
  const parsed = parseApiResponse(response);
  if (parsed.ok || !response.messageKey) {
    return parsed;
  }

  const localized = t(response.messageKey, response.messageArguments);
  if (localized && localized !== response.messageKey) {
    parsed.message = localized;
  }

  return parsed;
}

function isApiResponse<T>(value: unknown): value is ApiResponse<T> {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ApiResponse<T>).isSuccess === 'boolean'
  );
}

/** 同时保留真实 HTTP 状态与兼容响应体状态。 */
export async function parseHttpResponse<T>(response: Response): Promise<ParsedApiResponse<T>> {
  if (response.status === 204) {
    return { ok: true, statusCode: response.status, httpStatus: response.status };
  }

  const contentType = response.headers.get('content-type') || '';
  const isJson = contentType.includes('application/json') || contentType.includes('+json');

  if (!isJson) {
    const text = await response.text().catch(() => '');
    return {
      ok: false,
      message: text || `HTTP ${response.status} ${response.statusText}`,
      statusCode: response.status,
      httpStatus: response.status,
    };
  }

  try {
    const json = (await response.json()) as unknown;
    if (isApiResponse<T>(json)) {
      const parsed = parseApiResponse(json);
      return {
        ...parsed,
        statusCode: parsed.statusCode ?? response.status,
        httpStatus: response.status,
        traceId: parsed.traceId || response.headers.get('x-correlation-id') || undefined,
      };
    }

    return {
      ok: false,
      message: `HTTP ${response.status} ${response.statusText}`,
      statusCode: response.status,
      httpStatus: response.status,
    };
  } catch {
    return {
      ok: false,
      message: `HTTP ${response.status} ${response.statusText}`,
      statusCode: response.status,
      httpStatus: response.status,
    };
  }
}
