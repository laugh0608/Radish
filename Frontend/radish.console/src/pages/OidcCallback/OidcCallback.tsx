import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { message } from '@radish/ui';
import { OidcCallbackError, redeemOidcAuthorizationCode } from '@radish/http';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { log } from '@/utils/logger';

/**
 * OIDC 回调处理页面
 */
export function OidcCallback() {
  const [error, setError] = useState<string>();
  const [messageText, setMessageText] = useState<string>('正在完成登录...');
  const navigate = useNavigate();

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
          missingCodeMessage: '授权码缺失',
          staleCallbackMessage: '登录回调已失效，请重新发起登录。',
          missingAccessTokenMessage: '未收到 access_token',
          buildTokenRequestFailedMessage: ({ status, statusText, error, errorDescription }) => {
            const detailMessage = errorDescription || error;
            return detailMessage
              ? `Token 请求失败: ${status} ${statusText} (${detailMessage})`
              : `Token 请求失败: ${status} ${statusText}`;
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
        log.debug('OidcCallback', '登录后 Token 状态', tokenService.getTokenDebugInfo());
        window.dispatchEvent(new CustomEvent('auth:token-updated'));

        log.info('OidcCallback', 'Token 信息已保存');
        setMessageText('登录成功，正在跳转...');
        message.success('登录成功');

        // 使用 React Router 导航到首页
        redirectTimer = setTimeout(() => {
          navigate('/', { replace: true });
        }, 500);
      } catch (err) {
        if (cancelled) {
          return;
        }

        if (err instanceof OidcCallbackError && err.code === 'stale_callback') {
          setError('登录回调已失效，请重新发起登录。');
          setMessageText('登录失败');
          message.error('登录回调已失效，请重新发起登录。');
          return;
        }

        const msg = err instanceof Error ? err.message : String(err);
        setError(msg);
        setMessageText('登录失败');
        message.error('登录失败：' + msg);
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
      <h1>OIDC 回调处理</h1>
      <p>{messageText}</p>
      {error && <p style={{ color: 'red' }}>错误详情：{error}</p>}
    </div>
  );
}
