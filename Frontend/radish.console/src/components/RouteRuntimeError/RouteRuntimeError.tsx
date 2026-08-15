import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AntButton, Result } from '@radish/ui';
import { useNavigate, useRouteError } from 'react-router';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useUser } from '@/hooks/useUser';
import { canEnterConsole, getDefaultAuthorizedPath } from '@/router/routeMeta';
import { log } from '@/utils/logger';

function createDiagnosticId(): string {
  const timestamp = Date.now().toString(36).toUpperCase();
  const randomPart = Math.random().toString(36).slice(2, 8).toUpperCase();
  return `CONSOLE-ROUTE-${timestamp}-${randomPart}`;
}

export function RouteRuntimeError() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const routeError = useRouteError();
  const { user } = useUser();
  const [diagnosticId] = useState(createDiagnosticId);
  useDocumentTitle(t('console.errorBoundary.title'));

  useEffect(() => {
    log.error('RouteRuntimeError', 'Console route runtime error', {
      diagnosticId,
      pathname: window.location.pathname,
      routeError,
    });
  }, [diagnosticId, routeError]);

  const fallbackPath = canEnterConsole(user) ? getDefaultAuthorizedPath(user) : '/login';

  return (
    <div
      className="console-route-state console-route-state--result console-route-state--runtime"
      role="alert"
    >
      <Result
        status="error"
        title={t('console.errorBoundary.title')}
        subTitle={t('console.errorBoundary.descriptionWithId', { diagnosticId })}
        extra={[
          <AntButton
            key="available"
            type="primary"
            onClick={() => navigate(fallbackPath, { replace: true })}
          >
            {t('console.errorBoundary.home')}
          </AntButton>,
          <AntButton key="reload" onClick={() => window.location.reload()}>
            {t('console.errorBoundary.reload')}
          </AntButton>,
        ]}
      />
    </div>
  );
}
