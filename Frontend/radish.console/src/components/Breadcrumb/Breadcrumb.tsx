import { useMemo } from 'react';
import { useLocation, Link } from 'react-router-dom';
import { Breadcrumb, HomeOutlined } from '@radish/ui';
import type { BreadcrumbProps } from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { getConsoleBreadcrumbRoutes, getConsoleRouteTitle } from '@/router/routeMeta';

/**
 * 面包屑导航组件
 */
export function AppBreadcrumb() {
  const location = useLocation();
  const { t } = useTranslation();

  const breadcrumbItems: BreadcrumbProps['items'] = useMemo(() => {
    // 首页面包屑
    const items: BreadcrumbProps['items'] = [
      {
        title: (
          <Link to="/" className="admin-breadcrumb-home">
            <HomeOutlined />
            <span>{t('console.breadcrumb.home')}</span>
          </Link>
        ),
      },
    ];

    const routes = getConsoleBreadcrumbRoutes(location.pathname);
    routes.forEach((route, index) => {
      const isLast = index === routes.length - 1;
      const breadcrumbName = getConsoleRouteTitle(
        route.path.includes(':') ? location.pathname : route.path,
        t,
      );

      items.push({
        title: isLast ? (
          <span>{breadcrumbName}</span>
        ) : (
          <Link to={route.path}>{breadcrumbName}</Link>
        ),
      });
    });

    return items;
  }, [location.pathname, t]);

  // 如果只有首页一个面包屑，不显示面包屑导航
  if (breadcrumbItems.length <= 1) {
    return null;
  }

  return (
    <div className="admin-breadcrumb">
      <Breadcrumb items={breadcrumbItems} />
    </div>
  );
}
