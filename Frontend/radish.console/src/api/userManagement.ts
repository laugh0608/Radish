import { apiGet } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserListItem } from '../types/user';

function mapUserListItem(raw: any): UserListItem {
  return {
    uuid: raw.uuid ?? raw.Uuid ?? 0,
    voLoginName: raw.voLoginName ?? raw.VoLoginName ?? '',
    voUserName: raw.voUserName ?? raw.VoUserName ?? '',
    voUserEmail: raw.voUserEmail ?? raw.VoUserEmail ?? '',
    voAvatarUrl: raw.voAvatarUrl ?? raw.VoAvatarUrl,
    voAvatarThumbnailUrl: raw.voAvatarThumbnailUrl ?? raw.VoAvatarThumbnailUrl,
    voIsEnable: raw.voIsEnable ?? raw.VoIsEnable ?? false,
    voCreateTime: raw.voCreateTime ?? raw.VoCreateTime ?? '',
    voUpdateTime: raw.voUpdateTime ?? raw.VoUpdateTime,
    voIsDeleted: raw.voIsDeleted ?? raw.VoIsDeleted ?? false,
    voTenantId: raw.voTenantId ?? raw.VoTenantId ?? 0,
  };
}

/**
 * 用户状态枚举
 */
export enum UserStatus {
  Normal = 0,
  Disabled = 1,
  Locked = 2,
}

/**
 * 用户列表查询参数
 */
export interface UserListParams {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: UserStatus;
  role?: string;
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
    if (params.status !== undefined) {
      searchParams.set('status', params.status.toString());
    }
    if (params.role) {
      searchParams.set('role', params.role);
    }

    const queryString = searchParams.toString();
    const url = `/api/v1/User/GetUserList${queryString ? `?${queryString}` : ''}`;

    const response = await apiGet<any>(url, { withAuth: true });

    // 处理后端返回的 VoPagedResult 结构
    if (response.ok && response.data) {
      const backendData = response.data;
      const mappedData: UserListResponse = {
        items: (backendData.voItems || backendData.VoItems || []).map((item: any) => mapUserListItem(item)),
        total: backendData.voTotal || backendData.VoTotal || 0,
        pageIndex: backendData.voPageIndex || backendData.VoPageIndex || 1,
        pageSize: backendData.voPageSize || backendData.VoPageSize || 20,
      };

      return {
        ...response,
        data: mappedData
      };
    }

    return response as ParsedApiResponse<UserListResponse>;
  },

  /**
   * 获取用户详情
   */
  async getUserById(id: number): Promise<ParsedApiResponse<UserListItem>> {
    const response = await apiGet<any>(`/api/v1/User/GetUserById/${id}`, { withAuth: true });
    if (response.ok && response.data) {
      return {
        ...response,
        data: mapUserListItem(response.data),
      };
    }

    return response as ParsedApiResponse<UserListItem>;
  },

  /**
   * 获取用户统计信息
   */
  async getUserStats(userId?: number): Promise<ParsedApiResponse<UserStats>> {
    const url = userId
      ? `/api/v1/User/GetUserStats?userId=${userId}`
      : '/api/v1/User/GetUserStats';
    return apiGet<UserStats>(url, { withAuth: true });
  },
};

/**
 * 获取用户状态显示文本
 */
export function getUserStatusDisplay(status: UserStatus): string {
  switch (status) {
    case UserStatus.Normal:
      return '正常';
    case UserStatus.Disabled:
      return '禁用';
    case UserStatus.Locked:
      return '锁定';
    default:
      return '未知';
  }
}

/**
 * 获取用户状态颜色
 */
export function getUserStatusColor(status: UserStatus): string {
  switch (status) {
    case UserStatus.Normal:
      return 'success';
    case UserStatus.Disabled:
      return 'default';
    case UserStatus.Locked:
      return 'error';
    default:
      return 'default';
  }
}
