import type {
  ApiResponse,
  ApiRequestOptions,
  ParsedApiResponse,
} from './types';

/**
 * API 客户端配置
 */
interface ApiClientConfig {
  /** 默认基础 URL */
  baseUrl: string;
  /** 默认超时时间（毫秒） */
  timeout: number;
  /** 获取 token 的函数 */
  getToken?: () => string | null;
  /** 请求拦截器 */
  onRequest?: (url: string, options: RequestInit) => void;
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
  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers,
  };

  // 添加认证信息
  if (withAuth) {
    const token = currentConfig.getToken?.();
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  // 构建请求配置
  const fetchOptions: RequestInit = {
    ...restOptions,
    headers: finalHeaders,
  };

  // 请求拦截器
  currentConfig.onRequest?.(url.toString(), fetchOptions);

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

/**
 * 解析 API 响应
 */
export function parseApiResponse<T>(
  response: ApiResponse<T>
): ParsedApiResponse<T> {
  if (response.isSuccess) {
    return {
      ok: true,
      data: response.responseData,
      statusCode: response.statusCode,
    };
  } else {
    return {
      ok: false,
      message: response.messageInfo || '请求失败',
      code: response.code,
      statusCode: response.statusCode,
    };
  }
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

  const json = (await response.json()) as ApiResponse<T>;
  return parseApiResponse(json);
}

/**
 * POST 请求的便捷方法
 */
export async function apiPost<T>(
  url: string,
  data?: any,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    body: data ? JSON.stringify(data) : undefined,
    ...options,
  });

  const json = (await response.json()) as ApiResponse<T>;
  return parseApiResponse(json);
}

/**
 * PUT 请求的便捷方法
 */
export async function apiPut<T>(
  url: string,
  data?: any,
  options?: ApiRequestOptions
): Promise<ParsedApiResponse<T>> {
  const response = await apiFetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    body: data ? JSON.stringify(data) : undefined,
    ...options,
  });

  const json = (await response.json()) as ApiResponse<T>;
  return parseApiResponse(json);
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

  const json = (await response.json()) as ApiResponse<T>;
  return parseApiResponse(json);
}
