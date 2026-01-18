/**
 * 用户相关的 API 调用
 */

import { parseApiResponseWithI18n, apiGet, configureApiClient, type ApiResponse } from '@radish/ui';
import type { TFunction } from 'i18next';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

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

  const response = await apiGet<UserMentionOption[]>(
    `/api/v1/User/SearchForMention?keyword=${encodeURIComponent(keyword)}&limit=${limit}`
  );

  if (!response.ok || !response.data) {
    // 使用 i18n 解析错误消息
    const errorResponse = response as any;
    if (errorResponse.messageKey) {
      const parsed = parseApiResponseWithI18n(errorResponse, t);
      throw new Error(parsed.message || '搜索用户失败');
    }
    throw new Error(response.message || '搜索用户失败');
  }

  return response.data;
}
