import { useMemo } from 'react';
import { useLocation, Link } from 'react-router-dom';
import { Breadcrumb, HomeOutlined } from '@radish/ui';
import type { BreadcrumbProps } from '@radish/ui';

/**
 * 路由到面包屑映射
 */
const routeBreadcrumbMap: Record<string, string> = {
  '/': '仪表盘',
  '/applications': '应用管理',
  '/products': '商品管理',
  '/orders': '订单管理',
  '/users': '用户管理',
  '/roles': '角色管理',
  '/hangfire': '定时任务',
  '/theme-test': '主题测试',
};

/**
 * 面包屑导航组件
 */
export function AppBreadcrumb() {
  const location = useLocation();

  const breadcrumbItems: BreadcrumbProps['items'] = useMemo(() => {
    const pathSegments = location.pathname.split('/').filter(Boolean);

    // 首页面包屑
    const items: BreadcrumbProps['items'] = [
      {
        title: (
          <Link to="/" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
            <HomeOutlined />
            <span>首页</span>
          </Link>
        ),
      },
    ];

    // 如果不是首页，添加当前页面的面包屑
    if (pathSegments.length > 0) {
      let currentPath = '';

      pathSegments.forEach((segment, index) => {
        currentPath += `/${segment}`;
        const breadcrumbName = routeBreadcrumbMap[currentPath];

        if (breadcrumbName) {
          const isLast = index === pathSegments.length - 1;

          items.push({
            title: isLast ? (
              // 最后一个面包屑不需要链接
              <span>{breadcrumbName}</span>
            ) : (
              <Link to={currentPath}>{breadcrumbName}</Link>
            ),
          });
        }
      });
    }

    return items;
  }, [location.pathname]);

  // 如果只有首页一个面包屑，不显示面包屑导航
  if (breadcrumbItems.length <= 1) {
    return null;
  }

  return (
    <div style={{ marginBottom: '16px' }}>
      <Breadcrumb items={breadcrumbItems} />
    </div>
  );
}

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