import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from '@/i18n';
import { parseApiResponse, type ApiResponse } from '@/api/client';
import styles from './AuthTestApp.module.css';

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

// 默认通过 Gateway 暴露的 API 入口
const defaultApiBase = 'https://localhost:5000';

/**
 * 认证测试应用
 *
 * 用于测试 OIDC 登录、API 调用和国际化
 */
export const AuthTestApp = () => {
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

    useEffect(() => {
        populateWeatherData();
        populateCurrentUser();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [apiBaseUrl]);

    const contents = forecasts === undefined
        ? <p><em>{error ?? t('weather.loading')}</em></p>
        : <table className={styles.table} aria-labelledby="tableLabel">
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
        <div className={styles.container}>
            <div className={styles.langButtons}>
                <button type="button" onClick={() => i18n.changeLanguage('zh')}>
                    {t('lang.zh')}
                </button>
                <button type="button" onClick={() => i18n.changeLanguage('en')}>
                    {t('lang.en')}
                </button>
            </div>

            <h1 className={styles.title}>{t('app.title')}</h1>
            <p className={styles.description}>{t('app.description')}</p>

            <section className={styles.section}>
                <h2>{t('auth.sectionTitle')}</h2>
                <div className={styles.authSection}>
                    <div className={styles.buttonGroup}>
                        <button type="button" onClick={() => handleLogin(apiBaseUrl)}>
                            {t('auth.login')}
                        </button>
                        {currentUser && (
                            <button type="button" onClick={() => handleLogout(apiBaseUrl)}>
                                退出登录
                            </button>
                        )}
                    </div>
                    <div className={styles.userInfo}>
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
                            <span className={styles.error}>{t('auth.userInfoLoadFailedPrefix')}{userError}</span>
                        )}
                    </div>
                </div>
            </section>

            <section className={styles.section}>
                <h2>天气预报 API 测试</h2>
                {contents}
            </section>
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
};

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
    authorizeUrl.searchParams.set('scope', 'radish-api');

    const currentLanguage = i18n.language || 'zh';
    authorizeUrl.searchParams.set('culture', currentLanguage);
    authorizeUrl.searchParams.set('ui-culture', currentLanguage);

    window.location.href = authorizeUrl.toString();
}

function handleLogout(apiBaseUrl: string) {
    if (typeof window === 'undefined') {
        return;
    }

    window.localStorage.removeItem('access_token');
    window.localStorage.removeItem('refresh_token');

    const currentLanguage = i18n.language || 'zh';
    const logoutUrl = new URL(`${apiBaseUrl}/Account/Logout`);
    logoutUrl.searchParams.set('culture', currentLanguage);

    void fetch(logoutUrl.toString(), {
        method: 'POST',
        credentials: 'include',
        headers: {
            Accept: 'application/json',
            'Accept-Language': currentLanguage
        }
    }).catch(() => {
        // 忽略登出接口错误
    }).finally(() => {
        window.location.replace('/');
    });
}
