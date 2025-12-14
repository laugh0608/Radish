import { useState, useEffect, useRef } from 'react';
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
  // - 通过 Gateway 访问时：路径为 /console/callback（Gateway 保留完整路径）
  // - 直接访问开发服务器时：路径为 /console/callback（base: '/console/'）
  const isOidcCallback = typeof window !== 'undefined' &&
    window.location.pathname.endsWith('/callback');

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

  /**
   * 获取 Auth Server 的基础 URL
   * - 通过 Gateway 访问时（https://localhost:5000）：使用 Gateway 地址
   * - 直接访问开发服务器时（http://localhost:3002）：使用 Auth Server 直接地址
   */
  const getAuthServerBaseUrl = (): string => {
    const currentOrigin = window.location.origin;

    // 通过 Gateway 访问（生产环境或开发时通过 Gateway）
    if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
      return currentOrigin;
    }

    // 直接访问 console 开发服务器（开发环境）
    if (currentOrigin === 'http://localhost:3002' || currentOrigin === 'https://localhost:3002') {
      return 'http://localhost:5200'; // Auth Server 直接地址
    }

    // 默认使用 Gateway（生产环境）
    return currentOrigin;
  };

  /**
   * 获取 post_logout_redirect_uri
   * - 通过 Gateway 访问时：https://localhost:5000/console/
   * - 直接访问开发服务器时：http://localhost:3002/
   */
  const getPostLogoutRedirectUri = (): string => {
    const currentOrigin = window.location.origin;

    // 通过 Gateway 访问
    if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
      return `${currentOrigin}/console/`;
    }

    // 直接访问开发服务器
    return `${currentOrigin}/`;
  };

  const handleLogout = () => {
    // 清理本地保存的 Token
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');

    // 使用 OIDC 标准的 endsession endpoint 实现 Single Sign-Out
    const postLogoutRedirectUri = getPostLogoutRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
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
  const hasProcessed = useRef(false);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    // 防止重复执行（React StrictMode 会导致 useEffect 执行两次）
    if (hasProcessed.current) {
      return;
    }
    hasProcessed.current = true;

    const url = new URL(window.location.href);
    const code = url.searchParams.get('code');

    if (!code) {
      setError('授权码缺失');
      setMessageText('登录失败');
      return;
    }

    /**
     * 获取 Auth Server 的基础 URL
     */
    const getAuthServerBaseUrl = (): string => {
      const currentOrigin = window.location.origin;

      // 通过 Gateway 访问
      if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
        return currentOrigin;
      }

      // 直接访问 console 开发服务器
      if (currentOrigin === 'http://localhost:3002' || currentOrigin === 'https://localhost:3002') {
        return 'http://localhost:5200';
      }

      return currentOrigin;
    };

    /**
     * 获取 redirect_uri
     * - 通过 Gateway 访问时：https://localhost:5000/console/callback
     * - 直接访问开发服务器时：http://localhost:3002/console/callback
     * 注意：由于 Vite base 是 /console/，所以两种方式都需要 /console/ 前缀
     */
    const getRedirectUri = (): string => {
      const currentOrigin = window.location.origin;

      // 通过 Gateway 访问
      if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
        return `${currentOrigin}/console/callback`;
      }

      // 直接访问开发服务器（端口 3002，也需要 /console/ 前缀）
      return `${currentOrigin}/console/callback`;
    };

    const redirectUri = getRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const fetchToken = async () => {
      const body = new URLSearchParams();
      body.set('grant_type', 'authorization_code');
      body.set('client_id', 'radish-console');
      body.set('code', code);
      body.set('redirect_uri', redirectUri);

      try {
        const response = await fetch(`${authServerBaseUrl}/connect/token`, {
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
        // 通过 Gateway 访问时跳转到 /console/，直接访问时跳转到 /
        const currentOrigin = window.location.origin;
        const homePath = (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000')
          ? '/console/'
          : '/';

        setTimeout(() => {
          window.location.replace(homePath);
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
