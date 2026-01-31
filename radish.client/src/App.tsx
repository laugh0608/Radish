import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from './i18n';
import { parseApiResponse, type ApiResponse } from '@radish/ui';
import { notificationHub } from '@/services/notificationHub';
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from './stores/userStore';
import { LevelUpModal } from '@radish/ui';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { log } from '@/utils/logger';
import { getApiBaseUrl, getAuthBaseUrl } from '@/config/env';
import './App.css';

interface Forecast {
    date: string;
    temperatureC: number;
    temperatureF: number;
    summary: string;
}

interface CurrentUser {
    voUserId: number;
    voUserName: string;
    voTenantId: number;
    voAvatarUrl?: string;
    voAvatarThumbnailUrl?: string;
}

// WebOS 全局用户信息结构（与 useUserStore.UserInfo 对齐）
interface WebOsUserInfo {
    userId: number;
    userName: string;
    tenantId: number;
    roles: string[];
    avatarUrl?: string;
    avatarThumbnailUrl?: string;
}

interface OidcCallbackProps {
    apiBaseUrl: string;
}

function App() {
    const { t } = useTranslation();

    const [forecasts, setForecasts] = useState<Forecast[]>();
    const [error, setError] = useState<string>();
    const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
    const setWebOsUser = useUserStore(state => state.setUser);
    const clearWebOsUser = useUserStore(state => state.clearUser);
    const [userError, setUserError] = useState<string>();
    const unreadCount = useNotificationStore(state => state.unreadCount);
    const hubState = useNotificationStore(state => state.connectionState);

    // 升级事件监听
    const { levelUpData, showModal, handleClose } = useLevelUpListener();

    const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);

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
        
        // 验证 userStore 状态
        setTimeout(() => {
            const userState = useUserStore.getState();
            log.info('App', '========== 验证 userStore 状态 ==========');
            log.info('App', 'userId:', userState.userId);
            log.info('App', 'userName:', userState.userName);
            log.info('App', 'isAuthenticated:', userState.isAuthenticated());
        }, 1000);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [apiBaseUrl]);

    // 注意：WebSocket 连接由 Shell.tsx 统一管理，此处不再启动

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
        <>
            <main className="app-shell">
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
                                        userName: currentUser.voUserName,
                                        userId: currentUser.voUserId,
                                        tenantId: currentUser.voTenantId
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

                        <div>
                            <strong>通知</strong>
                            <div>Hub: {hubState}</div>
                            <div>未读: {unreadCount}</div>
                        </div>
                    </div>
                </section>

                {contents}
            </main>

            {/* 升级动画弹窗 */}
            {levelUpData && (
                <LevelUpModal
                    isOpen={showModal}
                    data={levelUpData}
                    onClose={handleClose}
                />
            )}
        </>
    );

    async function populateWeatherData() {
        const requestUrl = `${apiBaseUrl}/api/v2/WeatherForecast/GetStandard`;
        try {
            const response = await apiFetch(requestUrl);
            const json = await response.json() as ApiResponse<Forecast[]>;
            const parsed = parseApiResponse(json);

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

        log.info('App', '========== 主页面开始获取用户信息 ==========');

        // 优先使用缓存的用户信息（从登录回调页面预加载）
        const cachedUserInfo = window.localStorage.getItem('cached_user_info');
        log.info('App', '检查 localStorage 缓存:', cachedUserInfo ? '✅ 存在' : '❌ 不存在');

        if (cachedUserInfo) {
            try {
                const userData = JSON.parse(cachedUserInfo) as CurrentUser;
                log.info('App', '✅ 使用缓存的用户信息');
                log.info('App', '缓存数据详情:', {
                    userId: userData.voUserId,
                    userName: userData.voUserName,
                    tenantId: userData.voTenantId,
                    hasAvatar: !!userData.voAvatarUrl
                });

                // 验证缓存数据的有效性
                if (!userData.voUserId || !userData.voUserName) {
                    log.error('App', '❌ 缓存数据无效，userId 或 userName 为空');
                    window.localStorage.removeItem('cached_user_info');
                    // 继续从服务器获取
                } else {
                    setCurrentUser(userData);
                    setUserError(undefined);

                    // 同步到 WebOS 全局用户状态
                    const webOsUser: WebOsUserInfo = {
                        userId: userData.voUserId,
                        userName: userData.voUserName,
                        tenantId: userData.voTenantId,
                        roles: ['User'],
                        avatarUrl: userData.voAvatarUrl,
                        avatarThumbnailUrl: userData.voAvatarThumbnailUrl
                    };
                    setWebOsUser(webOsUser);
                    log.info('App', '✅ WebOS 用户状态已更新');
                    log.info('App', 'WebOS 状态详情:', {
                        userId: webOsUser.userId,
                        userName: webOsUser.userName,
                        tenantId: webOsUser.tenantId
                    });

                    // 清除缓存，避免使用过期数据
                    window.localStorage.removeItem('cached_user_info');
                    log.info('App', '========== 用户信息加载完成（使用缓存）==========');
                    return;
                }
            } catch (err) {
                // 缓存数据解析失败，继续从服务器获取
                log.error('App', '❌ 缓存数据解析失败:', err);
                window.localStorage.removeItem('cached_user_info');
            }
        }

        const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;
        log.info('App', '📡 从服务器获取用户信息:', requestUrl);

        try {
            const response = await apiFetch(requestUrl, { withAuth: true });
            log.info('App', '用户信息请求响应状态:', response.status);

            const json = await response.json() as ApiResponse<CurrentUser>;
            log.debug('App', '用户信息响应数据:', json);

            const parsed = parseApiResponse(json);

            if (!parsed.ok || !parsed.data) {
                throw new Error(parsed.message || t('auth.userInfoLoadFailedPrefix'));
            }

            log.info('App', '✅ 用户信息获取成功');
            log.info('App', '服务器数据详情:', {
                userId: parsed.data.voUserId,
                userName: parsed.data.voUserName,
                tenantId: parsed.data.voTenantId,
                hasAvatar: !!parsed.data.voAvatarUrl
            });

            setCurrentUser(parsed.data);
            setUserError(undefined);

            // 同步到 WebOS 全局用户状态，默认赋予基础角色
            const webOsUser: WebOsUserInfo = {
                userId: parsed.data.voUserId,
                userName: parsed.data.voUserName,
                tenantId: parsed.data.voTenantId,
                roles: ['User'],
                avatarUrl: parsed.data.voAvatarUrl,
                avatarThumbnailUrl: parsed.data.voAvatarThumbnailUrl
            };
            setWebOsUser(webOsUser);
            log.info('App', '✅ WebOS 用户状态已更新');
            log.info('App', 'WebOS 状态详情:', {
                userId: webOsUser.userId,
                userName: webOsUser.userName,
                tenantId: webOsUser.tenantId
            });
            log.info('App', '========== 用户信息加载完成（从服务器）==========');
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            log.error('App', '❌ 获取用户信息失败:', message);
            setUserError(`${t('auth.userInfoLoadFailedPrefix')}${message}`);
            setCurrentUser(null);
            clearWebOsUser();
            log.info('App', '========== 用户信息加载失败 ==========');
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

function handleLogin(_apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    const redirectUri = `${window.location.origin}/oidc/callback`;
    const authServerBaseUrl = getAuthBaseUrl();

    const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
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

function handleLogout(_apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    // 清理本地保存的 Token
    window.localStorage.removeItem('access_token');
    window.localStorage.removeItem('refresh_token');

    // 使用 OIDC 标准的 endsession endpoint 实现 Single Sign-Out
    const postLogoutRedirectUri = window.location.origin;
    const authServerBaseUrl = getAuthBaseUrl();

    const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
    logoutUrl.searchParams.set('client_id', 'radish-client');

    // 🌍 传递当前语言设置
    const currentLanguage = i18n.language || 'zh';
    logoutUrl.searchParams.set('culture', currentLanguage);

    // 重定向到 OIDC logout endpoint，Auth Server 会清除 session 并重定向回来
    window.location.href = logoutUrl.toString();
}

function OidcCallback({ apiBaseUrl }: OidcCallbackProps) {
    const { t } = useTranslation();
    const [error, setError] = useState<string>();
    const [message, setMessage] = useState<string>(t('oidc.completingLogin'));
    // 使用 useRef 而不是 useState，因为 React StrictMode 会卸载并重新挂载组件
    // useState 会被重置，但 useRef 在整个页面生命周期内保持值
    const hasExecutedRef = useRef(false);

    useEffect(() => {
        if (typeof window === 'undefined') {
            return;
        }

        // 防止 React StrictMode 导致的重复执行（授权码只能使用一次）
        if (hasExecutedRef.current) {
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

        // 标记为已执行，防止重复请求
        hasExecutedRef.current = true;

        const redirectUri = `${window.location.origin}/oidc/callback`;
        const authServerBaseUrl = getAuthBaseUrl();

        const fetchToken = async () => {
            const body = new URLSearchParams();
            body.set('grant_type', 'authorization_code');
            body.set('client_id', 'radish-client');
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

                // 立即获取用户信息并缓存，避免主页面加载时的竞态条件
                log.info('OidcCallback', '========== 回调页面开始预加载用户信息 ==========');
                try {
                    const userResponse = await apiFetch(
                        `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`,
                        { withAuth: true }
                    );
                    log.info('OidcCallback', '用户信息请求响应状态:', userResponse.status);

                    const userJson = await userResponse.json() as ApiResponse<CurrentUser>;
                    log.debug('OidcCallback', '用户信息响应数据:', userJson);

                    const userParsed = parseApiResponse(userJson);

                    if (userParsed.ok && userParsed.data) {
                        // 验证数据有效性
                        if (!userParsed.data.voUserId || !userParsed.data.voUserName) {
                            log.error('OidcCallback', '❌ 用户数据无效，userId 或 userName 为空');
                            log.error('OidcCallback', '无效数据:', userParsed.data);
                        } else {
                            // 缓存用户信息到 localStorage，主页面可以优先使用
                            const cacheData = JSON.stringify(userParsed.data);
                            window.localStorage.setItem('cached_user_info', cacheData);
                            log.info('OidcCallback', '✅ 用户信息已缓存到 localStorage');
                            log.info('OidcCallback', '缓存数据详情:', {
                                userId: userParsed.data.voUserId,
                                userName: userParsed.data.voUserName,
                                tenantId: userParsed.data.voTenantId,
                                hasAvatar: !!userParsed.data.voAvatarUrl,
                                cacheLength: cacheData.length
                            });
                        }
                    } else {
                        log.warn('OidcCallback', '❌ 用户信息解析失败:', userParsed.message);
                    }
                } catch (err) {
                    // 忽略错误，主页面会重新获取
                    log.error('OidcCallback', '❌ 预加载用户信息失败:', err);
                }

                log.info('OidcCallback', '========== 即将跳转到主页面 ==========');
                setMessage(t('oidc.loginSucceeded'));

                // 使用 replace 避免在浏览器历史中留下带 code 的 URL
                // 登录成功后跳转回 WebOS Shell（根路径）
                window.location.replace('/');
            } catch (err) {
                const msg = err instanceof Error ? err.message : String(err);
                setError(msg);
                setMessage(t('oidc.loginFailed'));
            }
        };

        void fetchToken();
    }, [apiBaseUrl, t]);

    return (
        <div>
            <h1>{t('oidc.title')}</h1>
            <p>{message}</p>
            {error && <p>{t('oidc.errorDetailPrefix')}{error}</p>}
        </div>
    );
}

export default App;
