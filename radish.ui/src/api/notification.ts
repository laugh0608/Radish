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

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || `获取通知列表失败（HTTP ${response.status}）`);
    }

    const json = await response.json() as ApiResponse<{
      page: number;
      dataCount: number;
      pageCount: number;
      data: Array<{
        id: number;
        userId: number;
        notificationId: number;
        isRead: boolean;
        readAt?: string;
        deliveryStatus: string;
        deliveredAt?: string;
        createTime: string;
        notification?: {
          id: number;
          type: string;
          priority: number;
          title: string;
          content: string;
          businessType?: string;
          businessId?: number;
          triggerId?: number;
          triggerName?: string;
          triggerAvatar?: string;
          extData?: string;
          createTime: string;
        };
      }>;
    }>;

    // 将后端的 PageModel<UserNotificationVo> 转换为前端期望的格式
    const pageModel = json.responseData!;
    const notifications: NotificationItemData[] = pageModel.data.map(userNotif => {
      const notif = userNotif.notification;
      return {
        id: userNotif.notificationId,
        type: notif?.type || 'System',
        title: notif?.title || '',
        content: notif?.content || '',
        priority: notif?.priority || 1,
        businessType: notif?.businessType,
        businessId: notif?.businessId,
        triggerId: notif?.triggerId,
        triggerName: notif?.triggerName,
        triggerAvatar: notif?.triggerAvatar,
        isRead: userNotif.isRead,
        createdAt: notif?.createTime || userNotif.createTime
      };
    });

    return {
      notifications,
      total: pageModel.dataCount
    };
  },

  /**
   * 获取未读数量
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiFetch(
      '/api/v1/Notification/GetUnreadCount',
      { withAuth: true }
    );

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || `获取未读数量失败（HTTP ${response.status}）`);
    }

    const json = await response.json() as ApiResponse<UnreadCountResponse>;
    return json.responseData!.unreadCount;
  },

  /**
   * 标记已读
   */
  async markAsRead(notificationIds: number[]): Promise<void> {
    const response = await apiFetch('/api/v1/Notification/MarkAsRead', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ notificationIds }),
      withAuth: true
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || `标记已读失败（HTTP ${response.status}）`);
    }
  },

  /**
   * 标记全部已读
   */
  async markAllAsRead(): Promise<void> {
    const response = await apiFetch('/api/v1/Notification/MarkAllAsRead', {
      method: 'PUT',
      withAuth: true
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || `标记全部已读失败（HTTP ${response.status}）`);
    }
  },

  /**
   * 删除通知
   */
  async deleteNotification(notificationId: number): Promise<void> {
    const response = await apiFetch(`/api/v1/Notification/DeleteNotification/${notificationId}`, {
      method: 'DELETE',
      withAuth: true
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || `删除通知失败（HTTP ${response.status}）`);
    }
  }
};
