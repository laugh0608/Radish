import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';
import { createOidcAuthorizationUrl } from '@radish/http';
import { AntButton } from '@radish/ui';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import { ClientBackLink } from '@/components/ClientBackLink';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { normalizeLanguage } from '@/locales/language';
import { rememberConsoleAuthReturnPath } from '@/services/authReturnPath';
import { log } from '@/utils/logger';
import './Login.css';

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
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const authorizationRef = useRef<Promise<string> | null>(null);

  useEffect(() => {
    let cancelled = false;

    // StrictMode 重放 effect 时复用同一次 PKCE 创建，避免覆盖 state / verifier。
    const authorization = authorizationRef.current ??= createOidcAuthorizationUrl({
      clientId: 'radish-console',
      authServerBaseUrl: getAuthServerBaseUrl(),
      redirectUri: getRedirectUri(),
      scope: 'openid profile offline_access radish-api',
      additionalParameters: {
        culture: language,
        ui_locales: language,
      },
    });

    void authorization.then((authorizeUrl) => {
      if (cancelled) {
        return;
      }

      const locationState = location.state as LoginLocationState | null;
      if (locationState?.returnLocation) {
        rememberConsoleAuthReturnPath(locationState.returnLocation);
      }
      window.location.replace(authorizeUrl);
    }).catch((error: unknown) => {
      if (cancelled) {
        return;
      }

      log.error('Login', '启动 OIDC 登录失败', error);
      setFailed(true);
    });

    return () => {
      cancelled = true;
    };
  }, [attempt, language, location.state]);

  const retryLogin = () => {
    authorizationRef.current = null;
    setFailed(false);
    setAttempt((current) => current + 1);
  };

  return (
    <main className="console-login-transition">
      {failed ? (
        <>
          <p role="alert">{t('console.login.startFailed')}</p>
          <div className="console-login-transition__actions">
            <AntButton type="primary" onClick={retryLogin}>
              {t('console.login.retry')}
            </AntButton>
            <ClientBackLink />
          </div>
        </>
      ) : (
        <p role="status" aria-live="polite">{t('console.login.redirecting')}</p>
      )}
    </main>
  );
}
