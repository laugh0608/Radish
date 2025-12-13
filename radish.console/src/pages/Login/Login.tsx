import { useState } from 'react';
import { Form, AntInput, AntButton, message } from '@radish/ui';
import './Login.css';

interface LoginProps {
  onLoginSuccess: () => void;
}

export const Login = ({ onLoginSuccess }: LoginProps) => {
  const [loading, setLoading] = useState(false);

  const handleLogin = () => {
    setLoading(true);

    // 跳转到 OIDC 登录
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';
    const currentOrigin = window.location.origin;
    const redirectUri = `${currentOrigin}/callback`;

    const authorizeUrl = new URL(`${apiBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', 'radish-console');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    authorizeUrl.searchParams.set('scope', 'radish-api');

    window.location.href = authorizeUrl.toString();
  };

  // 临时测试方案：使用默认账号登录获取 token
  const handleTestLogin = async () => {
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';

    try {
      setLoading(true);
      // 默认使用 admin 账号登录
      const response = await fetch(`${apiBaseUrl}/api/v1/Login/GetJwtToken`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          username: 'admin',
          password: 'admin123456',
        }),
      });

      const result = await response.json();

      if (result.success && result.response) {
        localStorage.setItem('access_token', result.response);
        message.success('登录成功');
        onLoginSuccess();
      } else {
        message.error(result.message || '登录失败');
      }
    } catch (error) {
      message.error('登录失败');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        <div className="login-header">
          <h1>🌿 Radish Console</h1>
          <p>后台管理系统</p>
        </div>

        <div className="login-content">
          <h3>选择登录方式</h3>

          <AntButton
            type="primary"
            size="large"
            block
            onClick={handleLogin}
            loading={loading}
            style={{ marginBottom: '12px' }}
          >
            OIDC 登录（推荐）
          </AntButton>

          <AntButton
            size="large"
            block
            onClick={() => void handleTestLogin()}
            loading={loading}
          >
            测试账号登录
          </AntButton>

          <div className="login-tip">
            <p>测试账号信息（选择任一账号）：</p>
            <p><strong>超级管理员:</strong> system / system123456</p>
            <p><strong>管理员:</strong> admin / admin123456</p>
            <p><strong>测试用户:</strong> test / test123456</p>
          </div>
        </div>
      </div>
    </div>
  );
};
