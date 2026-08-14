import { useCallback, useEffect, useRef, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { createOidcAuthorizationUrl } from '@radish/http';
import { AntButton, message } from '@radish/ui';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import { ClientBackLink } from '@/components/ClientBackLink';
import './Login.css';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';
import { LanguageSwitcher } from '@/i18n/LanguageSwitcher';
import { normalizeLanguage } from '@/locales/language';
import { log } from '@/utils/logger';
import { rememberConsoleAuthReturnPath } from '@/services/authReturnPath';

interface LoginLocationState {
  returnLocation?: {
    pathname: string;
    search?: string;
    hash?: string;
  };
}

export function Login() {
  const { t, i18n } = useTranslation();
  const location = useLocation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  useDocumentTitle(t('console.login.title'));
  const [loading, setLoading] = useState(false);
  const hasAutoLoginTriggeredRef = useRef(false);

  const handleLogin = useCallback(() => {
    setLoading(true);

    void createOidcAuthorizationUrl({
      clientId: 'radish-console',
      authServerBaseUrl: getAuthServerBaseUrl(),
      redirectUri: getRedirectUri(),
      scope: 'openid profile offline_access radish-api',
      additionalParameters: {
        culture: language,
        ui_locales: language,
      },
    }).then((authorizeUrl) => {
      const locationState = location.state as LoginLocationState | null;
      if (locationState?.returnLocation) {
        rememberConsoleAuthReturnPath(locationState.returnLocation);
      }
      window.location.href = authorizeUrl;
    }).catch((error: unknown) => {
      log.error('Login', '启动 OIDC 登录失败', error);
      setLoading(false);
      message.error(t('console.login.startFailed'));
    });
  }, [language, location.state, t]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    if (hasAutoLoginTriggeredRef.current) {
      return;
    }

    const shouldAutoLogin = new URL(window.location.href).searchParams.get('auto') === '1';
    if (!shouldAutoLogin) {
      return;
    }

    hasAutoLoginTriggeredRef.current = true;
    handleLogin();
  }, [handleLogin]);

  return (
    <div className="login-container">
      <div className="login-language-switcher">
        <LanguageSwitcher />
      </div>
      <div className="login-box">
        {/* 左侧信息区域 */}
        <div className="login-info">
          <h2>Radish Console</h2>
          <p>{t('console.login.description')}</p>
          <p>{t('console.login.summary')}</p>

          <div className="login-info-features">
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>{t('console.login.feature.oidc')}</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>{t('console.login.feature.permission')}</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>{t('console.login.feature.monitoring')}</span>
            </div>
            <div className="login-info-feature">
              <div className="login-info-feature-icon">+</div>
              <span>{t('console.login.feature.application')}</span>
            </div>
          </div>
        </div>

        {/* 右侧登录表单区域 */}
        <div className="login-form">
          <div className="login-header">
            <h1>{t('console.login.title')}</h1>
            <p>{t('console.login.hint')}</p>
          </div>

          <div className="login-content">
            <AntButton
              type="primary"
              size="large"
              block
              onClick={handleLogin}
              loading={loading}
            >
              {t('console.login.action')}
            </AntButton>
            <div className="login-client-back">
              <ClientBackLink />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
