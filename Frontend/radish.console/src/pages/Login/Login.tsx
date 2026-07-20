import { useCallback, useEffect, useRef, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { AntButton } from '@radish/ui';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import { ClientBackLink } from '@/components/ClientBackLink';
import './Login.css';
import { useTranslation } from 'react-i18next';
import { LanguageSwitcher } from '@/i18n/LanguageSwitcher';
import { normalizeLanguage } from '@/locales/language';

export function Login() {
  const { t, i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  useDocumentTitle(t('console.login.title'));
  const [loading, setLoading] = useState(false);
  const hasAutoLoginTriggeredRef = useRef(false);

  const handleLogin = useCallback(() => {
    setLoading(true);

    const redirectUri = getRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', 'radish-console');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    authorizeUrl.searchParams.set('scope', 'openid profile offline_access radish-api');
    authorizeUrl.searchParams.set('culture', language);
    authorizeUrl.searchParams.set('ui_locales', language);

    window.location.href = authorizeUrl.toString();
  }, [language]);

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
