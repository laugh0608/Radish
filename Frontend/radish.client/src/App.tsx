import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from './i18n';
import { OidcCallbackError, redeemOidcAuthorizationCode } from '@radish/http';
import { redirectToLogin, logout } from '@/services/auth';
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from './stores/userStore';
import { LevelUpModal } from '@radish/ui/level-up-modal';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { getApiBaseUrl, getAuthBaseUrl } from '@/config/env';
import { bootstrapAuth, type CurrentUser } from '@/services/authBootstrap';
import { tokenService } from '@/services/tokenService';
import './App.css';

function App() {
    const { t } = useTranslation();

    const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
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
        if (isOidcCallback) {
            return;
        }

        const cleanupAuth = bootstrapAuth({
            apiBaseUrl,
            onUserLoaded: (userData) => {
                setCurrentUser(userData);
                setUserError(undefined);
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
    }, [apiBaseUrl, isOidcCallback]);

    // 注意：WebSocket 连接由 Shell.tsx 统一管理，此处不再启动

    // OIDC 回调页面：单独渲染回调组件
    if (isOidcCallback) {
        return <OidcCallback />;
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

function OidcCallback() {
    const { t } = useTranslation();
    const [error, setError] = useState<string>();
    const [message, setMessage] = useState<string>(t('oidc.completingLogin'));

    useEffect(() => {
        if (typeof window === 'undefined') {
            return;
        }

        let cancelled = false;

        const url = new URL(window.location.href);

        // 🌍 从 URL 读取语言参数并同步到前端（用户可能在登录页切换了语言）
        const cultureParam = url.searchParams.get('culture') || url.searchParams.get('ui-culture');
        if (cultureParam && (cultureParam === 'zh' || cultureParam === 'en')) {
            void i18n.changeLanguage(cultureParam);
        }

        const redirectUri = `${window.location.origin}/oidc/callback`;
        const authServerBaseUrl = getAuthBaseUrl();

        const completeLogin = async () => {
            try {
                const tokenSet = await redeemOidcAuthorizationCode({
                    clientId: 'radish-client',
                    authServerBaseUrl,
                    redirectUri,
                    missingCodeMessage: t('oidc.missingCode'),
                    staleCallbackMessage: t('oidc.staleCallback'),
                    missingAccessTokenMessage: t('oidc.missingAccessToken'),
                    buildTokenRequestFailedMessage: ({ status, statusText, error, errorDescription }) => {
                        const baseMessage = t('oidc.tokenRequestFailed', { status, statusText });
                        const detailMessage = errorDescription || error;
                        return detailMessage ? `${baseMessage} (${detailMessage})` : baseMessage;
                    }
                });

                if (cancelled) {
                    return;
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

                const msg = err instanceof Error ? err.message : String(err);
                setError(msg);
                setMessage(t('oidc.loginFailed'));
            }
        };

        void completeLogin();

        return () => {
            cancelled = true;
        };
    }, [t]);

    return (
        <div>
            <h1>{t('oidc.title')}</h1>
            <p>{message}</p>
            {error && <p>{t('oidc.errorDetailPrefix')}{error}</p>}
        </div>
    );
}

export default App;
