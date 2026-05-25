import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { OidcCallbackError, redeemOidcAuthorizationCode } from '@radish/http';
import i18n from '@/i18n';
import { getApiBaseUrl, getAuthBaseUrl } from '@/config/env';
import { hydrateAuthUser } from '@/services/authBootstrap';
import { tokenService } from '@/services/tokenService';
import { getOidcRedirectUri } from '@/platform/tauriBridge';
import styles from './OidcCallbackPage.module.css';

function syncLanguageFromCallbackUrl(url: URL): void {
  const cultureParam = url.searchParams.get('culture') || url.searchParams.get('ui-culture');
  if (cultureParam === 'zh' || cultureParam === 'en') {
    void i18n.changeLanguage(cultureParam);
  }
}

export function OidcCallbackPage() {
  const { t } = useTranslation();
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>(t('oidc.completingLogin'));

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    let cancelled = false;
    const url = new URL(window.location.href);
    syncLanguageFromCallbackUrl(url);

    const redirectUri = getOidcRedirectUri();
    const authServerBaseUrl = getAuthBaseUrl();
    const apiBaseUrl = getApiBaseUrl();

    const completeLogin = async () => {
      try {
        const tokenSet = await redeemOidcAuthorizationCode({
          clientId: 'radish-client',
          authServerBaseUrl,
          redirectUri,
          missingCodeMessage: t('oidc.missingCode'),
          staleCallbackMessage: t('oidc.staleCallback'),
          missingAccessTokenMessage: t('oidc.missingAccessToken'),
          buildTokenRequestFailedMessage: ({ status, statusText, error: tokenError, errorDescription }) => {
            const baseMessage = t('oidc.tokenRequestFailed', { status, statusText });
            const detailMessage = errorDescription || tokenError;
            return detailMessage ? `${baseMessage} (${detailMessage})` : baseMessage;
          },
        });

        if (cancelled) {
          return;
        }

        if (tokenSet.expires_in) {
          tokenService.setTokenInfo({
            access_token: tokenSet.access_token,
            refresh_token: tokenSet.refresh_token,
            expires_in: tokenSet.expires_in,
            token_type: tokenSet.token_type || 'Bearer',
          });
        } else {
          tokenService.setTokenInfoFromJwt(tokenSet.access_token, tokenSet.refresh_token);
        }

        if (cancelled) {
          return;
        }

        setMessage(t('oidc.syncingProfile'));
        await hydrateAuthUser({
          apiBaseUrl,
          useCache: false,
        }).catch(() => null);

        if (cancelled) {
          return;
        }

        setMessage(t('oidc.loginSucceeded'));
        window.location.replace('/');
      } catch (err) {
        if (cancelled) {
          return;
        }

        if (err instanceof OidcCallbackError && err.code === 'stale_callback') {
          setError(t('oidc.staleCallback'));
          setMessage(t('oidc.loginFailed'));
          return;
        }

        const detail = err instanceof Error ? err.message : String(err);
        setError(detail);
        setMessage(t('oidc.loginFailed'));
      }
    };

    void completeLogin();

    return () => {
      cancelled = true;
    };
  }, [t]);

  return (
    <main className={styles.page}>
      <section className={styles.card} aria-live="polite">
        <h1 className={styles.title}>{t('oidc.title')}</h1>
        <p className={styles.message}>{message}</p>
        {error ? <p className={styles.error}>{t('oidc.errorDetailPrefix')}{error}</p> : null}
      </section>
    </main>
  );
}
