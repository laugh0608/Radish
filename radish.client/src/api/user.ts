/**
 * 用户相关的 API 调用
 */

import { parseApiResponse, type ApiResponse } from '@/api/client';
import type { TFunction } from 'i18next';

const defaultApiBase = 'https://localhost:5000';

/**
 * 获取 API Base URL
 */
function getApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return (configured ?? defaultApiBase).replace(/\/$/, '');
}

/**
 * 带认证的 fetch 封装
 */
interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

/**
 * 用户提及选项（用于@提及功能）
 */
export interface UserMentionOption {
  id: string | number;  // 后端返回long类型会被序列化为字符串
  userName: string;
  displayName?: string | null;
  avatar?: string | null;
}

/**
 * 搜索用户（用于@提及功能）
 * @param keyword 搜索关键词
 * @param limit 返回结果数量限制（默认10）
 * @param t i18n 翻译函数
 * @returns 用户列表
 */
export async function searchUsersForMention(
  keyword: string,
  t: TFunction,
  limit: number = 10
): Promise<UserMentionOption[]> {
  if (!keyword.trim()) {
    return [];
  }

  const url = `${getApiBaseUrl()}/api/v1/User/SearchForMention?keyword=${encodeURIComponent(keyword)}&limit=${limit}`;
  const response = await apiFetch(url); // 不需要认证

  // 检查响应状态
  if (!response.ok) {
    throw new Error(`搜索用户失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UserMentionOption[]>;
  const parsed = parseApiResponse<UserMentionOption[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '搜索用户失败');
  }

  return parsed.data;
}
