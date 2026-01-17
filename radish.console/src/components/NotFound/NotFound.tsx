import { useNavigate } from 'react-router-dom';
import { Result, AntButton } from '@radish/ui';
import { HomeOutlined, SearchOutlined } from '@radish/ui';

/**
 * 404 页面组件
 */
export function NotFound() {
  const navigate = useNavigate();

  const handleBackHome = () => {
    navigate('/', { replace: true });
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  return (
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
        subTitle="抱歉，您访问的页面不存在。可能是链接错误或页面已被移除。"
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
            onClick={() => {
              // TODO: 实现全局搜索功能
              console.log('打开搜索功能');
            }}
          >
            搜索内容
          </AntButton>,
        ]}
      />
    </div>
  );
}