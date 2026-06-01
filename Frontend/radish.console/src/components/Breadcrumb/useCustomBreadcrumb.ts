import type { BreadcrumbProps } from '@radish/ui';

/**
 * 自定义面包屑 Hook
 *
 * 用于在特定页面自定义面包屑内容
 */
export function useCustomBreadcrumb(items: BreadcrumbProps['items']) {
  // 这个 Hook 可以用于在特定页面覆盖默认的面包屑
  // 暂时返回传入的 items，后续可以扩展更复杂的逻辑
  return items;
}
