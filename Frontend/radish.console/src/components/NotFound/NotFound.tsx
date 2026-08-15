import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { Result, AntButton } from '@radish/ui';
import { HomeOutlined, SearchOutlined } from '@radish/ui';
import { ClientBackLink } from '../ClientBackLink';
import { GlobalSearch } from '../GlobalSearch';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useUser } from '@/hooks/useUser';
import { canEnterConsole, getDefaultAuthorizedPath } from '@/router/routeMeta';
import { ConsoleAccessDenied, RouteLoading } from '@/router/routerComponents';

/**
 * 404 页面组件
 */
export function NotFound() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useUser();
  const [searchVisible, setSearchVisible] = useState(false);
  useDocumentTitle(t('console.notFound.title'));

  const handleGoBack = () => {
    navigate(-1);
  };

  if (loading) {
    return <RouteLoading />;
  }

  if (user && !canEnterConsole(user)) {
    return <ConsoleAccessDenied />;
  }

  const authorized = canEnterConsole(user);
  const defaultPath = authorized ? getDefaultAuthorizedPath(user) : null;
  const actions = authorized
    ? [
        <AntButton
          type="primary"
          key="available"
          icon={<HomeOutlined />}
          onClick={() => navigate(defaultPath!, { replace: true })}
        >
          {t('console.notFound.availableHome')}
        </AntButton>,
        <AntButton key="back" onClick={handleGoBack}>
          {t('console.notFound.back')}
        </AntButton>,
        <AntButton
          key="search"
          icon={<SearchOutlined />}
          onClick={() => setSearchVisible(true)}
        >
          {t('console.notFound.search')}
        </AntButton>,
      ]
    : [
        <AntButton
          type="primary"
          key="login"
          onClick={() => navigate('/login', {
            replace: true,
            state: {
              returnLocation: {
                pathname: location.pathname,
                search: location.search,
                hash: location.hash,
              },
            },
          })}
        >
          {t('console.notFound.login')}
        </AntButton>,
        <ClientBackLink key="client" />,
      ];

  return (
    <>
      <div className="console-route-state console-route-state--result console-route-state--not-found">
        <Result
          status="404"
          title={t('console.notFound.title')}
          subTitle={authorized
            ? t('console.notFound.authorizedDescription')
            : t('console.notFound.anonymousDescription')}
          extra={actions}
        />
      </div>
      {authorized
        ? <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
        : null}
    </>
  );
}
