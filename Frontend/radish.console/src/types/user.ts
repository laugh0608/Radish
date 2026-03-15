/**
 * 用户信息类型（与后端CurrentUserVo对应）
 */
export interface UserInfo {
  voUserId: number;
  voUserName: string;
  voTenantId: number;
  roles?: string[];
  permissions?: string[];
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
}

/**
 * 用户列表项类型（与后端UserVo对应）
 */
export interface UserListItem {
  uuid: number;
  voLoginName: string;
  voUserName: string;
  voUserEmail: string;
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
  voIsEnable: boolean;
  voCreateTime: string;
  voUpdateTime?: string;
  voIsDeleted: boolean;
  voTenantId: number;
}
