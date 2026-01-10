import { apiClient } from './client';
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
    const response = await apiClient.get<ApiResponse<NotificationListResponse>>(
      '/api/v1/Notification/GetNotificationList',
      { params: query }
    );
    return response.data.response;
  },

  /**
   * 获取未读数量
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiClient.get<ApiResponse<UnreadCountResponse>>(
      '/api/v1/Notification/GetUnreadCount'
    );
    return response.data.response.unreadCount;
  },

  /**
   * 标记已读
   */
  async markAsRead(notificationIds: number[]): Promise<void> {
    await apiClient.put('/api/v1/Notification/MarkAsRead', { notificationIds });
  },

  /**
   * 标记全部已读
   */
  async markAllAsRead(): Promise<void> {
    await apiClient.put('/api/v1/Notification/ReadAll');
  },

  /**
   * 删除通知
   */
  async deleteNotification(notificationId: number): Promise<void> {
    await apiClient.delete(`/api/v1/Notification/${notificationId}`);
  }
};
