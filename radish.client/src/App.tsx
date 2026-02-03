import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from './i18n';
import { parseApiResponse, type ApiResponse } from '@radish/ui';
import { redirectToLogin, logout } from '@/services/auth';
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from './stores/userStore';
import { LevelUpModal } from '@radish/ui';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { log } from '@/utils/logger';
import { getApiBaseUrl, getAuthBaseUrl } from '@/config/env';
import { bootstrapAuth, type CurrentUser } from '@/services/authBootstrap';
import { tokenService } from '@/services/tokenService';
import './App.css';

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
        const cleanupAuth = bootstrapAuth({
            apiBaseUrl,
            onUserLoaded: (userData) => {
                setCurrentUser(userData);
                setUserError(undefined);

                // 同步到 WebOS 全局用户状态
                const webOsUser: WebOsUserInfo = {
                    userId: typeof userData.voUserId === 'string' ? parseInt(userData.voUserId, 10) : userData.voUserId,
                    userName: userData.voUserName,
                    tenantId: typeof userData.voTenantId === 'string' ? parseInt(userData.voTenantId, 10) : userData.voTenantId,
                    roles: ['User'],
                    avatarUrl: userData.voAvatarUrl,
                    avatarThumbnailUrl: userData.voAvatarThumbnailUrl
                };
                setWebOsUser(webOsUser);
            },
            onUserLoadFailed: (error) => {
                setUserError(`${t('auth.userInfoLoadFailedPrefix')}${error.message}`);
                setCurrentUser(null);
                clearWebOsUser();
            }
        });

        return () => {
            cleanupAuth();
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [apiBaseUrl]);

    // 注意：WebSocket 连接由 Shell.tsx 统一管理，此处不再启动

    // OIDC 回调页面：单独渲染回调组件
    if (isOidcCallback) {
        return <OidcCallback apiBaseUrl={apiBaseUrl} />;
    }

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
                            <button type="button" onClick={() => redirectToLogin()}>
                                {t('auth.login')}
                            </button>
                            {currentUser && (
                                <button type="button" onClick={() => logout()}>
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
                    expires_in?: number;
                    token_type?: string;
                };

                if (!tokenSet.access_token) {
                    throw new Error(t('oidc.missingAccessToken'));
                }

                if (tokenSet.expires_in) {
                    tokenService.setTokenInfo({
                        access_token: tokenSet.access_token,
                        refresh_token: tokenSet.refresh_token,
                        expires_in: tokenSet.expires_in,
                        token_type: tokenSet.token_type || 'Bearer'
                    });
                } else {
                    tokenService.setTokenInfoFromJwt(tokenSet.access_token, tokenSet.refresh_token);
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
