import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44342/',
  redirectUri: baseUrl,
  // 与服务端 OpenIddict 配置一致（见 DbMigrator: OpenIddict:Applications:Radish_Console）
  clientId: 'Radish_Console',
  responseType: 'code',
  scope: 'offline_access Radish',
  requireHttps: true,
  // 静默登录/续期回调页（与开发一致，确保构建后仍可访问）
  silentRefreshRedirectUri: baseUrl + '/assets/silent-refresh.html',
};

export const environment = {
  production: true,
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
  remoteEnv: {
    url: '/getEnvConfig',
    mergeStrategy: 'deepmerge'
  }
} as Environment;
