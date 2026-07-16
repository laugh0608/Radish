import type {
  ApiRequestOptions,
  ParsedApiResponse,
} from './types';
import { sanitizeLogValue } from './logSanitizer';
import { shouldRefreshToken, TokenRefreshErrorType, tryRefreshToken } from './token-refresh';
import { parseHttpResponse } from './response-parser';
import { createLocalizedRequestHeaders, localizeParsedApiResponse } from './i18n-contract';

export { parseApiResponse, parseApiResponseWithI18n, parseHttpResponse } from './response-parser';

/**
 * API 客户端配置
 */
export interface ApiClientConfig {
  /** 默认基础 URL */
  baseUrl: string;
  /** 默认超时时间（毫秒） */
  timeout: number;
  /** 获取 token 的函数 */
  getToken?: () => string | null;
  /** 获取当前请求语言；宿主负责返回服务端可识别的语言标签 */
  getLanguage?: () => string | null | undefined;
  /** 根据服务端 messageKey 解析用户可见消息 */
  translateMessage?: (key: string, messageArguments?: readonly unknown[]) => string | undefined;
  /** 请求拦截器 */
  onRequest?: (url: string, options: RequestInit) => void | Promise<void>;
  /** 响应拦截器 */
  onResponse?: (response: Response) => void;
  /** 错误拦截器 */
  onError?: (error: Error) => void;
}

/**
 * 默认配置
 */
const defaultConfig: ApiClientConfig = {
  baseUrl: '',
  timeout: 30000,
  getToken: () => {
    if (typeof window !== 'undefined') {
      return window.localStorage.getItem('access_token');
    }
    return null;
  },
};

/**
 * 当前配置（可以通过 configureApiClient 修改）
 */
let currentConfig: ApiClientConfig = { ...defaultConfig };

/**
 * 配置 API 客户端
 */
export function configureApiClient(config: Partial<ApiClientConfig>) {
  currentConfig = { ...currentConfig, ...config };
}

/**
 * 获取当前配置
 */
export function getApiClientConfig(): Readonly<ApiClientConfig> {
  return currentConfig;
}

/**
 * 统一的 API 请求函数
 */
export async function apiFetch(
  input: RequestInfo | URL,
  options: ApiRequestOptions = {}
): Promise<Response> {
  const {
    withAuth = false,
    baseUrl,
    timeout,
    headers,
    ...restOptions
  } = options;

  // 构建完整 URL
  const finalBaseUrl = baseUrl || currentConfig.baseUrl;
  const url =
    typeof input === 'string' && !input.startsWith('http')
      ? `${finalBaseUrl}${input}`
      : input;

  // 构建请求头
  const finalHeaders = createLocalizedRequestHeaders(headers, currentConfig.getLanguage?.());

  // 添加认证信息
  if (withAuth) {
    const token = currentConfig.getToken?.();
    if (token) {
      finalHeaders.set('Authorization', `Bearer ${token}`);
    }
  }
  const hasAuthorizationHeader = finalHeaders.has('Authorization');

  // 构建请求配置
  const fetchOptions: RequestInit = {
    ...restOptions,
    headers: finalHeaders,
  };

  // 请求拦截器
  await currentConfig.onRequest?.(url.toString(), fetchOptions);

  // 超时控制
  const timeoutMs = timeout || currentConfig.timeout;
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(url, {
      ...fetchOptions,
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    // 响应拦截器
    currentConfig.onResponse?.(response);

    // 检查是否需要刷新 token
    if (withAuth && hasAuthorizationHeader && shouldRefreshToken(response)) {
      try {
        // 刷新 token
        const newToken = await tryRefreshToken();

        // 使用新 token 重试请求
        const retryHeaders = new Headers(finalHeaders);
        retryHeaders.set('Authorization', `Bearer ${newToken}`);

        const retryResponse = await fetch(url, {
          ...fetchOptions,
          headers: retryHeaders,
        });

        // 响应拦截器
        currentConfig.onResponse?.(retryResponse);

        return retryResponse;
      } catch (refreshError) {
        // Refresh Token 确认失效时触发全局登出事件；网络或服务端错误保留原响应交给调用方处理。
        console.error('[API Client] Token refresh failed:', sanitizeLogValue(refreshError));
        const errorType = (refreshError as { errorType?: TokenRefreshErrorType }).errorType;
        if (errorType === TokenRefreshErrorType.InvalidRefreshToken) {
          const error = (refreshError as { error?: Error }).error;
          const reason = error?.message.includes('session_idle_expired')
            ? 'idle_session_expired'
            : undefined;
          window.dispatchEvent(new CustomEvent('auth:token-expired', {
            detail: { reason },
          }));
        }
        return response;
      }
    }

    return response;
  } catch (error) {
    clearTimeout(timeoutId);

    // 错误拦截器
    if (error instanceof Error) {
      currentConfig.onError?.(error);
    }

    throw error;
  }
}

async function parseConfiguredHttpResponse<T>(response: Response): Promise<ParsedApiResponse<T>> {
  const parsed = await parseHttpResponse<T>(response);
  return localizeParsedApiResponse(parsed, currentConfig.translateMessage);
}

function createJsonHeaders(headers?: HeadersInit): Headers {
  const result = new Headers(headers);
  if (!result.has('Content-Type')) {
    result.set('Content-Type', 'application/json');
  }
  return result;
}

/**
 * GET 请求的便捷方法
 */
export async function apiGet<T>(
  url: string,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    method: 'GET',
    ...options,
  });

  return await parseConfiguredHttpResponse<T>(response);
}

/**
 * POST 请求的便捷方法
 */
export async function apiPost<T>(
  url: string,
  data?: unknown,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    ...options,
    method: 'POST',
    headers: createJsonHeaders(options?.headers),
    body: data ? JSON.stringify(data) : undefined,
  });

  return await parseConfiguredHttpResponse<T>(response);
}

/**
 * PUT 请求的便捷方法
 */
export async function apiPut<T>(
  url: string,
  data?: unknown,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    ...options,
    method: 'PUT',
    headers: createJsonHeaders(options?.headers),
    body: data ? JSON.stringify(data) : undefined,
  });

  return await parseConfiguredHttpResponse<T>(response);
}

/**
 * DELETE 请求的便捷方法
 */
export async function apiDelete<T>(
  url: string,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    method: 'DELETE',
    ...options,
  });

  return await parseConfiguredHttpResponse<T>(response);
}
