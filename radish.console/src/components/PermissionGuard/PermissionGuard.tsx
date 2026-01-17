import type { ReactNode } from 'react';
import { usePermission, useRole } from '../../hooks/usePermission';

interface PermissionGuardProps {
  /**
   * 子组件
   */
  children: ReactNode;
  /**
   * 所需权限
   */
  permission?: string;
  /**
   * 所需角色
   */
  role?: string | string[];
  /**
   * 无权限时显示的内容
   */
  fallback?: ReactNode;
}

/**
 * 权限守卫组件
 *
 * 根据权限或角色控制子组件的显示
 *
 * @example
 * ```tsx
 * <PermissionGuard permission="User:Create">
 *   <Button>创建用户</Button>
 * </PermissionGuard>
 *
 * <PermissionGuard role={['Admin', 'System']}>
 *   <AdminPanel />
 * </PermissionGuard>
 * ```
 */
export function PermissionGuard({
  children,
  permission,
  role,
  fallback = null,
}: PermissionGuardProps) {
  const hasPermission = usePermission(permission || '');
  const hasRole = useRole(role || []);

  // 如果指定了权限，检查权限
  if (permission && !hasPermission) {
    return <>{fallback}</>;
  }

  // 如果指定了角色，检查角色
  if (role && !hasRole) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
