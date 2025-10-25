import { inject } from '@angular/core';
import { provideAppInitializer } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

function initAutoLogin() {
  const oAuthService = inject(OAuthService);
  return async () => {
    try {
      // 加载发现文档并尝试从回调解析登录（若已从IDP重定向回来）
      await oAuthService.loadDiscoveryDocumentAndTryLogin();

      // 已经有有效的访问令牌则开启自动刷新，否则自动发起登录
      if (oAuthService.hasValidAccessToken()) {
        // 对于 code + offline_access，库将优先使用 refresh_token 续期
        oAuthService.setupAutomaticSilentRefresh();
      } else {
        // 自动触发登录：若 IDP(Host) 已有会话，将直接签发并回跳，无需再次输入凭据
        oAuthService.initCodeFlow();
      }
    } catch (e) {
      // 保守处理：初始化过程中的错误打印到控制台，避免阻断应用启动
      console.error('[auth.auto-login] init failed', e);
    }
  };
}

export const AUTO_LOGIN_PROVIDER = [
  provideAppInitializer(() => initAutoLogin())
];

