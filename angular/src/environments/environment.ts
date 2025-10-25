import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44342/',
  redirectUri: baseUrl,
  // 与服务端 OpenIddict 配置一致（见 DbMigrator: OpenIddict:Applications:Radish_Console）
  clientId: 'Radish_Console',
  responseType: 'code',
  scope: 'offline_access Radish',
  // 开发环境允许 HTTP，以便在本地非 TLS 场景下联调
  requireHttps: false,
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
