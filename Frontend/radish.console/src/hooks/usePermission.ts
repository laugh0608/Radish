import { useUser } from '../contexts/UserContext';
import type { UserInfo } from '../types/user';

export function hasPermission(user: UserInfo | null | undefined, permission?: string): boolean {
  if (!user || !permission) {
    return false;
  }

  const roles = user.roles || [];
  if (roles.includes('System') || roles.includes('Admin')) {
    return true;
  }

  const permissions = user.permissions || [];
  return permissions.includes(permission);
}

export function hasRole(user: UserInfo | null | undefined, roles: string | string[]): boolean {
  if (!user || !user.roles) {
    return false;
  }

  const requiredRoles = Array.isArray(roles) ? roles : [roles];
  return requiredRoles.some((role) => user.roles?.includes(role));
}

/**
 * 权限验证 Hook
 *
 * @param permission 权限标识（如 "User:Create"）
 * @returns 是否有权限
 *
 * @example
 * ```tsx
 * const canCreateUser = usePermission('User:Create');
 * if (canCreateUser) {
 *   // 显示创建按钮
 * }
 * ```
 */
export function usePermission(permission: string): boolean {
  const { user } = useUser();

  return hasPermission(user, permission);
}

/**
 * 角色验证 Hook
 *
 * @param roles 角色列表
 * @returns 是否拥有任一角色
 *
 * @example
 * ```tsx
 * const isAdmin = useRole(['Admin', 'System']);
 * ```
 */
export function useRole(roles: string | string[]): boolean {
  const { user } = useUser();

  return hasRole(user, roles);
}
