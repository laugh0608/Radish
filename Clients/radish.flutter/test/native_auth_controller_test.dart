import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';

void main() {
  test('native OIDC 使用 state 与 PKCE S256、提交 verifier 并拒绝重放', () async {
    final gateway = InMemoryNativeAuthGateway();
    final exchangeService = _RecordingAuthorizationCodeExchangeService();
    final sessionController = _buildSessionController();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: exchangeService,
      sessionController: sessionController,
    );

    await controller.startLogin();

    final authorizeUri = gateway.lastAuthorizeUri!;
    final state = authorizeUri.queryParameters['state'];
    final challenge = authorizeUri.queryParameters['code_challenge'];
    expect(state, isNotNull);
    expect(state, hasLength(43));
    expect(challenge, isNotNull);
    expect(authorizeUri.queryParameters['code_challenge_method'], 'S256');

    gateway.setPendingCallback(NativeAuthCallbackPayload(
      type: NativeAuthCallbackType.login,
      code: 'native-code',
      state: state,
    ));
    await controller.consumePendingCallback();

    final codeVerifier = exchangeService.lastCodeVerifier;
    expect(codeVerifier, isNotNull);
    expect(codeVerifier, hasLength(43));
    expect(
      challenge,
      base64Url
          .encode(sha256.convert(utf8.encode(codeVerifier!)).bytes)
          .replaceAll('=', ''),
    );
    expect(exchangeService.lastCode, 'native-code');
    expect(exchangeService.redeemCount, 1);
    expect(sessionController.state.isAuthenticated, isTrue);

    gateway.setPendingCallback(NativeAuthCallbackPayload(
      type: NativeAuthCallbackType.login,
      code: 'native-code',
      state: state,
    ));
    await controller.consumePendingCallback();

    expect(exchangeService.redeemCount, 1);
    expect(controller.state.lastErrorMessage, '找不到对应的登录尝试，或登录尝试已经过期。');
  });

  test('native OIDC state 不匹配时 fail closed 且不兑换授权码', () async {
    final gateway = InMemoryNativeAuthGateway();
    final exchangeService = _RecordingAuthorizationCodeExchangeService();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: exchangeService,
      sessionController: _buildSessionController(),
    );

    await controller.startLogin();
    gateway.setPendingCallback(const NativeAuthCallbackPayload(
      type: NativeAuthCallbackType.login,
      code: 'native-code',
      state: 'wrong-state',
    ));
    await controller.consumePendingCallback();

    expect(exchangeService.lastCode, isNull);
    expect(controller.state.lastErrorMessage, '登录回调校验失败，请重新登录。');
  });

  test('native OIDC 超过十五分钟的登录尝试 fail closed', () async {
    final gateway = InMemoryNativeAuthGateway(
      initialPendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'native-code',
        state: 'expired-state',
      ),
      initialAuthorizationAttempt: NativeOidcAuthorizationAttempt(
        state: 'expired-state',
        codeVerifier: List.filled(43, 'v').join(),
        redirectUri: const AppEnvironment.development().nativeOidcRedirectUri,
        startedAt: DateTime.now().toUtc().subtract(const Duration(minutes: 16)),
      ),
    );
    final exchangeService = _RecordingAuthorizationCodeExchangeService();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: exchangeService,
      sessionController: _buildSessionController(),
    );

    await controller.consumePendingCallback();

    expect(exchangeService.redeemCount, 0);
    expect(controller.state.lastErrorMessage, '找不到对应的登录尝试，或登录尝试已经过期。');
  });

  test('native OIDC 本地证书中断七分四十六秒后合法 callback 仍可完成兑换', () async {
    const state = 'delayed-valid-state';
    final gateway = InMemoryNativeAuthGateway(
      initialPendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'delayed-native-code',
        state: state,
      ),
      initialAuthorizationAttempt: NativeOidcAuthorizationAttempt(
        state: state,
        codeVerifier: List.filled(43, 'v').join(),
        redirectUri: const AppEnvironment.development().nativeOidcRedirectUri,
        startedAt: DateTime.now()
            .toUtc()
            .subtract(const Duration(minutes: 7, seconds: 46)),
      ),
    );
    final exchangeService = _RecordingAuthorizationCodeExchangeService();
    final sessionController = _buildSessionController();
    final recreatedController = _buildController(
      gateway: gateway,
      exchangeService: exchangeService,
      sessionController: sessionController,
    );

    // Chrome 的本地证书页先触发 resumed；Activity / Flutter owner 重建后，
    // 合法 callback 才到达；现场约 7 分 46 秒仍在客户端十五分钟 TTL 内。
    await recreatedController.consumePendingCallback();

    expect(exchangeService.redeemCount, 1);
    expect(recreatedController.state.lastErrorMessage, isNull);
    expect(sessionController.state.isAuthenticated, isTrue);
  });

  test('native OIDC 授权错误使用稳定映射而不展示外部 description', () async {
    final gateway = InMemoryNativeAuthGateway();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: _RecordingAuthorizationCodeExchangeService(),
      sessionController: _buildSessionController(),
    );

    await controller.startLogin();
    gateway.setPendingCallback(NativeAuthCallbackPayload(
      type: NativeAuthCallbackType.login,
      state: gateway.lastAuthorizeUri!.queryParameters['state'],
      error: 'access_denied',
      errorDescription: 'Untrusted browser description',
    ));
    await controller.consumePendingCallback();

    expect(controller.state.lastErrorMessage, '已在浏览器中取消登录。');
  });

  test('native OIDC 未知授权错误不直接展示外部错误码', () async {
    final gateway = InMemoryNativeAuthGateway();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: _RecordingAuthorizationCodeExchangeService(),
      sessionController: _buildSessionController(),
    );

    await controller.startLogin();
    gateway.setPendingCallback(NativeAuthCallbackPayload(
      type: NativeAuthCallbackType.login,
      state: gateway.lastAuthorizeUri!.queryParameters['state'],
      error: 'untrusted_custom_error',
    ));
    await controller.consumePendingCallback();

    expect(controller.state.lastErrorMessage, '浏览器登录未完成，请重新登录。');
  });

  test('logout clears the local session when browser logout fails', () async {
    final gateway = _LogoutFailureGateway(failBrowserLogout: true);
    final sessionController = await _buildAuthenticatedSessionController();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: _RecordingAuthorizationCodeExchangeService(),
      sessionController: sessionController,
    );

    await controller.startLogout();

    expect(sessionController.state.isAnonymous, isTrue);
    expect(
      controller.state.lastErrorMessage,
      '本地登录已退出，但无法打开浏览器退出流程。',
    );
  });

  test('logout retries attempt cleanup without blocking local logout',
      () async {
    final gateway = _LogoutFailureGateway(failFirstAttemptCleanup: true);
    final sessionController = await _buildAuthenticatedSessionController();
    final controller = _buildController(
      gateway: gateway,
      exchangeService: _RecordingAuthorizationCodeExchangeService(),
      sessionController: sessionController,
    );

    await controller.startLogout();

    expect(gateway.attemptCleanupCount, 2);
    expect(gateway.lastLogoutUri, isNotNull);
    expect(sessionController.state.isAnonymous, isTrue);
    expect(controller.state.lastErrorMessage, isNull);
  });
}

SessionController _buildSessionController() {
  return SessionController(
    sessionStore: InMemorySessionStore(),
    refreshService: const SessionRefreshService(
      environment: AppEnvironment.development(),
    ),
  );
}

Future<SessionController> _buildAuthenticatedSessionController() async {
  final controller = SessionController(
    sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      userId: 'user-1',
      expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
    )),
    refreshService: const SessionRefreshService(
      environment: AppEnvironment.development(),
    ),
  );
  await controller.restore();
  return controller;
}

NativeAuthController _buildController({
  required NativeAuthGateway gateway,
  required AuthorizationCodeExchangeService exchangeService,
  required SessionController sessionController,
}) {
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: gateway,
    exchangeService: exchangeService,
  );
}

class _LogoutFailureGateway extends InMemoryNativeAuthGateway {
  _LogoutFailureGateway({
    this.failBrowserLogout = false,
    this.failFirstAttemptCleanup = false,
  });

  final bool failBrowserLogout;
  final bool failFirstAttemptCleanup;
  int attemptCleanupCount = 0;

  @override
  Future<void> clearAuthorizationAttempt() async {
    attemptCleanupCount += 1;
    if (failFirstAttemptCleanup && attemptCleanupCount == 1) {
      throw StateError('attempt cleanup unavailable');
    }
    await super.clearAuthorizationAttempt();
  }

  @override
  Future<void> openLogoutUrl(Uri logoutUri) async {
    await super.openLogoutUrl(logoutUri);
    if (failBrowserLogout) {
      throw StateError('browser unavailable');
    }
  }
}

class _RecordingAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  String? lastCode;
  String? lastCodeVerifier;
  int redeemCount = 0;

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
    required String codeVerifier,
  }) async {
    redeemCount += 1;
    lastCode = code;
    lastCodeVerifier = codeVerifier;
    return AuthSession(
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      userId: 'user-1',
      expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
    );
  }
}
