/**
 * 通知系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import {
  apiDelete,
  apiGet,
  apiPut,
  configureApiClient,
  createApiResponseError,
  type ParsedApiResponse,
  type NotificationCategory,
  type NotificationInboxMutationVo,
  type NotificationInboxPageVo,
  type NotificationInboxSummaryVo,
  type NotificationPreferenceVo,
  type PagedResponse,
  type UpdateNotificationPreferenceDto,
} from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type { LongId } from '@/api/user';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

function requireResponseData<T>(
  response: ParsedApiResponse<T>,
  fallbackMessage: string,
): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

/**
 * 通知详情 Vo（嵌套在 UserNotificationVo 中）
 */
export type NotificationBusinessId = LongId;
export type NotificationReferenceId = LongId;

export interface NotificationVo {
  voId: LongId;
  voType: string;
  voPriority: number;
  voTitle: string;
  voContent: string;
  voBusinessType: string | null;
  voBusinessId: NotificationBusinessId | null;
  voTriggerId: NotificationReferenceId | null;
  voTriggerName: string | null;
  voTriggerAvatar: string | null;
  voExtData: string | null;
  voCreateTime: string;
}

/**
 * 用户通知 Vo（后端实际返回的结构）
 */
export interface UserNotificationVo {
  voId: LongId;
  voUserId: LongId;
  voNotificationId: LongId;
  voIsRead: boolean;
  voReadAt: string | null;
  voCreateTime: string;
  voNotification: NotificationVo | null;
}

/**
 * 旧的扁平化通知接口（保留兼容性）
 * @deprecated 使用 UserNotificationVo 代替
 */
export interface Notification {
  voId: LongId;
  voUserId: LongId;
  voTitle: string;
  voContent: string;
  voType: string;
  voTypeDisplay: string;
  voIsRead: boolean;
  voRelatedId: LongId;
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
  unreadCount: string;
}

/**
 * 通知 API
 */
export const notificationApi = {
  async getInbox(params: {
    category?: NotificationCategory;
    onlyUnread?: boolean;
    cursor?: string;
    pageSize?: number;
  } = {}): Promise<NotificationInboxPageVo> {
    const search = new URLSearchParams();
    if (params.category) search.set('category', params.category);
    if (params.onlyUnread !== undefined) search.set('onlyUnread', String(params.onlyUnread));
    if (params.cursor) search.set('cursor', params.cursor);
    search.set('pageSize', String(params.pageSize ?? 20));
    const response = await apiGet<NotificationInboxPageVo>(
      `/api/v1/Notification/GetInbox?${search.toString()}`,
      { withAuth: true },
    );
    return requireResponseData(response, '加载通知收件箱失败');
  },

  async getInboxSummary(): Promise<NotificationInboxSummaryVo> {
    const response = await apiGet<NotificationInboxSummaryVo>(
      '/api/v1/Notification/GetInboxSummary',
      { withAuth: true },
    );
    return requireResponseData(response, '加载通知摘要失败');
  },

  async markInboxGroupsAsRead(groupIds: LongId[]): Promise<NotificationInboxMutationVo> {
    const response = await apiPut<NotificationInboxMutationVo>(
      '/api/v1/Notification/MarkInboxGroupsAsRead',
      { groupIds },
      { withAuth: true },
    );
    return requireResponseData(response, '标记通知分组已读失败');
  },

  async markAllInboxAsRead(category?: NotificationCategory): Promise<NotificationInboxMutationVo> {
    const response = await apiPut<NotificationInboxMutationVo>(
      '/api/v1/Notification/MarkAllAsRead',
      category ? { category } : {},
      { withAuth: true },
    );
    return requireResponseData(response, '标记分类通知已读失败');
  },

  async deleteInboxGroup(groupId: LongId): Promise<NotificationInboxMutationVo> {
    const response = await apiDelete<NotificationInboxMutationVo>(
      `/api/v1/Notification/DeleteInboxGroup/${encodeURIComponent(String(groupId))}`,
      { withAuth: true },
    );
    return requireResponseData(response, '删除通知分组失败');
  },

  async getPreferences(): Promise<NotificationPreferenceVo[]> {
    const response = await apiGet<NotificationPreferenceVo[]>(
      '/api/v1/Notification/GetPreferences',
      { withAuth: true },
    );
    return requireResponseData(response, '加载通知偏好失败');
  },

  async updatePreferences(
    preferences: UpdateNotificationPreferenceDto[],
  ): Promise<NotificationPreferenceVo[]> {
    const response = await apiPut<NotificationPreferenceVo[]>(
      '/api/v1/Notification/UpdatePreferences',
      { preferences },
      { withAuth: true },
    );
    return requireResponseData(response, '保存通知偏好失败');
  },

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
  async markAsRead(notificationId: LongId): Promise<boolean> {
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

    if (!response.ok || !response.data) {
      return 0;
    }

    const unreadCount = Number(response.data.unreadCount);
    return Number.isSafeInteger(unreadCount) && unreadCount >= 0 ? unreadCount : 0;
  },

  /**
   * 删除通知
   */
  async deleteNotification(notificationId: LongId): Promise<boolean> {
    const response = await apiDelete<void>(
      `/api/v1/Notification/DeleteNotification/${encodeURIComponent(String(notificationId))}`,
      { withAuth: true }
    );

    return response.ok;
  },
};
