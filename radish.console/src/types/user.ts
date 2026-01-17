/**
 * 用户信息类型
 */
export interface UserInfo {
  userId: number;
  userName: string;
  email?: string;
  avatar?: string;
  roles?: string[];
  tenantId?: number;
}
