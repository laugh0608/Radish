import { Environment } from '@abp/ng.core';

// 使用当前 Origin，避免 http/https 不一致导致证书或资源跨协议问题
const origin = (typeof window !== 'undefined' && window.location && window.location.origin) || 'http://localhost:4200';
const baseUrl = origin;

const oAuthConfig = {
  issuer: 'https://localhost:44342/',
  redirectUri: baseUrl,
  // 与服务端 OpenIddict 配置一致（见 DbMigrator: OpenIddict:Applications:Radish_Console）
  clientId: 'Radish_Console',
  responseType: 'code',
  // 至少包含 openid/profile，才能完成 OIDC Code Flow 并基于 Host 会话实现 SSO
  // 其余常见声明按需取用（email/phone/address/roles），后端通过 OpenIddict 自动支持内置范围
  scope: 'offline_access openid profile email phone address roles Radish',
// 本地默认使用 HTTP 开发，发布时通过 Nginx 反代提供 HTTPS。
// 与当前页面协议保持一致：HTTP 开发 => false，生产 HTTPS => true。
  requireHttps: origin.startsWith('https'),
  // 静默登录/续期回调页（需能被独立加载，不依赖主 SPA）
  silentRefreshRedirectUri: baseUrl + '/assets/silent-refresh.html',
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'Radish',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44342',
      rootNamespace: 'Radish',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
