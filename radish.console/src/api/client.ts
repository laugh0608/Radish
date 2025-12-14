const defaultApiBase = 'https://localhost:5000';

export interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

/**
 * API 客户端 - 统一封装 fetch 请求
 */
export async function apiFetch(
  input: RequestInfo | URL,
  options: ApiFetchOptions = {}
) {
  const { withAuth, headers, ...rest } = options;

  const apiBase = import.meta.env.VITE_API_BASE_URL || defaultApiBase;
  const url = typeof input === 'string' && !input.startsWith('http')
    ? `${apiBase}${input}`
    : input;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers,
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(url, {
    ...rest,
    headers: finalHeaders,
  });
}

/**
 * API 响应类型 - 匹配后端 MessageModel 结构
 */
export interface ApiResponse<T = any> {
  isSuccess: boolean;
  messageInfo?: string;
  responseData?: T;
  statusCode?: number;
  code?: string;
  messageKey?: string;
}

/**
 * 解析 API 响应
 */
export function parseApiResponse<T>(
  data: ApiResponse<T>
): { ok: boolean; data?: T; message?: string } {
  if (data.isSuccess) {
    return { ok: true, data: data.responseData };
  } else {
    return { ok: false, message: data.messageInfo || '请求失败' };
  }
}
