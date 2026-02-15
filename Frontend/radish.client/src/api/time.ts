import { apiGet, apiPost, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export interface TimeSettingsVo {
  voDefaultTimeZoneId: string;
  voDisplayFormat: string;
}

export interface UserTimePreferenceVo {
  voUserId: number;
  voTimeZoneId: string;
  voIsCustomized: boolean;
  voSystemDefaultTimeZoneId: string;
  voDisplayFormat: string;
  voModifyTime?: string | null;
}

/**
 * 获取后端系统时间配置
 */
export async function getTimeSettings(): Promise<TimeSettingsVo> {
  const response = await apiGet<TimeSettingsVo>('/api/v1/User/GetTimeSettings');

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取系统时间配置失败');
  }

  return response.data;
}

/**
 * 获取当前登录用户的时区偏好
 */
export async function getMyTimePreference(): Promise<UserTimePreferenceVo> {
  const response = await apiGet<UserTimePreferenceVo>('/api/v1/User/GetMyTimePreference', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户时区偏好失败');
  }

  return response.data;
}

/**
 * 更新当前登录用户的时区偏好
 */
export async function updateMyTimePreference(timeZoneId: string): Promise<UserTimePreferenceVo> {
  const response = await apiPost<UserTimePreferenceVo>(
    '/api/v1/User/UpdateMyTimePreference',
    { timeZoneId },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '更新用户时区偏好失败');
  }

  return response.data;
}
