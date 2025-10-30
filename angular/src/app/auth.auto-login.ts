import { APP_INITIALIZER, inject } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { ConfigStateService, EnvironmentService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';

function initAutoLoginFactory(oAuthService: OAuthService) {
  return async () => {
    try {
      // 在调用 OAuthService 之前，确保已按环境配置完成（避免 APP_INITIALIZER 顺序竞争导致 issuer 为空）
      try {
        const envService = inject(EnvironmentService);
        const env = envService.getEnvironment?.();
        if (env?.oAuthConfig) {
          oAuthService.configure(env.oAuthConfig);
        }
      } catch {}

      const configState = inject(ConfigStateService);
      // 避免并发重复刷新导致前一个 XHR 被浏览器取消（控制台出现 XHR 加载失败的噪音）
      let refreshing = false;
      let refreshedAfterLogin = false;
      const refreshAppState = () => {
        if (refreshing) return;
        try {
          refreshing = true;
          // 刷新后端 ApplicationConfiguration，更新 currentUser/menus/权限
          configState
            .refreshAppState()
            .pipe(finalize(() => (refreshing = false)))
            .subscribe({
              next: () => {
                refreshedAfterLogin = true;
              },
              error: () => {
                // 忽略错误，避免阻断应用启动
              },
            });
        } catch {
          refreshing = false;
        }
      };

      // 1) 加载发现文档并尝试从回调解析登录（若已从 IdP 重定向回来）
      await oAuthService.loadDiscoveryDocumentAndTryLogin();

      // 2) 若未拿到令牌，则尝试基于已存在的 IdP 会话静默获取（无感知 SSO）
      if (!oAuthService.hasValidAccessToken()) {
        try {
          // noPrompt=true -> 带 prompt=none，已登录则直接签发 code；未登录返回 login_required
          await oAuthService.silentRefresh(undefined, true);
        } catch (err) {
          // 兼容：未登录/需交互(consent_required / login_required / interaction_required)时忽略错误，保持匿名
          // 仅记录调试信息，避免影响首页匿名访问
          console.warn('[auth.auto-login] silent SSO skipped:', err);
        }
      }

      // 3) 若仍未登录且带有 `?sso=auto`（兼容历史 `?sso=true`/`?sso=1`/`?sso=yes`），进行一次顶层 Code Flow 跳转
      //    这样能复用 Host 已登录会话，绕过第三方 Cookie 被拦截导致的 silent SSO 失败
      if (!oAuthService.hasValidAccessToken()) {
        const url = new URL(window.location.href);
        const ssoParam = url.searchParams.get('sso');
        const shouldSso = !!ssoParam && ['auto', '1', 'true', 'yes'].includes(ssoParam.toLowerCase());
        if (shouldSso) {
          // 触发交互式（顶层）授权，IdP 已登录时不会再显示登录页
          oAuthService.initCodeFlow();
          return; // 发生跳转，后续逻辑不再执行
        }
      }

      // 4) 成功获取令牌后，开启自动静默续期（使用 refresh_token 或 prompt=none 视配置而定）
      if (oAuthService.hasValidAccessToken()) {
        oAuthService.setupAutomaticSilentRefresh(undefined, 'access_token', true);
        // 避免与 ABP 内部初始化的 ApplicationConfiguration 请求并发，
        // 不在此处立即刷新，统一在 token_received 事件中进行一次刷新。
      }
      // 5) 监听会话终止（若 IdP 支持 front-channel/logout），则本地也登出
      oAuthService.events.subscribe(e => {
        if ((e as any)?.type === 'token_received') {
          refreshAppState();
        }
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
    deps: [OAuthService, EnvironmentService],
    useFactory: initAutoLoginFactory,
  },
];
