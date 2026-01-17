import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { message } from '@radish/ui';
import { getAuthServerBaseUrl, getRedirectUri } from '@/config/env';

/**
 * OIDC 回调处理页面
 */
export function OidcCallback() {
  const [error, setError] = useState<string>();
  const [messageText, setMessageText] = useState<string>('正在完成登录...');
  const hasProcessed = useRef(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    // 防止重复执行（React StrictMode 会导致 useEffect 执行两次）
    if (hasProcessed.current) {
      return;
    }
    hasProcessed.current = true;

    const url = new URL(window.location.href);
    const code = url.searchParams.get('code');

    if (!code) {
      setError('授权码缺失');
      setMessageText('登录失败');
      return;
    }

    const redirectUri = getRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const fetchToken = async () => {
      const body = new URLSearchParams();
      body.set('grant_type', 'authorization_code');
      body.set('client_id', 'radish-console');
      body.set('code', code);
      body.set('redirect_uri', redirectUri);

      try {
        const response = await fetch(`${authServerBaseUrl}/connect/token`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
          },
          body
        });

        if (!response.ok) {
          throw new Error(`Token 请求失败: ${response.status} ${response.statusText}`);
        }

        const tokenSet = await response.json() as {
          access_token?: string;
          refresh_token?: string;
        };

        if (!tokenSet.access_token) {
          throw new Error('未收到 access_token');
        }

        window.localStorage.setItem('access_token', tokenSet.access_token);
        if (tokenSet.refresh_token) {
          window.localStorage.setItem('refresh_token', tokenSet.refresh_token);
        }

        setMessageText('登录成功，正在跳转...');
        message.success('登录成功');

        // 使用 React Router 导航到首页
        setTimeout(() => {
          navigate('/', { replace: true });
        }, 500);
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setError(msg);
        setMessageText('登录失败');
        message.error('登录失败：' + msg);
      }
    };

    void fetchToken();
  }, [navigate]);

  return (
    <div style={{ padding: '40px', textAlign: 'center' }}>
      <h1>OIDC 回调处理</h1>
      <p>{messageText}</p>
      {error && <p style={{ color: 'red' }}>错误详情：{error}</p>}
    </div>
  );
}
