import { useUser } from '../contexts/UserContext';

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

  if (!user || !permission) {
    return false;
  }

  const roles = user.roles || [];
  if (roles.includes('System')) {
    return true;
  }

  const permissions = user.permissions || [];
  return permissions.includes(permission);
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

  if (!user || !user.roles) {
    return false;
  }

  const requiredRoles = Array.isArray(roles) ? roles : [roles];
  return requiredRoles.some(role => user.roles?.includes(role));
}
