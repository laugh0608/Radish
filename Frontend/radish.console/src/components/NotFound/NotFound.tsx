import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Result, AntButton } from '@radish/ui';
import { HomeOutlined, SearchOutlined } from '@radish/ui';
import { GlobalSearch } from '../GlobalSearch';

/**
 * 404 页面组件
 */
export function NotFound() {
  const navigate = useNavigate();
  const [searchVisible, setSearchVisible] = useState(false);

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
          title="页面未找到"
          subTitle="抱歉，您访问的页面不存在。可能是链接错误、权限入口已调整，或页面已被移除。"
          extra={[
            <AntButton
              type="primary"
              key="home"
              icon={<HomeOutlined />}
              onClick={handleBackHome}
            >
              返回首页
            </AntButton>,
            <AntButton
              key="back"
              onClick={handleGoBack}
            >
              返回上页
            </AntButton>,
            <AntButton
              key="search"
              icon={<SearchOutlined />}
              onClick={() => setSearchVisible(true)}
            >
              搜索菜单
            </AntButton>,
          ]}
        />
      </div>
      <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
    </>
  );
}
