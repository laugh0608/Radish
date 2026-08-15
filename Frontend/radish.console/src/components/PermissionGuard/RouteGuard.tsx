import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { AntButton, Result } from '@radish/ui';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { ClientBackLink } from '@/components/ClientBackLink';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useUser } from '@/hooks/useUser';
import {
  canAccessConsoleRoute,
  getDefaultAuthorizedPath,
  type ConsoleRouteMeta,
} from '@/router/routeMeta';

interface RouteGuardProps {
  children: ReactNode;
  route: ConsoleRouteMeta;
}

interface RoutePermissionDeniedProps {
  fallbackPath: string;
  routeTitle: string;
}

function RoutePermissionDenied({ fallbackPath, routeTitle }: RoutePermissionDeniedProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  useDocumentTitle(t('console.guard.deniedDocumentTitle'));

  return (
    <div className="console-route-state console-route-state--result console-route-state--permission">
      <Result
        status="403"
        title={t('console.guard.deniedTitle', { title: routeTitle })}
        subTitle={t('console.guard.deniedDescription')}
        extra={[
          <AntButton
            key="available"
            type="primary"
            onClick={() => navigate(fallbackPath, { replace: true })}
          >
            {t('console.guard.openAvailable')}
          </AntButton>,
          <ClientBackLink key="client" />,
        ]}
      />
    </div>
  );
}

export function RouteGuard({ children, route }: RouteGuardProps) {
  const { t } = useTranslation();
  const location = useLocation();
  const { user, loading } = useUser();

  if (loading) {
    return (
      <div className="console-route-state console-route-state--loading" role="status">
        {t('console.guard.checking')}
      </div>
    );
  }

  if (!user) {
    return (
      <Navigate
        to="/login?auto=1"
        replace
        state={{
          returnLocation: {
            pathname: location.pathname,
            search: location.search,
            hash: location.hash,
          },
        }}
      />
    );
  }

  if (!canAccessConsoleRoute(route, user)) {
    const fallbackPath = getDefaultAuthorizedPath(user);
    const routeTitle = t(`console.route.${route.key}`, { defaultValue: route.title });
    return <RoutePermissionDenied fallbackPath={fallbackPath} routeTitle={routeTitle} />;
  }

  return <>{children}</>;
}
