import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/profile/presentation/profile_page.dart';

void main() {
  testWidgets('renders guest profile boundary without loading a target', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _NoopSessionRefreshService(),
    );
    final authController = _buildAuthController(sessionController);
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    expect(find.text('Guest boundary'), findsOneWidget);
    expect(find.text('Loading public profile...'), findsNothing);
  });

  testWidgets('renders public profile, stats, posts, and comments', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
    );
    final authController = _buildAuthController(sessionController);
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    expect(find.text('@luobo'), findsOneWidget);
    expect(find.text('Public activity'), findsOneWidget);
    expect(find.text('Posts'), findsOneWidget);
    expect(find.text('Comments'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Recent public posts'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('Recent public posts'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Recent public comments'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('Recent public comments'), findsOneWidget);
    expect(find.text('Reply to @radish'), findsOneWidget);
  });

  testWidgets('renders guest-selected public profile target', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _NoopSessionRefreshService(),
    );
    final authController = _buildAuthController(sessionController);
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
          guestUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Recent public posts'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('Recent public posts'), findsOneWidget);
  });

  testWidgets(
      'opens shared forum handoff targets from profile post and comment',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final openedTargets = <ForumDetailHandoffTarget>[];
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
    );
    final authController = _buildAuthController(sessionController);
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('Open post'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open post'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.first.postId, 'post-1');
    expect(
      openedTargets.first.source,
      ForumDetailHandoffSource.publicProfilePost,
    );

    await tester.scrollUntilVisible(
      find.text('Open comment context'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open comment context'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(2));
    expect(openedTargets.last.postId, 'post-1');
    expect(openedTargets.last.commentId, 'comment-1');
    expect(
      openedTargets.last.source,
      ForumDetailHandoffSource.publicProfileComment,
    );
  });

  testWidgets('renders profile error state when repository fails', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
    );
    final authController = _buildAuthController(sessionController);
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _FailingProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Public profile unavailable'), findsOneWidget);
    expect(find.text('Profile API is unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });

  testWidgets('guest profile can start the native sign-in flow',
      (tester) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _NoopSessionRefreshService(),
    );
    final authGateway = InMemoryNativeAuthGateway();
    final authController = _buildAuthController(
      sessionController,
      gateway: authGateway,
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    await tester.tap(find.text('Sign in with OIDC'));
    await tester.pump();

    final authorizeUri = authGateway.lastAuthorizeUri;
    expect(authorizeUri, isNotNull);
    expect(authorizeUri!.path, '/connect/authorize');
    expect(authorizeUri.queryParameters['client_id'], 'radish-client');
    expect(
        authorizeUri.queryParameters['redirect_uri'], 'radish://oidc/callback');
    expect(
      authorizeUri.queryParameters['scope'],
      'openid profile offline_access radish-api',
    );
  });

  testWidgets('authenticated profile can sign out through native OIDC flow', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
    );
    final authGateway = InMemoryNativeAuthGateway();
    final authController = _buildAuthController(
      sessionController,
      gateway: authGateway,
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          authController: authController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign out'));
    await tester.pump();

    final logoutUri = authGateway.lastLogoutUri;
    expect(logoutUri, isNotNull);
    expect(logoutUri!.path, '/connect/endsession');
    expect(
      logoutUri.queryParameters['post_logout_redirect_uri'],
      'radish://oidc/logout-complete',
    );
    expect(sessionController.state.isAnonymous, isTrue);
  });
}

NativeAuthController _buildAuthController(
  SessionController sessionController, {
  InMemoryNativeAuthGateway? gateway,
  AuthSession? nextSession,
  String? exchangeFailureMessage,
}) {
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: gateway ?? InMemoryNativeAuthGateway(),
    exchangeService: _FakeAuthorizationCodeExchangeService(
      nextSession: nextSession,
      failureMessage: exchangeFailureMessage,
    ),
  );
}

class _SuccessProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'luobo',
      displayName: 'Radish Author',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 12,
      commentCount: 28,
      totalLikeCount: 96,
      postLikeCount: 54,
      commentLikeCount: 42,
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
          id: 'post-1',
          title: 'Native profile follow-up',
          summary: 'Expand the public profile beyond a single info card.',
          content: 'Expand the public profile beyond a single info card.',
          categoryName: 'Engineering',
          viewCount: 128,
          likeCount: 16,
          commentCount: 6,
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
          id: 'comment-1',
          postId: 'post-1',
          content: 'Recent public comments should stay readable in the shell.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName: 'radish',
          replyToCommentSnapshot: 'public profile preview',
        ),
      ],
    );
  }
}

class _FailingProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    throw const RadishApiClientException('Profile API is unreachable');
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) {
    throw const RadishApiClientException('Profile API is unreachable');
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('Profile API is unreachable');
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('Profile API is unreachable');
  }
}

class _NoopSessionRefreshService extends SessionRefreshService {
  _NoopSessionRefreshService()
      : super(environment: const AppEnvironment.development());

  @override
  Future<AuthSession> refresh(AuthSession session) async {
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
