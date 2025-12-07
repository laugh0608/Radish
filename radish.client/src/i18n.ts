import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

const resources = {
    en: {
        translation: {
            'app.title': 'Radish Weather Forecast',
            'app.description': 'Display WeatherForecast sample data from Radish.Api to verify connectivity.',

            'auth.sectionTitle': 'Authentication',
            'auth.login': 'Login via OIDC',
            'auth.logout': 'Logout',
            'auth.currentUser': 'Current user: {{userName}} (Id: {{userId}}, Tenant: {{tenantId}})',
            'auth.notLoggedIn': 'Not logged in',
            'auth.userInfoLoadFailedPrefix': 'Failed to load user info: ',

            'weather.loading': 'Loading weather data from Radish.Api...',
            'weather.date': 'Date',
            'weather.tempC': 'Temp. (C)',
            'weather.tempF': 'Temp. (F)',
            'weather.summary': 'Summary',

            'info.weather.load_success': 'Weather forecast loaded successfully.',
            'error.weather.load_failed': 'Failed to load weather data.',

            'oidc.title': 'OIDC Login Callback',
            'oidc.completingLogin': 'Completing login...',
            'oidc.missingCode': 'Missing authorization code.',
            'oidc.loginFailed': 'Login failed',
            'oidc.loginSucceeded': 'Login succeeded, redirecting to home...',
            'oidc.errorDetailPrefix': 'Error detail: ',
            'oidc.missingAccessToken': 'Token response does not contain access_token.',
            'oidc.tokenRequestFailed': 'Token request failed: {{status}} {{statusText}}',

            'lang.zhCN': '中文',
            'lang.en': 'EN'
        }
    },
    'zh-CN': {
        translation: {
            'app.title': 'Radish 天气预报',
            'app.description': '实时展示来自 Radish.Api 的 WeatherForecast 示例数据，便于验证前后端联通性。',

            'auth.sectionTitle': '认证',
            'auth.login': '通过 OIDC 登录',
            'auth.logout': '退出登录',
            'auth.currentUser': '当前用户：{{userName}}（Id: {{userId}}, Tenant: {{tenantId}}）',
            'auth.notLoggedIn': '当前未登录',
            'auth.userInfoLoadFailedPrefix': '用户信息加载失败：',

            'weather.loading': '正在从 Radish.Api 加载天气数据...',
            'weather.date': '日期',
            'weather.tempC': '温度 (℃)',
            'weather.tempF': '温度 (℉)',
            'weather.summary': '摘要',

            'info.weather.load_success': '获取天气预报成功。',
            'error.weather.load_failed': '天气数据加载失败。',

            'oidc.title': 'OIDC 登录回调',
            'oidc.completingLogin': '正在完成登录...',
            'oidc.missingCode': '缺少授权码 code。',
            'oidc.loginFailed': '登录失败',
            'oidc.loginSucceeded': '登录成功，即将跳转到首页...',
            'oidc.errorDetailPrefix': '错误详情：',
            'oidc.missingAccessToken': 'Token 响应中缺少 access_token。',
            'oidc.tokenRequestFailed': 'Token 请求失败: {{status}} {{statusText}}',

            'lang.zhCN': '中文',
            'lang.en': 'EN'
        }
    }
} as const;

i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        resources,
        fallbackLng: 'zh-CN',
        supportedLngs: ['zh-CN', 'en'],
        interpolation: {
            escapeValue: false
        },
        detection: {
            order: ['querystring', 'localStorage', 'navigator'],
            caches: ['localStorage'],
            lookupQuerystring: 'lang',
            lookupLocalStorage: 'radish_lang'
        }
    });

export default i18n;
