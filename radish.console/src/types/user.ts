/**
 * 用户信息类型（与后端UserVo对应）
 */
export interface UserInfo {
  userId: number;
  userName: string;
  email?: string;
  avatarUrl?: string;
  roles?: string[];
  tenantId?: number;
}

/**
 * 用户列表项类型（与后端UserVo对应）
 */
export interface UserListItem {
  uuid: number;
  voLoginName: string;
  voUserName: string;
  voUserEmail: string;
  voIsEnable: boolean;
  voCreateTime: string;
  voUpdateTime?: string;
  voIsDeleted: boolean;
  voTenantId: number;
}
