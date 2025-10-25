import { APP_INITIALIZER } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

function initAutoLoginFactory(oAuthService: OAuthService) {
  return async () => {
    try {
      // 加载发现文档并尝试从回调解析登录（若已从 IDP 重定向回来）
      await oAuthService.loadDiscoveryDocumentAndTryLogin();
      // 若已认证则启用自动刷新；未认证则不跳转登录，保持匿名访问
      if (oAuthService.hasValidAccessToken()) {
        oAuthService.setupAutomaticSilentRefresh();
      }
      // 监听会话终止（若 IdP 支持 front-channel/logout），则本地也登出
      oAuthService.events.subscribe(e => {
        if ((e as any)?.type === 'session_terminated') {
          oAuthService.logOut();
        }
      });
    } catch (e) {
      // 保守处理：初始化过程中的错误打印到控制台，避免阻断应用启动
      console.error('[auth.auto-login] init failed', e);
    }
  };
}

export const AUTO_LOGIN_PROVIDER = [
  {
    provide: APP_INITIALIZER,
    multi: true,
    deps: [OAuthService],
    useFactory: initAutoLoginFactory,
  },
];
