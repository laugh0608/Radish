/**
 * 用户信息类型（与后端CurrentUserVo对应）
 */
export interface UserInfo {
  voUserId: string;
  voDisplayName?: string;
  voDisplayHandle?: string;
  voUserName: string;
  voTenantId: string;
  roles?: string[];
  permissions?: string[];
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
}

export type WebSessionRevocation = 'Completed' | 'Pending' | 'NeedsAttention';

/**
 * 用户列表项类型（与后端UserVo对应）
 */
export interface UserListItem {
  uuid: string;
  voPublicId?: string;
  voPublicIndex?: string;
  voDisplayName?: string;
  voDisplayHandle?: string;
  voUserName: string;
  voUserEmail: string;
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
  voIsEnable: boolean;
  voWebSessionRevocation?: WebSessionRevocation;
  voCreateTime: string;
  voUpdateTime?: string;
  voIsDeleted: boolean;
  voTenantId: string;
  voRoleNames: string[];
}
