import { useState } from 'react';
import { log } from '@/utils/logger';
import { useNavigate } from 'react-router-dom';
import { AntButton, message } from '@radish/ui';
import './Login.css';

/**
 * 获取 Auth Server 的基础 URL
 */
function getAuthServerBaseUrl(): string {
  const currentOrigin = window.location.origin;

  // 通过 Gateway 访问
  if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
    return currentOrigin;
  }

  // 直接访问 console 开发服务器
  if (currentOrigin === 'http://localhost:3100' || currentOrigin === 'https://localhost:3100') {
    return 'http://localhost:5200';
  }

  return currentOrigin;
}

/**
 * 获取 redirect_uri
 */
function getRedirectUri(): string {
  const currentOrigin = window.location.origin;

  // 通过 Gateway 访问
  if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
    return `${currentOrigin}/console/callback`;
  }

  // 直接访问开发服务器（需要 /console 前缀，因为 basename 是 /console）
  return `${currentOrigin}/console/callback`;
}

export function Login() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const handleLogin = () => {
    setLoading(true);

    const redirectUri = getRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', 'radish-console');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    authorizeUrl.searchParams.set('scope', 'openid profile radish-api');

    window.location.href = authorizeUrl.toString();
  };

  // 临时测试方案：使用默认账号登录获取 token
  const handleTestLogin = async () => {
    // 统一通过 Gateway 访问
    const apiBaseUrl = window.location.origin;

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
        navigate('/', { replace: true });
      } else {
        message.error(result.message || '登录失败');
      }
    } catch (error) {
      message.error('登录失败');
      log.error(error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        {/* 左侧信息区域 */}
        <div className="login-info">
          <h2>Radish Console</h2>
          <p>现代化社区平台管理控制台</p>
          <p>统一管理用户、应用、权限和系统配置</p>

          <div className="login-info-features">
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>统一身份认证 (OIDC)</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>细粒度权限控制</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>实时系统监控</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>应用生态管理</span>
            </div>
          </div>
        </div>

        {/* 右侧登录表单区域 */}
        <div className="login-form">
          <div className="login-header">
            <h1>登录</h1>
            <p>选择登录方式进入管理控制台</p>
          </div>

          <div className="login-content">
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
              <p>提示：测试账号登录仅用于开发环境快速测试</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
