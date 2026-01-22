import { useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { AntButton } from '@radish/ui';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import './Login.css';

export function Login() {
  useDocumentTitle('登录');
  const [loading, setLoading] = useState(false);

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
            <p>使用统一身份认证登录管理控制台</p>
          </div>

          <div className="login-content">
            <AntButton
              type="primary"
              size="large"
              block
              onClick={handleLogin}
              loading={loading}
            >
              登录
            </AntButton>
          </div>
        </div>
      </div>
    </div>
  );
}
