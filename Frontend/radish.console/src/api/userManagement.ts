import { apiGet } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserListItem } from '../types/user';

type ApiRecord = Record<string, unknown>;

function isApiRecord(value: unknown): value is ApiRecord {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function toNumber(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return 0;
}

function toStringValue(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

function toIdString(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value);
  }

  return '';
}

function toOptionalString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function toBoolean(value: unknown): boolean {
  return typeof value === 'boolean' ? value : false;
}

function toStringArray(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
    : [];
}

function toParsedResponse<T>(response: ParsedApiResponse<unknown>, data?: T): ParsedApiResponse<T> {
  return {
    ok: response.ok,
    data,
    message: response.message,
    code: response.code,
    statusCode: response.statusCode,
  };
}

function mapUserListItem(raw: ApiRecord): UserListItem {
  return {
    uuid: toIdString(raw.uuid ?? raw.Uuid),
    voPublicId: toOptionalString(raw.voPublicId ?? raw.VoPublicId),
    voPublicIndex: toIdString(raw.voPublicIndex ?? raw.VoPublicIndex),
    voDisplayName: toOptionalString(raw.voDisplayName ?? raw.VoDisplayName),
    voDisplayHandle: toOptionalString(raw.voDisplayHandle ?? raw.VoDisplayHandle),
    voUserName: toStringValue(raw.voUserName ?? raw.VoUserName),
    voUserEmail: toStringValue(raw.voUserEmail ?? raw.VoUserEmail),
    voAvatarUrl: toOptionalString(raw.voAvatarUrl ?? raw.VoAvatarUrl),
    voAvatarThumbnailUrl: toOptionalString(raw.voAvatarThumbnailUrl ?? raw.VoAvatarThumbnailUrl),
    voIsEnable: toBoolean(raw.voIsEnable ?? raw.VoIsEnable),
    voCreateTime: toStringValue(raw.voCreateTime ?? raw.VoCreateTime),
    voUpdateTime: toOptionalString(raw.voUpdateTime ?? raw.VoUpdateTime),
    voIsDeleted: toBoolean(raw.voIsDeleted ?? raw.VoIsDeleted),
    voTenantId: toIdString(raw.voTenantId ?? raw.VoTenantId),
    voRoleNames: toStringArray(raw.voRoleNames ?? raw.VoRoleNames),
  };
}

/**
 * 用户列表查询参数
 */
export interface UserListParams {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  isEnabled?: boolean;
  roleName?: string;
}

/**
 * 用户列表响应（与后端返回结构对应）
 */
export interface UserListResponse {
  items: UserListItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

/**
 * 用户统计信息
 */
export interface UserStats {
  totalUsers: number;
  activeUsers: number;
  newUsersToday: number;
  onlineUsers: number;
}

export interface ConsoleUserAuthorization {
  voUserId: string;
  voRoleNames: string[];
  voPermissionKeys: string[];
}

/**
 * 用户管理 API
 */
export const userManagementApi = {
  /**
   * 获取用户列表
   */
  async getUserList(params: UserListParams = {}): Promise<ParsedApiResponse<UserListResponse>> {
    const searchParams = new URLSearchParams();

    if (params.pageIndex !== undefined) {
      searchParams.set('pageIndex', params.pageIndex.toString());
    }
    if (params.pageSize !== undefined) {
      searchParams.set('pageSize', params.pageSize.toString());
    }
    if (params.keyword) {
      searchParams.set('keyword', params.keyword);
    }
    if (params.isEnabled !== undefined) {
      searchParams.set('isEnabled', params.isEnabled.toString());
    }
    if (params.roleName) {
      searchParams.set('roleName', params.roleName);
    }

    const queryString = searchParams.toString();
    const url = `/api/v1/User/GetUserList${queryString ? `?${queryString}` : ''}`;

    const response = await apiGet<ApiRecord>(url, { withAuth: true });

    // 处理后端返回的 VoPagedResult 结构
    if (response.ok && isApiRecord(response.data)) {
      const backendData = response.data;
      const rawItems = Array.isArray(backendData.voItems)
        ? backendData.voItems
        : Array.isArray(backendData.VoItems)
          ? backendData.VoItems
          : [];

      const mappedData: UserListResponse = {
        items: rawItems.map((item) => mapUserListItem(isApiRecord(item) ? item : {})),
        total: toNumber(backendData.voTotal ?? backendData.VoTotal),
        pageIndex: toNumber(backendData.voPageIndex ?? backendData.VoPageIndex) || 1,
        pageSize: toNumber(backendData.voPageSize ?? backendData.VoPageSize) || 20,
      };

      return toParsedResponse(response, mappedData);
    }

    return toParsedResponse(response);
  },

  /**
   * 获取用户详情
   */
  async getUserById(id: string): Promise<ParsedApiResponse<UserListItem>> {
    const response = await apiGet<ApiRecord | ApiRecord[]>(
      `/api/v1/User/GetUserById/${encodeURIComponent(String(id))}`,
      { withAuth: true }
    );
    if (response.ok && response.data) {
      const rawUser = Array.isArray(response.data) ? response.data[0] : response.data;
      if (!isApiRecord(rawUser)) {
        return toParsedResponse(response);
      }

      return toParsedResponse(response, mapUserListItem(rawUser));
    }

    return toParsedResponse(response);
  },

  async getUserAuthorization(id: string): Promise<ParsedApiResponse<ConsoleUserAuthorization>> {
    const response = await apiGet<ApiRecord>(
      `/api/v1/User/GetUserAuthorization/${encodeURIComponent(String(id))}`,
      { withAuth: true },
    );
    if (!response.ok || !isApiRecord(response.data)) {
      return toParsedResponse(response);
    }

    const raw = response.data;
    return toParsedResponse(response, {
      voUserId: toIdString(raw.voUserId ?? raw.VoUserId),
      voRoleNames: toStringArray(raw.voRoleNames ?? raw.VoRoleNames),
      voPermissionKeys: toStringArray(raw.voPermissionKeys ?? raw.VoPermissionKeys),
    });
  },

  /**
   * 获取用户统计信息
   */
  async getUserStats(userId?: string): Promise<ParsedApiResponse<UserStats>> {
    const url = userId
      ? `/api/v1/User/GetUserStats?userId=${encodeURIComponent(userId)}`
      : '/api/v1/User/GetUserStats';
    return apiGet<UserStats>(url, { withAuth: true });
  },
};
