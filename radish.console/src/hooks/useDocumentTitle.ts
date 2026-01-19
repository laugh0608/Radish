import { useEffect } from 'react';

/**
 * 页面标题管理 Hook
 *
 * 根据路由动态设置页面标题
 *
 * @param title 页面标题
 * @param suffix 标题后缀，默认为 "Radish Console"
 *
 * @example
 * ```tsx
 * // 在页面组件中使用
 * function UserList() {
 *   useDocumentTitle('用户管理');
 *   // 页面标题将显示为：用户管理 - Radish Console
 *   return <div>...</div>;
 * }
 * ```
 */
export function useDocumentTitle(title: string, suffix = 'Radish Console') {
  useEffect(() => {
    const previousTitle = document.title;

    // 设置新标题
    document.title = title ? `${title} - ${suffix}` : suffix;

    // 组件卸载时恢复之前的标题
    return () => {
      document.title = previousTitle;
    };
  }, [title, suffix]);
}

/**
 * 路由到标题的映射
 */
export const routeTitleMap: Record<string, string> = {
  '/': '仪表盘',
  '/applications': '应用管理',
  '/products': '商品管理',
  '/orders': '订单管理',
  '/users': '用户管理',
  '/roles': '角色管理',
  '/hangfire': '定时任务',
  '/theme-test': '主题测试',
  '/login': '登录',
  '/callback': 'OIDC 回调',
};

/**
 * 根据路径获取页面标题
 *
 * @param pathname 路由路径
 * @returns 页面标题
 */
export function getTitleFromPath(pathname: string): string {
  return routeTitleMap[pathname] || '页面';
}
