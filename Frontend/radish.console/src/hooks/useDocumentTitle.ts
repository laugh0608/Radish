import { useEffect } from 'react';
import { routeTitleMap } from '@/router/routeMeta';

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

    document.title = title ? `${title} - ${suffix}` : suffix;

    return () => {
      document.title = previousTitle;
    };
  }, [title, suffix]);
}

/**
 * 根据路径获取页面标题
 *
 * @param pathname 路由路径
 * @returns 页面标题
 */
export function getTitleFromPath(pathname: string): string {
  return routeTitleMap[pathname] || '页面';
}

export { routeTitleMap };
