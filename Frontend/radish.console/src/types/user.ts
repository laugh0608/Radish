/**
 * 用户信息类型（与后端CurrentUserVo对应）
 */
export interface UserInfo {
  voUserId: string;
  voUserName: string;
  voTenantId: string;
  roles?: string[];
  permissions?: string[];
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
}

/**
 * 用户列表项类型（与后端UserVo对应）
 */
export interface UserListItem {
  uuid: string;
  voPublicId?: string;
  voPublicIndex?: string;
  voDisplayName?: string;
  voDisplayHandle?: string;
  voLoginName: string;
  voUserName: string;
  voUserRealName: string;
  voUserEmail: string;
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
  voIsEnable: boolean;
  voCreateTime: string;
  voUpdateTime?: string;
  voIsDeleted: boolean;
  voTenantId: string;
}
