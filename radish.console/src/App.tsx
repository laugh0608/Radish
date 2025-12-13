import { useState, useEffect } from 'react';
import { AdminLayout } from './components/AdminLayout';
import { Dashboard } from './pages/Dashboard';
import { Applications } from './pages/Applications';
import { Login } from './pages/Login';
import { message } from '@radish/ui';
import './App.css';

type MenuItem = 'dashboard' | 'applications' | 'users' | 'roles' | 'service-status';

function App() {
  const [currentMenu, setCurrentMenu] = useState<MenuItem>('dashboard');
  const [user] = useState({ name: 'Admin', avatar: undefined });
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  useEffect(() => {
    // 检查是否有 token
    const token = localStorage.getItem('access_token');
    if (token) {
      setIsLoggedIn(true);
    }
  }, []);

  // 检查是否是 OIDC 回调页面
  // 统一通过 Gateway 访问，pathname 固定为 /console/callback
  const isOidcCallback = typeof window !== 'undefined' &&
    window.location.pathname === '/console/callback';

  // 如果是回调页面，显示回调处理组件
  if (isOidcCallback) {
    return <OidcCallback onSuccess={() => setIsLoggedIn(true)} />;
  }

  const handleMenuClick = (key: string) => {
    setCurrentMenu(key as MenuItem);
  };

  const handleUserMenuClick = (key: string) => {
    switch (key) {
      case 'logout':
        handleLogout();
        break;
      case 'profile':
        message.info('个人信息功能待实现');
        break;
      case 'settings':
        message.info('设置功能待实现');
        break;
    }
  };

  const handleLogout = () => {
    // 清理本地保存的 Token
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');

    // 使用 OIDC 标准的 endsession endpoint 清除 Auth Server 的会话
    // 统一通过 Gateway 访问，origin 就是 Gateway 地址
    const currentOrigin = window.location.origin;
    const postLogoutRedirectUri = `${currentOrigin}/console/`;

    const logoutUrl = new URL(`${currentOrigin}/connect/endsession`);
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
    logoutUrl.searchParams.set('client_id', 'radish-console');

    // 重定向到 OIDC logout endpoint，Auth Server 会清除 session 并重定向回来
    window.location.href = logoutUrl.toString();
  };

  const handleLoginSuccess = () => {
    setIsLoggedIn(true);
  };

  // 如果未登录，显示登录页面
  if (!isLoggedIn) {
    return <Login onLoginSuccess={handleLoginSuccess} />;
  }

  const renderContent = () => {
    switch (currentMenu) {
      case 'dashboard':
        return <Dashboard />;
      case 'applications':
        return <Applications />;
      case 'users':
        return (
          <div>
            <h2>用户管理</h2>
            <p>用户管理功能待实现</p>
          </div>
        );
      case 'roles':
        return (
          <div>
            <h2>角色管理</h2>
            <p>角色管理功能待实现</p>
          </div>
        );
      default:
        return <Dashboard />;
    }
  };

  return (
    <AdminLayout
      selectedKey={currentMenu}
      onMenuClick={handleMenuClick}
      user={user}
      onUserMenuClick={handleUserMenuClick}
    >
      {renderContent()}
    </AdminLayout>
  );
}

interface OidcCallbackProps {
  onSuccess: () => void;
}

function OidcCallback({ onSuccess }: OidcCallbackProps) {
  const [error, setError] = useState<string>();
  const [messageText, setMessageText] = useState<string>('正在完成登录...');

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const url = new URL(window.location.href);
    const code = url.searchParams.get('code');

    if (!code) {
      setError('授权码缺失');
      setMessageText('登录失败');
      return;
    }

    // 统一通过 Gateway 访问，origin 就是 Gateway 地址
    const currentOrigin = window.location.origin;
    const redirectUri = `${currentOrigin}/console/callback`;

    const fetchToken = async () => {
      const body = new URLSearchParams();
      body.set('grant_type', 'authorization_code');
      body.set('client_id', 'radish-console');
      body.set('code', code);
      body.set('redirect_uri', redirectUri);

      try {
        const response = await fetch(`${currentOrigin}/connect/token`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
          },
          body
        });

        if (!response.ok) {
          throw new Error(`Token 请求失败: ${response.status} ${response.statusText}`);
        }

        const tokenSet = await response.json() as {
          access_token?: string;
          refresh_token?: string;
        };

        if (!tokenSet.access_token) {
          throw new Error('未收到 access_token');
        }

        window.localStorage.setItem('access_token', tokenSet.access_token);
        if (tokenSet.refresh_token) {
          window.localStorage.setItem('refresh_token', tokenSet.refresh_token);
        }

        setMessageText('登录成功，正在跳转...');
        message.success('登录成功');

        // 通知父组件登录成功，并跳转到首页
        onSuccess();

        // 跳转到 Console 首页
        setTimeout(() => {
          window.location.replace('/console/');
        }, 500);
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setError(msg);
        setMessageText('登录失败');
        message.error('登录失败：' + msg);
      }
    };

    void fetchToken();
  }, [onSuccess]);

  return (
    <div style={{ padding: '40px', textAlign: 'center' }}>
      <h1>OIDC 回调处理</h1>
      <p>{messageText}</p>
      {error && <p style={{ color: 'red' }}>错误详情：{error}</p>}
    </div>
  );
}

export default App;
