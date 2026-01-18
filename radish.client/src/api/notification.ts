/**
 * 通知系统相关的 API 调用
 */

import { apiGet, apiPost, apiPut, configureApiClient, type PagedResponse } from '@radish/ui';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

/**
 * 通知信息
 */
export interface Notification {
  VoId: number;
  VoUserId: number;
  VoTitle: string;
  VoContent: string;
  VoType: string;
  VoTypeDisplay: string;
  VoIsRead: boolean;
  VoRelatedId: number;
  VoRelatedType: string;
  VoRelatedUrl: string;
  VoIcon: string;
  VoColor: string;
  VoPriority: number;
  VoExpiresAt: string;
  VoReadAt: string;
  VoCreateTime: string;
  VoUpdateTime: string;
}

/**
 * 通知 API
 */
export const notificationApi = {
  /**
   * 获取我的通知列表
   */
  async getMyNotifications(params: {
    pageIndex?: number;
    pageSize?: number;
    isRead?: boolean;
  }): Promise<PagedResponse<Notification> | null> {
    const { pageIndex = 1, pageSize = 20, isRead } = params;

    let url = `/api/v1/Notification/GetMyNotifications?pageIndex=${pageIndex}&pageSize=${pageSize}`;
    if (isRead !== undefined) {
      url += `&isRead=${isRead}`;
    }

    const response = await apiGet<PagedResponse<Notification>>(url, {
      withAuth: true,
    });

    return response.ok ? response.data || null : null;
  },

  /**
   * 标记通知为已读
   */
  async markAsRead(notificationId: number): Promise<boolean> {
    const response = await apiPut<void>(`/api/v1/Notification/MarkAsRead/${notificationId}`, undefined, {
      withAuth: true,
    });

    return response.ok;
  },

  /**
   * 标记所有通知为已读
   */
  async markAllAsRead(): Promise<boolean> {
    const response = await apiPut<void>('/api/v1/Notification/MarkAllAsRead', undefined, {
      withAuth: true,
    });

    return response.ok;
  },

  /**
   * 获取未读通知数量
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiGet<{ VoUnreadCount: number }>('/api/v1/Notification/GetUnreadCount', {
      withAuth: true,
    });

    return response.ok && response.data ? response.data.VoUnreadCount : 0;
  },

  /**
   * 删除通知
   */
  async deleteNotification(notificationId: number): Promise<boolean> {
    const response = await apiPost<void>(`/api/v1/Notification/Delete/${notificationId}`, undefined, {
      withAuth: true,
    });

    return response.ok;
  },
};