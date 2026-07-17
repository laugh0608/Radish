import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Result, AntButton } from '@radish/ui';
import { HomeOutlined, SearchOutlined } from '@radish/ui';
import { GlobalSearch } from '../GlobalSearch';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';

/**
 * 404 页面组件
 */
export function NotFound() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchVisible, setSearchVisible] = useState(false);
  useDocumentTitle(t('console.notFound.title'));

  const handleBackHome = () => {
    navigate('/', { replace: true });
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  return (
    <>
      <div style={{
        padding: '48px',
        maxWidth: '600px',
        margin: '0 auto',
        minHeight: '60vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center'
      }}>
        <Result
          status="404"
          title={t('console.notFound.title')}
          subTitle={t('console.notFound.description')}
          extra={[
            <AntButton
              type="primary"
              key="home"
              icon={<HomeOutlined />}
              onClick={handleBackHome}
            >
              {t('console.notFound.home')}
            </AntButton>,
            <AntButton
              key="back"
              onClick={handleGoBack}
            >
              {t('console.notFound.back')}
            </AntButton>,
            <AntButton
              key="search"
              icon={<SearchOutlined />}
              onClick={() => setSearchVisible(true)}
            >
              {t('console.notFound.search')}
            </AntButton>,
          ]}
        />
      </div>
      <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
    </>
  );
}
