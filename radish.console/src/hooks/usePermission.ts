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

  if (!user) {
    return false;
  }

  // TODO: 实现真正的权限验证逻辑
  // 当前简化实现：检查用户角色
  // 未来需要从后端获取用户的权限列表进行验证

  // 临时实现：System 和 Admin 角色拥有所有权限
  const roles = user.roles || [];
  if (roles.includes('System') || roles.includes('Admin')) {
    return true;
  }

  // 其他角色暂时没有权限
  return false;
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
