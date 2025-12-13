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

  const handleMenuClick = (key: string) => {
    setCurrentMenu(key as MenuItem);
  };

  const handleUserMenuClick = (key: string) => {
    switch (key) {
      case 'logout':
        localStorage.removeItem('access_token');
        setIsLoggedIn(false);
        message.success('已退出登录');
        break;
      case 'profile':
        message.info('个人信息功能待实现');
        break;
      case 'settings':
        message.info('设置功能待实现');
        break;
    }
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

export default App;
