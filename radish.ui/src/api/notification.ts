import { apiFetch } from './client';
import type { ApiResponse } from './types';
import type { NotificationItemData } from '../components/Notification/Notification';

/**
 * 通知列表查询参数
 */
export interface NotificationListQuery {
  pageIndex: number;
  pageSize: number;
  type?: string;
}

/**
 * 未读数响应
 */
export interface UnreadCountResponse {
  userId: number;
  unreadCount: number;
  unreadCountByType?: Record<string, number>;
}

/**
 * 通知列表响应
 */
export interface NotificationListResponse {
  notifications: NotificationItemData[];
  total: number;
}

/**
 * 通知 API 客户端
 */
export const notificationApi = {
  /**
   * 获取通知列表
   */
  async getList(query: NotificationListQuery): Promise<NotificationListResponse> {
    const params = new URLSearchParams({
      pageIndex: query.pageIndex.toString(),
      pageSize: query.pageSize.toString(),
      ...(query.type && { type: query.type })
    });

    const response = await apiFetch(
      `/api/v1/Notification/GetNotificationList?${params.toString()}`,
      { withAuth: true }
    );

    const json = await response.json() as ApiResponse<NotificationListResponse>;
    return json.responseData!;
  },

  /**
   * 获取未读数量
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiFetch(
      '/api/v1/Notification/GetUnreadCount',
      { withAuth: true }
    );

    const json = await response.json() as ApiResponse<UnreadCountResponse>;
    return json.responseData!.unreadCount;
  },

  /**
   * 标记已读
   */
  async markAsRead(notificationIds: number[]): Promise<void> {
    await apiFetch('/api/v1/Notification/MarkAsRead', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ notificationIds }),
      withAuth: true
    });
  },

  /**
   * 标记全部已读
   */
  async markAllAsRead(): Promise<void> {
    await apiFetch('/api/v1/Notification/ReadAll', {
      method: 'PUT',
      withAuth: true
    });
  },

  /**
   * 删除通知
   */
  async deleteNotification(notificationId: number): Promise<void> {
    await apiFetch(`/api/v1/Notification/${notificationId}`, {
      method: 'DELETE',
      withAuth: true
    });
  }
};
