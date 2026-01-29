/**
 * 通知系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, apiPost, apiPut, configureApiClient, type PagedResponse } from '@radish/ui';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

/**
 * 通知 Vo（直接使用后端字段名）
 */
export interface Notification {
  voId: number;
  voUserId: number;
  voTitle: string;
  voContent: string;
  voType: string;
  voTypeDisplay: string;
  voIsRead: boolean;
  voRelatedId: number;
  voRelatedType: string;
  voRelatedUrl: string;
  voIcon: string;
  voColor: string;
  voPriority: number;
  voExpiresAt: string;
  voReadAt: string;
  voCreateTime: string;
  voUpdateTime: string;
}

/**
 * 未读数量响应
 */
interface UnreadCountResponse {
  voUnreadCount: number;
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

    let url = `/api/v1/Notification/GetNotificationList?pageIndex=${pageIndex}&pageSize=${pageSize}`;
    if (isRead !== undefined) {
      url += `&isRead=${isRead}`;
    }

    const response = await apiGet<PagedResponse<Notification>>(url, {
      withAuth: true,
    });

    if (!response.ok || !response.data) {
      return null;
    }

    return response.data;
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
    const response = await apiGet<UnreadCountResponse>('/api/v1/Notification/GetUnreadCount', {
      withAuth: true,
    });

    return response.ok && response.data ? response.data.voUnreadCount : 0;
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