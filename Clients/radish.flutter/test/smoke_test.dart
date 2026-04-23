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
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
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

    expect(find.text('Restoring session'), findsOneWidget);

    await tester.pump();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('Guest'), findsOneWidget);
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
    expect(find.text('Guest'), findsOneWidget);
    expect(find.text('Sign in'), findsOneWidget);
    expect(tester.takeException(), isNull);
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
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Restored session for user user-42'), findsOneWidget);
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
      find.text('Open @luobo'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open @luobo'));
    await tester.pumpAndSettle();

    expect(find.text('Profile'), findsWidgets);
    expect(find.text('Guest mode is reading public profile user-9'),
        findsOneWidget);
    expect(find.text('User user-9'), findsWidgets);
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

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Guest'), findsNothing);
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

    expect(find.text('Guest'), findsOneWidget);
    expect(find.text('Session expired'), findsOneWidget);
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
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Restored session for user user-88'), findsOneWidget);
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

    expect(find.text('Sign-in needs attention'), findsOneWidget);
    expect(
      find.text(
        'Sign-in was canceled before the browser returned to the app.',
      ),
      findsOneWidget,
    );
    expect(find.text('Retry sign-in'), findsOneWidget);
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

    expect(find.text('Sign-in needs attention'), findsOneWidget);
    expect(
      find.text('Sign-in was canceled from the browser.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Dismiss'));
    await tester.pumpAndSettle();
    expect(find.text('Sign-in needs attention'), findsNothing);
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
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    expect(find.text('Guest boundary'), findsOneWidget);
    await tester.tap(find.text('Sign in with OIDC'));
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

    expect(find.text('Profile'), findsWidgets);
    expect(find.text('Restored session for user user-108'), findsOneWidget);
    expect(find.text('Signed in'), findsOneWidget);
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
    await tester.tap(find.text('Forum'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open detail'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('Sign-in needs attention'), findsOneWidget);
    expect(
      find.text('Sign-in was canceled before the browser returned to the app.'),
      findsOneWidget,
    );

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-code',
      ),
    );
    await tester.tap(find.text('Retry sign-in'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('Browse history handoff'), findsWidgets);
    expect(find.text('Restored session for user user-109'), findsNothing);
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
    await tester.tap(find.text('Forum'));
    await tester.pumpAndSettle();

    expect(find.text('Forum detail handoff'), findsOneWidget);

    await tester.tap(find.text('Open detail'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Forum detail'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('Post body'), findsOneWidget);
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
    await tester.tap(find.text('Forum'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('luobo').first);
    await tester.pumpAndSettle();

    expect(find.text('Profile'), findsWidgets);
    expect(find.text('Guest mode is reading public profile user-9'),
        findsOneWidget);
    expect(find.text('User user-9'), findsWidgets);
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
          initialTitle: 'Forum detail handoff',
          commentId: 'reply-1',
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Forum detail'), findsWidgets);
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
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('Open comment context'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open comment context'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Forum detail'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('Public profile comment handoff'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);
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

    expect(find.text('Forum detail'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('Notification handoff'), findsWidgets);
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
    await tester.tap(find.text('Forum'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open detail'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('Resume forum'), findsOneWidget);
    await tester.tap(find.text('Resume forum'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('Browse history handoff'), findsWidgets);
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
          title: 'Forum detail handoff',
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
      title: 'Forum detail handoff',
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
          title: 'Forum detail handoff',
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
