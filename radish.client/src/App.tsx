import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from './i18n';
import { parseApiResponse, type ApiResponse } from './api/client';
import './App.css';

interface Forecast {
    date: string;
    temperatureC: number;
    temperatureF: number;
    summary: string;
}

interface CurrentUser {
    userId: number;
    userName: string;
    tenantId: number;
}

interface OidcCallbackProps {
    apiBaseUrl: string;
}

// 默认通过 Gateway 暴露的 API 入口
const defaultApiBase = 'https://localhost:5000';

function App() {
    const { t } = useTranslation();

    const [forecasts, setForecasts] = useState<Forecast[]>();
    const [error, setError] = useState<string>();
    const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
    const [userError, setUserError] = useState<string>();

    const apiBaseUrl = useMemo(() => {
        const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
        return (configured ?? defaultApiBase).replace(/\/$/, '');
    }, []);

    const isBrowser = typeof window !== 'undefined';
    const isOidcCallback = isBrowser && window.location.pathname === '/oidc/callback';

    // 🌍 启动时检查 URL 中的语言参数（从 Auth Server 返回时可能带有语言参数）
    useEffect(() => {
        if (!isBrowser || isOidcCallback) return;

        const url = new URL(window.location.href);
        const cultureParam = url.searchParams.get('culture') || url.searchParams.get('ui-culture');

        if (cultureParam && (cultureParam === 'zh' || cultureParam === 'en')) {
            if (i18n.language !== cultureParam) {
                void i18n.changeLanguage(cultureParam);
            }
            // 清理 URL 参数
            url.searchParams.delete('culture');
            url.searchParams.delete('ui-culture');
            window.history.replaceState({}, '', url.toString());
        }
    }, [isBrowser, isOidcCallback]);

    useEffect(() => {
        populateWeatherData();
        populateCurrentUser();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [apiBaseUrl]);

    // OIDC 回调页面：单独渲染回调组件
    if (isOidcCallback) {
        return <OidcCallback apiBaseUrl={apiBaseUrl} />;
    }

    const contents = forecasts === undefined
        ? <p><em>{error ?? t('weather.loading')}</em></p>
        : <table className="table table-striped" aria-labelledby="tableLabel">
            <thead>
                <tr>
                    <th>{t('weather.date')}</th>
                    <th>{t('weather.tempC')}</th>
                    <th>{t('weather.tempF')}</th>
                    <th>{t('weather.summary')}</th>
                </tr>
            </thead>
            <tbody>
                {forecasts.map(forecast =>
                    <tr key={forecast.date}>
                        <td>{forecast.date}</td>
                        <td>{forecast.temperatureC}</td>
                        <td>{forecast.temperatureF}</td>
                        <td>{forecast.summary}</td>
                    </tr>
                )}
            </tbody>
        </table>;

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginBottom: '0.5rem' }}>
                <button type="button" onClick={() => i18n.changeLanguage('zh')}>
                    {t('lang.zh')}
                </button>
                <button type="button" onClick={() => i18n.changeLanguage('en')}>
                    {t('lang.en')}
                </button>
            </div>
            <h1 id="tableLabel">{t('app.title')}</h1>
            <p>{t('app.description')}</p>

            <section style={{ marginBottom: '1rem' }}>
                <h2>{t('auth.sectionTitle')}</h2>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                        <button type="button" onClick={() => handleLogin(apiBaseUrl)}>
                            {t('auth.login')}
                        </button>
                        {currentUser && (
                            <button type="button" onClick={() => handleLogout(apiBaseUrl)}>
                                退出登录
                            </button>
                        )}
                    </div>
                    <div>
                        {currentUser && (
                            <span>
                                {t('auth.currentUser', {
                                    userName: currentUser.userName,
                                    userId: currentUser.userId,
                                    tenantId: currentUser.tenantId
                                })}
                            </span>
                        )}
                        {!currentUser && !userError && (
                            <span>{t('auth.notLoggedIn')}</span>
                        )}
                        {userError && (
                            <span>{t('auth.userInfoLoadFailedPrefix')}{userError}</span>
                        )}
                    </div>
                </div>
            </section>

            {contents}
        </div>
    );

    async function populateWeatherData() {
        const requestUrl = `${apiBaseUrl}/api/WeatherForecast/GetStandard`;
        try {
            const response = await apiFetch(requestUrl);
            const json = await response.json() as ApiResponse<Forecast[]>;
            const parsed = parseApiResponse(json, t);

            if (!parsed.ok || !parsed.data) {
                throw new Error(parsed.message || t('error.weather.load_failed'));
            }

            setForecasts(parsed.data);
            setError(undefined);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            setError(message);
            setForecasts(undefined);
        }
    }

    async function populateCurrentUser() {
        if (!isBrowser) {
            return;
        }

        const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;
        try {
            const response = await apiFetch(requestUrl, { withAuth: true });

            const json = await response.json() as ApiResponse<CurrentUser>;
            const parsed = parseApiResponse(json, t);

            if (!parsed.ok || !parsed.data) {
                throw new Error(parsed.message || t('auth.userInfoLoadFailedPrefix'));
            }

            setCurrentUser(parsed.data);
            setUserError(undefined);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            setUserError(`${t('auth.userInfoLoadFailedPrefix')}${message}`);
            setCurrentUser(null);
        }
    }
}

interface ApiFetchOptions extends RequestInit {
    withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
    const { withAuth, headers, ...rest } = options;

    const finalHeaders: HeadersInit = {
        Accept: 'application/json',
        'Accept-Language': i18n.language || 'zh',
        ...headers,
    };

    if (withAuth && typeof window !== 'undefined') {
        const token = window.localStorage.getItem('access_token');
        if (token) {
            (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
        }
    }

    return fetch(input, {
        ...rest,
        headers: finalHeaders,
    });
}

function handleLogin(apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    const redirectUri = `${window.location.origin}/oidc/callback`;

    const authorizeUrl = new URL(`${apiBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', 'radish-client');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    // 目前后端已为 radish-client 配置了 radish-api Scope，这里只请求资源 scope，避免无关 scope 带来 invalid_scope 问题
    authorizeUrl.searchParams.set('scope', 'radish-api');

    // 🌍 传递当前语言设置到 Auth Server，实现国际化统一
    const currentLanguage = i18n.language || 'zh';
    authorizeUrl.searchParams.set('culture', currentLanguage);
    authorizeUrl.searchParams.set('ui-culture', currentLanguage);

    window.location.href = authorizeUrl.toString();
}

function handleLogout(apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    // 清理本地保存的 Token
    window.localStorage.removeItem('access_token');
    window.localStorage.removeItem('refresh_token');

    // 🌍 传递当前语言设置
    const currentLanguage = i18n.language || 'zh';
    const logoutUrl = new URL(`${apiBaseUrl}/Account/Logout`);
    logoutUrl.searchParams.set('culture', currentLanguage);

    // 调用 Auth 的 Logout，并在完成后回到首页
    void fetch(logoutUrl.toString(), {
        method: 'POST',
        credentials: 'include',
        headers: {
            Accept: 'application/json',
            'Accept-Language': currentLanguage
        }
    }).catch(() => {
        // 忽略登出接口错误，仍然清理本地状态并跳转首页
    }).finally(() => {
        window.location.replace('/');
    });
}

function OidcCallback({ apiBaseUrl }: OidcCallbackProps) {
    const { t } = useTranslation();
    const [error, setError] = useState<string>();
    const [message, setMessage] = useState<string>(t('oidc.completingLogin'));

    useEffect(() => {
        if (typeof window === 'undefined') {
            return;
        }

        const url = new URL(window.location.href);
        const code = url.searchParams.get('code');

        // 🌍 从 URL 读取语言参数并同步到前端（用户可能在登录页切换了语言）
        const cultureParam = url.searchParams.get('culture') || url.searchParams.get('ui-culture');
        if (cultureParam && (cultureParam === 'zh' || cultureParam === 'en')) {
            void i18n.changeLanguage(cultureParam);
        }

        if (!code) {
            setError(t('oidc.missingCode'));
            setMessage(t('oidc.loginFailed'));
            return;
        }

        const redirectUri = `${window.location.origin}/oidc/callback`;

        const fetchToken = async () => {
            const body = new URLSearchParams();
            body.set('grant_type', 'authorization_code');
            body.set('client_id', 'radish-client');
            body.set('code', code);
            body.set('redirect_uri', redirectUri);

            try {
                const response = await fetch(`${apiBaseUrl}/connect/token`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body
                });

                if (!response.ok) {
                    throw new Error(t('oidc.tokenRequestFailed', { status: response.status, statusText: response.statusText }));
                }

                const tokenSet = await response.json() as {
                    access_token?: string;
                    refresh_token?: string;
                };

                if (!tokenSet.access_token) {
                    throw new Error(t('oidc.missingAccessToken'));
                }

                window.localStorage.setItem('access_token', tokenSet.access_token);
                if (tokenSet.refresh_token) {
                    window.localStorage.setItem('refresh_token', tokenSet.refresh_token);
                }

                setMessage(t('oidc.loginSucceeded'));

                // 使用 replace 避免在浏览器历史中留下带 code 的 URL
                window.location.replace('/');
            } catch (err) {
                const msg = err instanceof Error ? err.message : String(err);
                setError(msg);
                setMessage(t('oidc.loginFailed'));
            }
        };

        void fetchToken();
    }, [apiBaseUrl]);

    return (
        <div>
            <h1>{t('oidc.title')}</h1>
            <p>{message}</p>
            {error && <p>{t('oidc.errorDetailPrefix')}{error}</p>}
        </div>
    );
}

export default App;
