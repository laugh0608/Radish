/**
 * 通知系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, apiPost, apiPut, configureApiClient, type PagedResponse } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 通知详情 Vo（嵌套在 UserNotificationVo 中）
 */
export interface NotificationVo {
  voId: number;
  voType: string;
  voPriority: number;
  voTitle: string;
  voContent: string;
  voBusinessType: string | null;
  voBusinessId: number | null;
  voTriggerId: number | null;
  voTriggerName: string | null;
  voTriggerAvatar: string | null;
  voExtData: string | null;
  voCreateTime: string;
}

/**
 * 用户通知 Vo（后端实际返回的结构）
 */
export interface UserNotificationVo {
  voId: number;
  voUserId: number;
  voNotificationId: number;
  voIsRead: boolean;
  voReadAt: string | null;
  voDeliveryStatus: string;
  voDeliveredAt: string | null;
  voCreateTime: string;
  voNotification: NotificationVo | null;
}

/**
 * 旧的扁平化通知接口（保留兼容性）
 * @deprecated 使用 UserNotificationVo 代替
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
   * 返回 UserNotificationVo 结构，通知详情在 voNotification 字段中
   */
  async getMyNotifications(params: {
    pageIndex?: number;
    pageSize?: number;
    onlyUnread?: boolean;
    type?: string;
  }): Promise<PagedResponse<UserNotificationVo> | null> {
    const { pageIndex = 1, pageSize = 20, onlyUnread, type } = params;

    let url = `/api/v1/Notification/GetNotificationList?pageIndex=${pageIndex}&pageSize=${pageSize}`;
    if (onlyUnread !== undefined) {
      url += `&onlyUnread=${onlyUnread}`;
    }
    if (type) {
      url += `&type=${encodeURIComponent(type)}`;
    }

    const response = await apiGet<PagedResponse<UserNotificationVo>>(url, {
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
    const response = await apiPut<void>('/api/v1/Notification/MarkAsRead', {
      notificationIds: [notificationId]
    }, {
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
