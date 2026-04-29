import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/platform/app_lifecycle_gateway.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_follow_up_store.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/notifications/data/notification_repository.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';

void main() {
  testWidgets('restores into guest shell when no session exists',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    expect(find.text('正在恢复会话'), findsOneWidget);

    await tester.pump();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('游客'), findsOneWidget);
  });

  testWidgets('renders shell status strip on narrow screens without overflow',
      (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('DEVELOPMENT'), findsOneWidget);
    expect(find.text('游客'), findsOneWidget);
    expect(find.text('登录'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('root Android back moves the app task to background',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 1);
    expect(find.text('Radish Flutter'), findsOneWidget);
  });

  testWidgets('restores authenticated session into profile boundary',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('已登录用户 user-42'), findsOneWidget);
  });

  testWidgets('discover handoff opens guest profile target in shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).last;
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开 @luobo'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开 @luobo'));
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('用户 user-9'), findsWidgets);
  });

  testWidgets('recent public profile target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(sessionController);

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _SeededDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _FakeForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开 @luobo'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开 @luobo'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('继续看公开主页'), findsOneWidget);

    await tester.tap(find.text('继续看公开主页'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
  });

  testWidgets('refreshes expired session before entering shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt:
                DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt:
              DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.success(
        AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 2)),
          ),
          refreshToken: 'refresh-token-next',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 2)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('游客'), findsNothing);
  });

  testWidgets('falls back to guest shell when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt:
                DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt:
              DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
        ),
      ),
      refreshService:
          _FakeSessionRefreshService.failure('refresh token expired'),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();

    expect(find.text('游客'), findsOneWidget);
    expect(find.text('会话已失效'), findsOneWidget);
  });

  testWidgets('native OIDC callback redeems a session into the shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(
      sessionController,
      pendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'native-code-1',
      ),
      nextSession: AuthSession(
        accessToken: _buildJwt(
          userId: 'user-88',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
        refreshToken: 'refresh-token',
        userId: 'user-88',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('已登录用户 user-88'), findsOneWidget);
  });

  testWidgets('shows a visible shell notice when browser sign-in is canceled',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(sessionController);

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await authController.startLogin();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text(
        '浏览器返回应用前，登录已取消。',
      ),
      findsOneWidget,
    );
    expect(find.text('重试登录'), findsOneWidget);
  });

  testWidgets('maps access_denied callback into a friendly auth notice',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(
      sessionController,
      pendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        error: 'access_denied',
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text('已在浏览器中取消登录。'),
      findsOneWidget,
    );
    await tester.tap(find.text('关闭'));
    await tester.pumpAndSettle();
    expect(find.text('登录需要处理'), findsNothing);
  });

  testWidgets('profile sign-in returns to the original profile tab',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-108',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-108',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('游客模式'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, '登录'));
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-108'), findsOneWidget);
    expect(find.text('已登录'), findsOneWidget);
  });

  testWidgets('persisted profile sign-in target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-208',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-208',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore();

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _FakeDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _FakeForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, '登录'));
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-rebuild-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-208'), findsOneWidget);
    expect(find.text('已登录'), findsOneWidget);
  });

  testWidgets('retry sign-in resumes forum detail target after cancellation',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-109',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-109',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text('浏览器返回应用前，登录已取消。'),
      findsOneWidget,
    );

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-code',
      ),
    );
    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
    expect(find.text('已登录用户 user-109'), findsNothing);
  });

  testWidgets('persisted forum sign-in target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-209',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-209',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore();

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _FakeDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _SeededBigIdForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-rebuild-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
  });

  testWidgets('forum detail sign-in keeps current detail context',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-210',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-210',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('登录并保留当前位置'), findsOneWidget);

    await tester.tap(find.text('登录并保留当前位置'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(find.text('重试登录'), findsOneWidget);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-detail-login-code',
      ),
    );
    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已登录'), findsWidgets);
    expect(find.text('登录并保留当前位置'), findsNothing);
  });

  testWidgets(
      'notification detail sign-in keeps source and comment target after retry',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-211',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-211',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore(
      initialPendingHandoff: const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: 'Native discover wiring plan',
        commentId: 'comment-big-1',
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _SeededBigIdDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('登录并保留当前位置'), findsOneWidget);

    await tester.tap(find.text('登录并保留当前位置'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('登录需要处理'), findsWidgets);
    expect(find.text('重试登录'), findsWidgets);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'notification-detail-login-code',
      ),
    );
    await tester.tap(find.text('重试登录').first);
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('已登录'), findsWidgets);
    expect(find.text('登录并保留当前位置'), findsNothing);
  });

  testWidgets('forum feed opens native public detail page', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsOneWidget);

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('正文'), findsOneWidget);
  });

  testWidgets('forum author handoff opens native public profile tab',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('luobo').first);
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('用户 user-9'), findsWidgets);
  });

  testWidgets('shell forum handoff opens native detail and targets comment',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        initialForumHandoffTarget: const ForumDetailHandoffTarget(
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          commentId: 'reply-1',
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('First public child comment'), findsOneWidget);
  });

  testWidgets(
      'profile comment handoff reuses shared native forum detail target',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('打开评论上下文'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开评论上下文'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);
  });

  testWidgets('profile post handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('打开帖子'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开帖子'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('profile quick reply handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('回到原帖'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('回到原帖'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('我的轻回应'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('我的轻回应'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets(
      'profile recent browse handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(
          initialRecentBrowseHandoff: const ForumDetailHandoffTarget(
            postId: 'post-42',
            source: ForumDetailHandoffSource.browseHistory,
            initialTitle: '论坛详情回流',
          ),
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('最近阅读'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('继续阅读帖子'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('我的最近阅读'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近阅读'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('discover document handoff returns to discover after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final docsFollowUpStore = InMemoryDocsFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDocumentDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: docsFollowUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);

    Navigator.of(tester.element(find.text('文档详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('发现'), findsWidgets);
    expect(find.text('文档精选'), findsOneWidget);
    expect(find.text('文档详情'), findsNothing);

    final recentTarget = await docsFollowUpStore.readRecentDocumentTarget();
    expect(recentTarget?.slug, 'flutter-docs-scope');
    expect(recentTarget?.source, DocsDetailHandoffSource.browseHistory);
  });

  testWidgets('docs detail link opens another native docs detail route',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDocumentDiscoverRepository(),
        docsRepository: _LinkedDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pumpAndSettle();

    final linkedDocsAction = find.text('公开阅读边界').last;
    await tester.ensureVisible(linkedDocsAction);
    await tester.tap(linkedDocsAction);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：文档内链'), findsOneWidget);
    expect(find.text('Doc public-docs-reading-boundary'), findsWidgets);

    await tester.tap(find.text('返回来源').last);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);
  });

  testWidgets('docs tab search opens native docs detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _SearchableDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('文档'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'boundary');
    await tester.tap(find.text('搜索文档'));
    await tester.pumpAndSettle();

    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('Doc public-docs-reading-boundary'), findsWidgets);
  });

  testWidgets('profile recent document handoff returns to profile after pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(
          initialRecentDocumentTarget: const DocsDetailHandoffTarget(
            slug: 'flutter-docs-scope',
            source: DocsDetailHandoffSource.browseHistory,
            initialTitle: 'Radish Flutter docs scope',
          ),
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final recentDocumentButton =
        find.widgetWithText(FilledButton, '继续阅读文档').last;
    await tester.scrollUntilVisible(
      recentDocumentButton,
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(recentDocumentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('我的最近文档'), findsWidgets);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);

    Navigator.of(tester.element(find.text('文档详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近文档'), findsWidgets);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('文档详情'), findsNothing);
  });

  testWidgets('pending notification handoff opens shared native forum detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore(
      initialPendingHandoff: const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: 'Native discover wiring plan',
      ),
    );
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededBigIdDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
  });

  testWidgets('latest forum notification opens shared native detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: const _FakeForumNotificationRepository(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('查看论坛通知'), findsOneWidget);

    await tester.tap(find.text('查看论坛通知'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
  });

  testWidgets('recent browse handoff resumes forum detail from shell chip',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('继续阅读论坛'), findsOneWidget);
    await tester.tap(find.text('继续阅读论坛'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
  });
}

NativeAuthController _buildAuthController(
  SessionController sessionController, {
  NativeAuthCallbackPayload? pendingCallback,
  AuthSession? nextSession,
  String? exchangeFailureMessage,
}) {
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: InMemoryNativeAuthGateway(
      initialPendingCallback: pendingCallback,
    ),
    exchangeService: _FakeAuthorizationCodeExchangeService(
      nextSession: nextSession,
      failureMessage: exchangeFailureMessage,
    ),
  );
}

class _FakeDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [],
      documents: [],
      products: [],
    );
  }
}

class _SeededDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: 'post-1',
          title: 'Native discover',
          summary: 'Use discover to jump into real tabs.',
          categoryId: 'forum-cat-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
        ),
      ],
      documents: [],
      products: [],
    );
  }
}

class _SeededDocumentDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [],
      documents: [
        DocsDocumentSummary(
          id: 'doc-3001',
          title: 'Radish Flutter docs scope',
          slug: 'flutter-docs-scope',
          summary: 'Read-only docs route handoff.',
          modifyTime: '2026-04-20T08:00:00Z',
        ),
      ],
      products: [],
    );
  }
}

class _SeededBigIdDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Use discover to jump into real tabs.',
          categoryId: 'forum-cat-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
        ),
      ],
      documents: [],
      products: [],
    );
  }
}

class _FakeDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    return const DocsDocumentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      documents: [],
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    return DocsDocumentDetail(
      id: 'doc-$slug',
      title: 'Doc $slug',
      slug: slug,
      markdownContent: '# $slug',
      sourceType: 'Markdown',
      visibility: 1,
      status: 1,
      createTime: '2026-04-20T08:00:00Z',
    );
  }
}

class _SearchableDocsRepository extends _FakeDocsRepository {
  static const _documents = [
    DocsDocumentSummary(
      id: 'doc-flutter-docs-scope',
      title: 'Radish Flutter docs scope',
      slug: 'flutter-docs-scope',
      summary: 'Native Flutter docs wiring.',
    ),
    DocsDocumentSummary(
      id: 'doc-public-docs-reading-boundary',
      title: 'Public docs reading boundary',
      slug: 'public-docs-reading-boundary',
      summary: 'Keep editing outside the native docs search batch.',
    ),
  ];

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    final normalizedKeyword = keyword?.trim().toLowerCase();
    final documents = _documents.where((document) {
      if (normalizedKeyword == null || normalizedKeyword.isEmpty) {
        return true;
      }

      return document.title.toLowerCase().contains(normalizedKeyword) ||
          document.slug.toLowerCase().contains(normalizedKeyword) ||
          (document.summary?.toLowerCase().contains(normalizedKeyword) ??
              false);
    }).toList();

    return DocsDocumentPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: documents.length,
      pageCount: 1,
      documents: documents,
    );
  }
}

class _LinkedDocsRepository extends _FakeDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    if (slug == 'flutter-docs-scope') {
      return DocsDocumentDetail(
        id: 'doc-$slug',
        title: 'Doc $slug',
        slug: slug,
        markdownContent:
            '# $slug\n继续阅读 [公开阅读边界](/docs/public-docs-reading-boundary)',
        sourceType: 'Markdown',
        visibility: 1,
        status: 1,
        createTime: '2026-04-20T08:00:00Z',
      );
    }

    return super.getDocumentDetail(slug: slug);
  }
}

class _FakeForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: 'Post $postId',
      summary: 'Summary for $postId',
      content: '# Post $postId\n\nBody content',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-1',
      authorName: 'tester',
      tagNames: const ['flutter'],
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 0,
      items: [],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T08:13:00Z',
    );
  }
}

class _SeededForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          categoryId: 'category-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
          commentCount: 3,
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: '论坛详情回流',
      summary: 'Tap through to the public native detail page.',
      content:
          '# Native detail\n\n- author metadata\n- body content\n- back navigation',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      tagNames: const ['android', 'flutter'],
      commentCount: 3,
      createTime: '2026-04-20T08:00:00Z',
      updateTime: '2026-04-20T10:30:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    if (pageIndex == 1) {
      return const ForumCommentPage(
        page: 1,
        pageSize: 20,
        dataCount: 2,
        pageCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'comment-1',
            postId: 'post-42',
            content: 'First public root comment',
            authorId: 'user-9',
            authorName: 'luobo',
            likeCount: 2,
            replyCount: 1,
            childrenTotal: 1,
            createTime: '2026-04-20T11:00:00Z',
          ),
          ForumCommentSummary(
            id: 'comment-2',
            postId: 'post-42',
            content: 'Second public root comment',
            authorId: 'user-10',
            authorName: 'reader',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:05:00Z',
          ),
        ],
      );
    }

    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      comments: [],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (parentId == 'comment-1') {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'reply-1',
            postId: 'post-42',
            content: 'First public child comment',
            authorId: 'user-11',
            authorName: 'guest-child',
            parentId: 'comment-1',
            rootId: 'comment-1',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:10:00Z',
          ),
        ],
      );
    }

    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    if (commentId == 'reply-1') {
      return const ForumCommentNavigationLocation(
        commentId: 'reply-1',
        postId: 'post-42',
        rootCommentId: 'comment-1',
        parentCommentId: 'comment-1',
        isRootComment: false,
        rootPageIndex: 1,
        childPageIndex: 1,
      );
    }

    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: 'comment-1',
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 2,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: 'post-42',
          authorId: 'user-9',
          authorName: 'luobo',
          content: '学到了',
          createTime: '2026-04-20T11:20:00Z',
        ),
        ForumQuickReplySummary(
          id: 'quick-2',
          postId: 'post-42',
          authorId: 'user-10',
          authorName: 'reader',
          content: '同感',
          createTime: '2026-04-20T11:25:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T11:30:00Z',
    );
  }
}

class _SeededBigIdForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Connect real summaries without expanding into details.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'luobo',
          commentCount: 6,
          viewCount: 128,
          isEssence: true,
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: 'Native discover wiring plan',
      summary: 'Connect real summaries without expanding into details.',
      content: '# Big id detail\n\nPreserve string ids through native handoff.',
      contentType: 'Markdown',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: '1024',
      authorName: 'luobo',
      commentCount: 6,
      viewCount: 128,
      isEssence: true,
      createTime: '2026-04-18T10:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      comments: [
        ForumCommentSummary(
          id: 'comment-big-1',
          postId: '2042219067430928384',
          content: 'Big id root comment',
          authorId: '2048',
          authorName: 'reader',
          createTime: '2026-04-18T12:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 1,
      items: [
        ForumQuickReplySummary(
          id: 'quick-big-1',
          postId: '2042219067430928384',
          authorId: '2048',
          authorName: 'reader',
          content: '已读',
          createTime: '2026-04-18T12:05:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-big-created',
      postId: postId,
      authorId: 'current-user',
      authorName: 'current',
      content: content,
      createTime: '2026-04-18T12:20:00Z',
    );
  }
}

class _FakeProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 0,
      commentCount: 0,
      totalLikeCount: 0,
      postLikeCount: 0,
      commentLikeCount: 0,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfileCommentPage(
      page: 1,
      pageSize: 3,
      dataCount: 0,
      pageCount: 1,
      comments: [],
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserQuickReplyPage(
      page: 1,
      pageSize: 3,
      total: 0,
      items: [],
    );
  }
}

class _FakeForumNotificationRepository implements NotificationRepository {
  const _FakeForumNotificationRepository();

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const ForumDetailHandoffTarget(
      postId: '2042219067430928384',
      source: ForumDetailHandoffSource.notification,
      initialTitle: '帖子被评论',
      commentId: 'comment-big-1',
    );
  }
}

class _RecordingAppLifecycleGateway implements AppLifecycleGateway {
  int moveTaskToBackCallCount = 0;

  @override
  Future<void> moveTaskToBack() async {
    moveTaskToBackCallCount += 1;
  }
}

class _SeededProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 1,
      commentCount: 1,
      totalLikeCount: 3,
      postLikeCount: 1,
      commentLikeCount: 2,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      posts: [
        PublicProfilePostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          content: 'Tap through to the public native detail page.',
          categoryName: 'General',
          viewCount: 128,
          likeCount: 16,
          commentCount: 3,
          createTime: '2026-04-20T08:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfileCommentPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      comments: [
        PublicProfileCommentSummary(
          id: 'reply-1',
          postId: 'post-42',
          content: 'Recent public comments should stay readable in the shell.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName: 'luobo',
          replyToCommentSnapshot: 'First public root comment',
        ),
      ],
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserQuickReplyPage(
      page: 1,
      pageSize: 3,
      total: 1,
      items: [
        UserQuickReplySummary(
          id: 'quick-42',
          postId: 'post-42',
          postTitle: '论坛详情回流',
          content: '这个回流很好用',
          createTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }
}

class _FakeSessionRefreshService extends SessionRefreshService {
  _FakeSessionRefreshService.missing()
      : _nextSession = null,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.success(AuthSession nextSession)
      : _nextSession = nextSession,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.failure(String failureMessage)
      : _nextSession = null,
        _failureMessage = failureMessage,
        super(environment: const AppEnvironment.development());

  final AuthSession? _nextSession;
  final String? _failureMessage;

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    final failureMessage = _failureMessage;
    if (failureMessage != null) {
      throw SessionRefreshException(failureMessage);
    }

    final nextSession = _nextSession;
    if (nextSession != null) {
      return nextSession;
    }

    return session;
  }
}

class _FakeAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  const _FakeAuthorizationCodeExchangeService({
    this.nextSession,
    this.failureMessage,
  });

  final AuthSession? nextSession;
  final String? failureMessage;

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
  }) async {
    final failureMessage = this.failureMessage;
    if (failureMessage != null) {
      throw AuthorizationCodeExchangeException(failureMessage);
    }

    final nextSession = this.nextSession;
    if (nextSession == null) {
      throw const AuthorizationCodeExchangeException(
        'No fake authorization-code session was configured.',
      );
    }

    return nextSession;
  }
}

String _buildJwt({
  required String userId,
  required DateTime expiresAt,
}) {
  final header = base64Url.encode(utf8.encode('{"alg":"none","typ":"JWT"}'));
  final payload = base64Url.encode(
    utf8.encode(
      '{"sub":"$userId","exp":${expiresAt.toUtc().millisecondsSinceEpoch ~/ 1000}}',
    ),
  );
  return '$header.$payload.signature';
}
