/**
 * 用户相关的 API 调用
 * 直接使用后端 Vo 字段名，不做映射
 */

import { apiGet, configureApiClient } from '@radish/ui';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 用户提及选项（使用 vo 前缀，与后端 VO 保持一致）
 */
export interface UserMentionOption {
  voId: number | string;  // 后端返回long类型会被序列化为字符串
  voUserName: string;
  voDisplayName?: string | null;
  voAvatar?: string | null;
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
    throw new Error(response.message || '搜索用户失败');
  }

  // 直接返回后端数据，不做字段映射
  return response.data;
}
