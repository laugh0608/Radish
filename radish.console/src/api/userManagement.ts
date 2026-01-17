import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';
import type { ParsedApiResponse } from '@radish/ui';
import type { UserListItem } from '../types/user';

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
 * 用户创建/更新参数
 */
export interface UserCreateParams {
  userName: string;
  email?: string;
  password?: string;
  roles?: string[];
  status?: UserStatus;
}

export interface UserUpdateParams {
  id: number;
  userName?: string;
  email?: string;
  roles?: string[];
  status?: UserStatus;
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

    return apiGet<UserListResponse>(url, { withAuth: true });
  },

  /**
   * 获取用户详情
   */
  async getUserById(id: number): Promise<ParsedApiResponse<UserListItem>> {
    return apiGet<UserListItem>(`/api/v1/User/GetUserById/${id}`, { withAuth: true });
  },

  /**
   * 创建用户
   */
  async createUser(params: UserCreateParams): Promise<ParsedApiResponse<UserListItem>> {
    return apiPost<UserListItem>('/api/v1/User/Create', params, { withAuth: true });
  },

  /**
   * 更新用户
   */
  async updateUser(params: UserUpdateParams): Promise<ParsedApiResponse<UserListItem>> {
    return apiPut<UserListItem>(`/api/v1/User/Update/${params.id}`, params, { withAuth: true });
  },

  /**
   * 删除用户
   */
  async deleteUser(id: number): Promise<ParsedApiResponse<void>> {
    return apiDelete<void>(`/api/v1/User/Delete/${id}`, { withAuth: true });
  },

  /**
   * 更新用户状态
   */
  async updateUserStatus(id: number, status: UserStatus): Promise<ParsedApiResponse<void>> {
    return apiPut<void>(`/api/v1/User/UpdateStatus/${id}`, { status }, { withAuth: true });
  },

  /**
   * 重置用户密码
   */
  async resetPassword(id: number, newPassword: string): Promise<ParsedApiResponse<void>> {
    return apiPost<void>(`/api/v1/User/ResetPassword/${id}`, { newPassword }, { withAuth: true });
  },

  /**
   * 分配角色
   */
  async assignRoles(id: number, roles: string[]): Promise<ParsedApiResponse<void>> {
    return apiPost<void>(`/api/v1/User/AssignRoles/${id}`, { roles }, { withAuth: true });
  },

  /**
   * 强制用户下线
   */
  async forceLogout(id: number): Promise<ParsedApiResponse<void>> {
    return apiPost<void>(`/api/v1/User/ForceLogout/${id}`, {}, { withAuth: true });
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