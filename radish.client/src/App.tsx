import { useEffect, useMemo, useState } from 'react';
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

    // OIDC 回调页面：单独渲染回调组件
    if (isOidcCallback) {
        return <OidcCallback apiBaseUrl={apiBaseUrl} />;
    }

    useEffect(() => {
        populateWeatherData();
        populateCurrentUser();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [apiBaseUrl]);

    const contents = forecasts === undefined
        ? <p><em>{error ?? 'Loading weather data from Radish.Api...'}</em></p>
        : <table className="table table-striped" aria-labelledby="tableLabel">
            <thead>
                <tr>
                    <th>Date</th>
                    <th>Temp. (C)</th>
                    <th>Temp. (F)</th>
                    <th>Summary</th>
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
            <h1 id="tableLabel">Radish Weather Forecast</h1>
            <p>实时展示来自 Radish.Api 的 WeatherForecast 示例数据，便于验证前后端联通性。</p>

            <section style={{ marginBottom: '1rem' }}>
                <h2>Authentication</h2>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                        <button type="button" onClick={() => handleLogin(apiBaseUrl)}>
                            通过 OIDC 登录
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
                                当前用户：{currentUser.userName}（Id: {currentUser.userId}, Tenant: {currentUser.tenantId}）
                            </span>
                        )}
                        {!currentUser && !userError && (
                            <span>当前未登录</span>
                        )}
                        {userError && (
                            <span>用户信息加载失败：{userError}</span>
                        )}
                    </div>
                </div>
            </section>

            {contents}
        </div>
    );

    async function populateWeatherData() {
        const requestUrl = `${apiBaseUrl}/api/WeatherForecast/Get`;
        try {
            const response = await apiFetch(requestUrl, apiBaseUrl);
            const data = await response.json();
            setForecasts(data as Forecast[]);
            setError(undefined);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            setError(`无法从 ${requestUrl} 获取数据：${message}`);
            setForecasts(undefined);
        }
    }

    async function populateCurrentUser() {
        if (!isBrowser) {
            return;
        }

        const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;
        try {
            const response = await apiFetch(requestUrl, apiBaseUrl, { withAuth: true });

            const json = await response.json() as {
                statusCode: number;
                isSuccess: boolean;
                messageInfo: string;
                responseData?: CurrentUser;
            };

            if (!json.isSuccess || !json.responseData) {
                throw new Error(json.messageInfo || '未能获取到当前用户信息');
            }

            setCurrentUser(json.responseData);
            setUserError(undefined);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            setUserError(`无法从 ${requestUrl} 获取当前用户：${message}`);
            setCurrentUser(null);
        }
    }
}

interface ApiFetchOptions extends RequestInit {
    withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, apiBaseUrl: string, options: ApiFetchOptions = {}) {
    const { withAuth, headers, ...rest } = options;

    const finalHeaders: HeadersInit = {
        Accept: 'application/json',
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

    window.location.href = authorizeUrl.toString();
}

function handleLogout(apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    // 清理本地保存的 Token
    window.localStorage.removeItem('access_token');
    window.localStorage.removeItem('refresh_token');

    const logoutUrl = `${apiBaseUrl}/Account/Logout`;

    // 调用 Auth 的 Logout，并在完成后回到首页
    void fetch(logoutUrl, {
        method: 'POST',
        headers: {
            Accept: 'application/json'
        }
    }).catch(() => {
        // 忽略登出接口错误，仍然清理本地状态并跳转首页
    }).finally(() => {
        window.location.replace('/');
    });
}

function OidcCallback({ apiBaseUrl }: OidcCallbackProps) {
    const [error, setError] = useState<string>();
    const [message, setMessage] = useState<string>('正在完成登录...');

    useEffect(() => {
        if (typeof window === 'undefined') {
            return;
        }

        const url = new URL(window.location.href);
        const code = url.searchParams.get('code');

        if (!code) {
            setError('缺少授权码 code。');
            setMessage('登录失败');
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
                    throw new Error(`Token 请求失败: ${response.status} ${response.statusText}`);
                }

                const tokenSet = await response.json() as {
                    access_token?: string;
                    refresh_token?: string;
                };

                if (!tokenSet.access_token) {
                    throw new Error('Token 响应中缺少 access_token。');
                }

                window.localStorage.setItem('access_token', tokenSet.access_token);
                if (tokenSet.refresh_token) {
                    window.localStorage.setItem('refresh_token', tokenSet.refresh_token);
                }

                setMessage('登录成功，即将跳转到首页...');

                // 使用 replace 避免在浏览器历史中留下带 code 的 URL
                window.location.replace('/');
            } catch (err) {
                const msg = err instanceof Error ? err.message : String(err);
                setError(msg);
                setMessage('登录失败');
            }
        };

        void fetchToken();
    }, [apiBaseUrl]);

    return (
        <div>
            <h1>OIDC 登录回调</h1>
            <p>{message}</p>
            {error && <p>错误详情：{error}</p>}
        </div>
    );
}

export default App;
