import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { message } from '@radish/ui';
import { OidcCallbackError, redeemOidcAuthorizationCode } from '@radish/http';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { log } from '@/utils/logger';
import { ClientBackLink } from '@/components/ClientBackLink';
import i18n from '@/i18n';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';

/**
 * OIDC 回调处理页面
 */
export function OidcCallback() {
  const { t } = useTranslation();
  const [error, setError] = useState<string>();
  const [messageText, setMessageText] = useState<string>(() => i18n.t('console.callback.processing'));
  const navigate = useNavigate();
  useDocumentTitle(t('console.callback.documentTitle'));

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    let cancelled = false;
    let redirectTimer: ReturnType<typeof setTimeout> | null = null;

    const redirectUri = getRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const completeLogin = async () => {
      try {
        const tokenSet = await redeemOidcAuthorizationCode({
          clientId: 'radish-console',
          authServerBaseUrl,
          redirectUri,
          missingCodeMessage: i18n.t('console.callback.missingCode'),
          staleCallbackMessage: i18n.t('console.callback.stale'),
          missingAccessTokenMessage: i18n.t('console.callback.missingAccessToken'),
          buildTokenRequestFailedMessage: ({ status, statusText, error, errorDescription }) => {
            const detailMessage = errorDescription || error;
            return detailMessage
              ? i18n.t('console.callback.tokenRequestFailedWithDetail', { status, statusText, detail: detailMessage })
              : i18n.t('console.callback.tokenRequestFailed', { status, statusText });
          }
        });

        if (cancelled) {
          return;
        }

        // 使用 TokenService 存储 Token 信息
        tokenService.setTokenInfo({
          access_token: tokenSet.access_token,
          refresh_token: tokenSet.refresh_token,
          expires_in: tokenSet.expires_in || 3600, // 默认 1 小时
          token_type: tokenSet.token_type || 'Bearer',
        });
        tokenService.recordSessionActivity(true);
        log.debug('OidcCallback', '登录后 Token 状态', tokenService.getTokenDebugInfo());
        window.dispatchEvent(new CustomEvent('auth:token-updated'));

        log.info('OidcCallback', 'Token 信息已保存');
        setMessageText(i18n.t('console.callback.successRedirecting'));
        message.success(i18n.t('console.callback.success'));

        // 使用 React Router 导航到首页
        redirectTimer = setTimeout(() => {
          navigate('/', { replace: true });
        }, 500);
      } catch (err) {
        if (cancelled) {
          return;
        }

        if (err instanceof OidcCallbackError && err.code === 'stale_callback') {
          const staleMessage = i18n.t('console.callback.stale');
          setError(staleMessage);
          setMessageText(i18n.t('console.callback.failure'));
          message.error(staleMessage);
          return;
        }

        const msg = err instanceof Error ? err.message : String(err);
        setError(msg);
        setMessageText(i18n.t('console.callback.failure'));
        message.error(i18n.t('console.callback.failureWithDetail', { detail: msg }));
      }
    };

    void completeLogin();

    return () => {
      cancelled = true;
      if (redirectTimer) {
        clearTimeout(redirectTimer);
      }
    };
  }, [navigate]);

  return (
    <div style={{ padding: '40px', textAlign: 'center' }}>
      <h1>{t('console.callback.heading')}</h1>
      <p>{messageText}</p>
      {error && <p style={{ color: 'red' }}>{t('console.callback.errorDetails', { detail: error })}</p>}
      {error ? <ClientBackLink /> : null}
    </div>
  );
}
