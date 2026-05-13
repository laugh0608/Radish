import 'dart:async';
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
import 'package:radish_flutter/features/docs/data/docs_models.dart';
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

    expect(find.text('游客模式'), findsOneWidget);
    expect(find.text('正在加载公开资料...'), findsNothing);
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
    expect(find.text('公开动态'), findsOneWidget);
    expect(find.text('帖子'), findsOneWidget);
    expect(find.text('评论'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('最近公开帖子'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsWidgets);

    await tester.scrollUntilVisible(
      find.text('最近公开评论'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('最近公开评论'), findsOneWidget);
    expect(find.text('回复 @radish'), findsOneWidget);
  });

  testWidgets('keeps long profile text constrained on narrow screens', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: _longUserId,
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: _longUserId,
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
          repository: _LongTextProfileRepository(),
          recentBrowseHandoffTargets: const [
            ForumDetailHandoffTarget(
              postId: _longPostId,
              commentId: _longCommentId,
              source: ForumDetailHandoffSource.browseHistory,
              initialTitle: _longRecentBrowseTitle,
            ),
          ],
          recentDocumentTargets: const [
            DocsDetailHandoffTarget(
              slug: _longDocsSlug,
              source: DocsDetailHandoffSource.browseHistory,
              initialTitle: _longDocsTitle,
            ),
          ],
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text(_longDisplayName), findsOneWidget);
    expect(find.text('@$_longUserName'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('/docs/$_longDocsSlug'),
      200,
      scrollable: scrollable,
    );
    expect(tester.takeException(), isNull);
    expect(find.text('/docs/$_longDocsSlug'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('帖子 $_longPostId · 评论 $_longCommentId'),
      200,
      scrollable: scrollable,
    );
    expect(tester.takeException(), isNull);
    expect(find.text('帖子 $_longPostId · 评论 $_longCommentId'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text(_longPostTitle),
      200,
      scrollable: scrollable,
    );
    expect(tester.takeException(), isNull);
    expect(find.text(_longPostTitle), findsOneWidget);
    expect(find.text(_longCategoryName), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text(_longCommentSnapshot),
      200,
      scrollable: scrollable,
    );
    expect(tester.takeException(), isNull);
    expect(find.text(_longCommentSnapshot), findsOneWidget);
  });

  testWidgets('keeps public profile visible while refresh is pending', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PendingRefreshProfileRepository();
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
          repository: repository,
          publicUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);

    repository.refreshCompleter = Completer<PublicProfileSummary>();
    await tester.tap(find.text('刷新资料'));
    await tester.pump();

    expect(find.text('正在刷新公开资料，当前仍展示上次可用内容。'), findsOneWidget);
    expect(find.text('Radish Author'), findsOneWidget);
    expect(find.text('正在刷新'), findsOneWidget);

    repository.refreshCompleter!.complete(_updatedProfileSummary('guest-42'));
    await tester.pumpAndSettle();

    expect(find.text('正在刷新公开资料，当前仍展示上次可用内容。'), findsNothing);
    expect(find.text('Updated Radish Author'), findsOneWidget);
  });

  testWidgets('keeps public profile visible when refresh fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

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
          repository: _RefreshFailingProfileRepository(),
          publicUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);

    await tester.tap(find.text('刷新资料'));
    await tester.pumpAndSettle();

    expect(find.text('刷新资料失败'), findsOneWidget);
    expect(find.text('公开资料刷新服务暂时不可用'), findsOneWidget);
    expect(find.text('Radish Author'), findsOneWidget);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
  });

  testWidgets('clears profile refresh issue after successful refresh', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

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
          repository: _FailThenRecoverProfileRepository(),
          publicUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('刷新资料'));
    await tester.pumpAndSettle();

    expect(find.text('刷新资料失败'), findsOneWidget);

    await tester.tap(find.text('刷新资料'));
    await tester.pumpAndSettle();

    expect(find.text('刷新资料失败'), findsNothing);
    expect(find.text('Updated Radish Author'), findsOneWidget);
  });

  testWidgets('renders empty revisit sections on my profile', (tester) async {
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

    expect(find.text('最近复访'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('暂无最近文档。'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('暂无最近文档。'), findsOneWidget);
    expect(
      find.text('打开公开文档后，这里会保留最多 5 条最近文档。'),
      findsOneWidget,
    );
    await tester.scrollUntilVisible(
      find.text('暂无最近阅读。'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('暂无最近阅读。'), findsOneWidget);
    expect(
      find.text('打开论坛帖子后，这里会保留最多 5 条最近阅读上下文。'),
      findsOneWidget,
    );
  });

  testWidgets('uses first-person empty public activity copy on my profile', (
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
          repository: _EmptyPublicActivityProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('你还没有可公开展示的帖子。'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('你还没有可公开展示的帖子。'), findsOneWidget);
    expect(find.text('公开发布的帖子会在这里形成只读回看入口。'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('你还没有可公开展示的评论。'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('你还没有可公开展示的评论。'), findsOneWidget);
    expect(find.text('公开评论会在这里形成只读回看入口。'), findsOneWidget);
    expect(find.text('这个用户暂无公开帖子。'), findsNothing);
    expect(find.text('这个用户暂无公开评论。'), findsNothing);
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
          publicUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('最近公开帖子'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('最近公开帖子'), findsOneWidget);
  });

  testWidgets('renders recent public profile revisit action', (tester) async {
    var openedRecentProfile = false;
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
          recentPublicUserId: 'recent-user-1',
          onOpenRecentPublicProfile: () {
            openedRecentProfile = true;
          },
        ),
      ),
    );

    expect(find.text('游客模式'), findsOneWidget);
    expect(find.text('继续看公开主页'), findsOneWidget);

    await tester.tap(find.text('继续看公开主页'));
    await tester.pump();

    expect(openedRecentProfile, isTrue);
  });

  testWidgets('authenticated public profile can return to my profile', (
    tester,
  ) async {
    var openedMyProfile = false;
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
          publicUserId: 'public-user-2',
          onOpenMyProfile: () {
            openedMyProfile = true;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('公开主页'), findsOneWidget);
    expect(find.text('正在阅读公开主页 public-user-2'), findsOneWidget);
    expect(find.text('回到我的主页'), findsOneWidget);

    await tester.tap(find.text('回到我的主页'));
    await tester.pump();

    expect(openedMyProfile, isTrue);
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
      find.text('打开帖子'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开帖子'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.first.postId, 'post-1');
    expect(
      openedTargets.first.source,
      ForumDetailHandoffSource.publicProfilePost,
    );

    await tester.scrollUntilVisible(
      find.text('打开评论上下文'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开评论上下文'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(2));
    expect(openedTargets.last.postId, 'post-1');
    expect(openedTargets.last.commentId, 'comment-1');
    expect(
      openedTargets.last.source,
      ForumDetailHandoffSource.publicProfileComment,
    );
  });

  testWidgets('opens recent browse target from my profile', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
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
          recentBrowseHandoffTarget: const ForumDetailHandoffTarget(
            postId: 'post-1',
            source: ForumDetailHandoffSource.browseHistory,
            initialTitle: 'Native profile follow-up',
            commentId: 'comment-1',
          ),
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('最近阅读'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('最近阅读'), findsOneWidget);
    expect(find.text('继续回到上次打开的评论上下文。'), findsOneWidget);

    await tester.tap(find.text('继续阅读帖子'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-1');
    expect(openedTargets.single.commentId, 'comment-1');
    expect(
      openedTargets.single.source,
      ForumDetailHandoffSource.profileRecentBrowse,
    );
  });

  testWidgets('renders multiple recent browse targets from my profile', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
          recentBrowseHandoffTargets: const [
            ForumDetailHandoffTarget(
              postId: 'post-2',
              source: ForumDetailHandoffSource.browseHistory,
              initialTitle: 'Second forum read',
            ),
            ForumDetailHandoffTarget(
              postId: 'post-1',
              source: ForumDetailHandoffSource.browseHistory,
              initialTitle: 'First comment read',
              commentId: 'comment-1',
            ),
          ],
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('最近阅读'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('Second forum read'), findsOneWidget);
    expect(find.text('First comment read'), findsOneWidget);
    expect(find.text('帖子 post-1 · 评论 comment-1'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '继续阅读帖子').last);
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-1');
    expect(openedTargets.single.commentId, 'comment-1');
    expect(
      openedTargets.single.source,
      ForumDetailHandoffSource.profileRecentBrowse,
    );
  });

  testWidgets('opens recent document target from my profile', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final openedTargets = <DocsDetailHandoffTarget>[];
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
          recentDocumentTarget: const DocsDetailHandoffTarget(
            slug: 'flutter-docs-scope',
            source: DocsDetailHandoffSource.browseHistory,
            initialTitle: 'Radish Flutter docs scope',
          ),
          onOpenDocsDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    final recentDocumentButton =
        find.widgetWithText(FilledButton, '继续阅读文档').last;
    await tester.scrollUntilVisible(
      recentDocumentButton,
      200,
      scrollable: scrollable,
    );
    expect(find.text('最近文档'), findsWidgets);
    expect(find.text('/docs/flutter-docs-scope'), findsOneWidget);

    await tester.tap(recentDocumentButton);
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.slug, 'flutter-docs-scope');
    expect(
      openedTargets.single.source,
      DocsDetailHandoffSource.profileRecentDocument,
    );
  });

  testWidgets('renders multiple recent document targets from my profile', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;
    final openedTargets = <DocsDetailHandoffTarget>[];
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
          recentDocumentTargets: const [
            DocsDetailHandoffTarget(
              slug: 'public-docs-reading-boundary',
              source: DocsDetailHandoffSource.browseHistory,
              initialTitle: 'Public docs reading boundary',
            ),
            DocsDetailHandoffTarget(
              slug: 'flutter-docs-scope',
              source: DocsDetailHandoffSource.browseHistory,
              initialTitle: 'Radish Flutter docs scope',
            ),
          ],
          onOpenDocsDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('Public docs reading boundary'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('Public docs reading boundary'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('/docs/flutter-docs-scope'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '继续阅读文档').last);
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.slug, 'flutter-docs-scope');
    expect(
      openedTargets.single.source,
      DocsDetailHandoffSource.profileRecentDocument,
    );
  });

  testWidgets('does not render recent browse target on public profile', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

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
          publicUserId: 'public-user-2',
          recentBrowseHandoffTarget: const ForumDetailHandoffTarget(
            postId: 'post-1',
            source: ForumDetailHandoffSource.browseHistory,
            initialTitle: 'Native profile follow-up',
          ),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('公开主页'), findsOneWidget);
    expect(find.text('最近阅读'), findsNothing);
    expect(find.text('最近文档'), findsNothing);
  });

  testWidgets('loads more public posts and opens appended post handoff', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PagedPostProfileRepository();
    final scrollable = find.byType(Scrollable).first;
    final openedTargets = <ForumDetailHandoffTarget>[];
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
          repository: repository,
          publicUserId: 'public-user-2',
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多帖子'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('已显示 3 / 4 条帖子'), findsOneWidget);

    await tester.tap(find.text('加载更多帖子'));
    await tester.pumpAndSettle();

    expect(repository.postPages, [1, 2]);
    expect(find.text('第四篇公开帖子'), findsOneWidget);
    expect(find.text('加载更多帖子'), findsNothing);

    await tester.scrollUntilVisible(
      find.text('打开帖子').last,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开帖子').last);
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-page-4');
    expect(openedTargets.single.initialTitle, '第四篇公开帖子');
    expect(
      openedTargets.single.source,
      ForumDetailHandoffSource.publicProfilePost,
    );
  });

  testWidgets('keeps loaded public posts when loading more fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
          repository: _PagedPostLoadMoreFailingProfileRepository(),
          publicUserId: 'public-user-2',
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多帖子'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('加载更多帖子'));
    await tester.pumpAndSettle();

    expect(find.text('第一页公开帖子 1'), findsOneWidget);
    expect(find.text('加载更多公开帖子失败'), findsOneWidget);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
  });

  testWidgets('loads more public comments and opens appended comment handoff', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PagedCommentProfileRepository();
    final scrollable = find.byType(Scrollable).first;
    final openedTargets = <ForumDetailHandoffTarget>[];
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
          repository: repository,
          publicUserId: 'public-user-2',
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多评论'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('已显示 3 / 4 条评论'), findsOneWidget);

    await tester.tap(find.text('加载更多评论'));
    await tester.pumpAndSettle();

    expect(repository.commentPages, [1, 2]);
    expect(find.text('第四条公开评论上下文'), findsOneWidget);
    expect(find.text('加载更多评论'), findsNothing);

    await tester.scrollUntilVisible(
      find.text('打开评论上下文').last,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开评论上下文').last);
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-4');
    expect(openedTargets.single.commentId, 'comment-page-4');
    expect(
      openedTargets.single.source,
      ForumDetailHandoffSource.publicProfileComment,
    );
  });

  testWidgets('keeps loaded public comments when loading more fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
          repository: _PagedCommentLoadMoreFailingProfileRepository(),
          publicUserId: 'public-user-2',
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多评论'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('加载更多评论'));
    await tester.pumpAndSettle();

    expect(find.text('第一页公开评论 1'), findsOneWidget);
    expect(find.text('加载更多公开评论失败'), findsOneWidget);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
  });

  testWidgets('renders my quick replies and opens their forum handoff', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2400);
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
      find.text('我的轻回应'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('我的轻回应'), findsOneWidget);
    expect(find.text('这个原生回看入口不错'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsWidgets);
    expect(find.text('帖子 post-1 · 轻回应 quick-1'), findsOneWidget);
    expect(find.text('原帖回流'), findsOneWidget);

    await tester.tap(find.text('回到原帖'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-1');
    expect(openedTargets.single.initialTitle, 'Native profile follow-up');
    expect(openedTargets.single.source, ForumDetailHandoffSource.myQuickReply);
  });

  testWidgets('loads more my quick replies without leaving profile', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PagedQuickReplyProfileRepository();
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
          repository: repository,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多轻回应'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('已显示 3 / 4 条轻回应'), findsOneWidget);

    await tester.tap(find.text('加载更多轻回应'));
    await tester.pumpAndSettle();

    expect(repository.quickReplyPages, [1, 2]);
    expect(find.text('第四条回看上下文'), findsOneWidget);
    expect(find.text('加载更多轻回应'), findsNothing);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
  });

  testWidgets('keeps loaded my quick replies when loading more fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
          repository: _PagedQuickReplyLoadMoreFailingProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('加载更多轻回应'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('加载更多轻回应'));
    await tester.pumpAndSettle();

    expect(find.text('第一页轻回应 1'), findsOneWidget);
    expect(find.text('加载更多轻回应失败'), findsOneWidget);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
  });

  testWidgets('does not render my quick replies on public profile target', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

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
          publicUserId: 'public-user-2',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('公开主页'), findsOneWidget);
    expect(find.text('我的轻回应'), findsNothing);
  });

  testWidgets('keeps profile ready when my quick replies fail', (
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
          repository: _QuickReplyFailingProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('我的轻回应'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('我的轻回应'), findsOneWidget);
    expect(find.text('轻回应服务暂时不可用'), findsOneWidget);
    expect(find.text('暂时无法加载公开资料'), findsNothing);
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

    expect(find.text('暂时无法加载公开资料'), findsOneWidget);
    expect(find.text('公开资料服务暂时不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
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

    await tester.tap(find.text('登录'));
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
    await tester.tap(find.text('退出登录'));
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

const _longUserId =
    'user-2042219067430928384-extra-long-public-profile-identifier-for-narrow-screen';
const _longUserName =
    'radish_native_profile_reader_with_a_very_long_public_handle_for_narrow_screen';
const _longDisplayName =
    'Radish Native Profile Reader With A Very Long Display Name For Narrow Screens';
const _longDocsSlug =
    'flutter-native-profile-long-document-slug-that-should-not-break-the-narrow-profile-card-layout';
const _longDocsTitle =
    'Flutter native profile long document title that should stay constrained';
const _longPostId =
    'post-2042219067430928384-extra-long-public-id-for-profile-preview';
const _longCommentId =
    'comment-2042219067430928384-extra-long-public-id-for-profile-preview';
const _longRecentBrowseTitle =
    'A very long recent forum reading title that should stay inside the profile card';
const _longPostTitle =
    'A very long public post title that should stay readable without pushing the profile page wider';
const _longPostSummary =
    'This public post summary is intentionally long so the native profile card can prove it keeps text constrained on narrow screens.';
const _longCategoryName =
    'extremely-long-category-name-for-profile-preview-chip';
const _longCommentSnapshot =
    'A very long quoted comment snapshot that should be constrained inside the profile comment chip on narrow screens';

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
          id: 'quick-1',
          postId: 'post-1',
          postTitle: 'Native profile follow-up',
          content: '这个原生回看入口不错',
          createTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }
}

class _LongTextProfileRepository extends _SuccessProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return const PublicProfileSummary(
      userId: _longUserId,
      userName: _longUserName,
      displayName: _longDisplayName,
      createTime: '2026-04-20T08:00:00Z',
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
          id: _longPostId,
          title: _longPostTitle,
          summary: _longPostSummary,
          content: _longPostSummary,
          categoryName: _longCategoryName,
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
          id: _longCommentId,
          postId: _longPostId,
          content:
              'This long public comment should stay inside the profile preview tile on narrow screens.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName:
              'reply_target_with_a_very_long_public_name_for_profile_preview',
          replyToCommentSnapshot: _longCommentSnapshot,
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
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }
}

class _QuickReplyFailingProfileRepository extends _SuccessProfileRepository {
  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }
}

class _EmptyPublicActivityProfileRepository extends _SuccessProfileRepository {
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
      pageCount: 0,
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
      pageCount: 0,
      comments: [],
    );
  }
}

class _PendingRefreshProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;
  Completer<PublicProfileSummary>? refreshCompleter;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _RefreshFailingProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    throw const RadishApiClientException('公开资料刷新服务暂时不可用');
  }
}

class _FailThenRecoverProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    if (_profileCalls == 2) {
      throw const RadishApiClientException('公开资料刷新服务暂时不可用');
    }

    return Future.value(_updatedProfileSummary(userId));
  }
}

PublicProfileSummary _updatedProfileSummary(String userId) {
  return PublicProfileSummary(
    userId: userId,
    userName: 'luobo',
    displayName: 'Updated Radish Author',
    createTime: '2026-04-20T08:00:00Z',
  );
}

class _PagedPostProfileRepository extends _SuccessProfileRepository {
  final List<int> postPages = <int>[];

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    postPages.add(pageIndex);

    if (pageIndex == 1) {
      return const PublicProfilePostPage(
        page: 1,
        pageSize: 3,
        dataCount: 4,
        pageCount: 2,
        posts: [
          PublicProfilePostSummary(
            id: 'post-page-1',
            title: '第一页公开帖子 1',
            summary: '第一页帖子摘要 1',
            content: '第一页帖子正文 1',
            categoryName: 'Engineering',
            viewCount: 11,
            likeCount: 1,
            commentCount: 2,
            createTime: '2026-04-20T08:00:00Z',
          ),
          PublicProfilePostSummary(
            id: 'post-page-2',
            title: '第一页公开帖子 2',
            summary: '第一页帖子摘要 2',
            content: '第一页帖子正文 2',
            categoryName: 'Community',
            viewCount: 12,
            likeCount: 2,
            commentCount: 3,
            createTime: '2026-04-20T08:02:00Z',
          ),
          PublicProfilePostSummary(
            id: 'post-page-3',
            title: '第一页公开帖子 3',
            summary: '第一页帖子摘要 3',
            content: '第一页帖子正文 3',
            categoryName: 'Design',
            viewCount: 13,
            likeCount: 3,
            commentCount: 4,
            createTime: '2026-04-20T08:04:00Z',
          ),
        ],
      );
    }

    return const PublicProfilePostPage(
      page: 2,
      pageSize: 3,
      dataCount: 4,
      pageCount: 2,
      posts: [
        PublicProfilePostSummary(
          id: 'post-page-4',
          title: '第四篇公开帖子',
          summary: '第四篇公开帖子摘要',
          content: '第四篇公开帖子正文',
          categoryName: 'Flutter',
          viewCount: 14,
          likeCount: 4,
          commentCount: 5,
          createTime: '2026-04-20T08:06:00Z',
        ),
      ],
    );
  }
}

class _PagedPostLoadMoreFailingProfileRepository
    extends _PagedPostProfileRepository {
  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex == 1) {
      return super.getPublicPosts(
        userId: userId,
        pageIndex: pageIndex,
        pageSize: pageSize,
      );
    }

    throw const RadishApiClientException('加载更多公开帖子失败');
  }
}

class _PagedCommentProfileRepository extends _SuccessProfileRepository {
  final List<int> commentPages = <int>[];

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    commentPages.add(pageIndex);

    if (pageIndex == 1) {
      return const PublicProfileCommentPage(
        page: 1,
        pageSize: 3,
        dataCount: 4,
        pageCount: 2,
        comments: [
          PublicProfileCommentSummary(
            id: 'comment-page-1',
            postId: 'post-1',
            content: '第一页公开评论 1',
            likeCount: 1,
            createTime: '2026-04-20T09:00:00Z',
          ),
          PublicProfileCommentSummary(
            id: 'comment-page-2',
            postId: 'post-2',
            content: '第一页公开评论 2',
            likeCount: 2,
            createTime: '2026-04-20T09:02:00Z',
          ),
          PublicProfileCommentSummary(
            id: 'comment-page-3',
            postId: 'post-3',
            content: '第一页公开评论 3',
            likeCount: 3,
            createTime: '2026-04-20T09:04:00Z',
          ),
        ],
      );
    }

    return const PublicProfileCommentPage(
      page: 2,
      pageSize: 3,
      dataCount: 4,
      pageCount: 2,
      comments: [
        PublicProfileCommentSummary(
          id: 'comment-page-4',
          postId: 'post-4',
          content: '第四条公开评论上下文',
          likeCount: 4,
          createTime: '2026-04-20T09:06:00Z',
        ),
      ],
    );
  }
}

class _PagedCommentLoadMoreFailingProfileRepository
    extends _PagedCommentProfileRepository {
  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex == 1) {
      return super.getPublicComments(
        userId: userId,
        pageIndex: pageIndex,
        pageSize: pageSize,
      );
    }

    throw const RadishApiClientException('加载更多公开评论失败');
  }
}

class _PagedQuickReplyProfileRepository extends _SuccessProfileRepository {
  final List<int> quickReplyPages = <int>[];

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    quickReplyPages.add(pageIndex);

    if (pageIndex == 1) {
      return const UserQuickReplyPage(
        page: 1,
        pageSize: 3,
        total: 4,
        items: [
          UserQuickReplySummary(
            id: 'quick-page-1',
            postId: 'post-1',
            postTitle: 'Native profile follow-up',
            content: '第一页轻回应 1',
            createTime: '2026-04-20T09:10:00Z',
          ),
          UserQuickReplySummary(
            id: 'quick-page-2',
            postId: 'post-2',
            postTitle: 'Second native follow-up',
            content: '第一页轻回应 2',
            createTime: '2026-04-20T09:12:00Z',
          ),
          UserQuickReplySummary(
            id: 'quick-page-3',
            postId: 'post-3',
            postTitle: 'Third native follow-up',
            content: '第一页轻回应 3',
            createTime: '2026-04-20T09:14:00Z',
          ),
        ],
      );
    }

    return const UserQuickReplyPage(
      page: 2,
      pageSize: 3,
      total: 4,
      items: [
        UserQuickReplySummary(
          id: 'quick-page-4',
          postId: 'post-4',
          postTitle: 'Fourth native follow-up',
          content: '第四条回看上下文',
          createTime: '2026-04-20T09:16:00Z',
        ),
      ],
    );
  }
}

class _PagedQuickReplyLoadMoreFailingProfileRepository
    extends _PagedQuickReplyProfileRepository {
  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    if (pageIndex == 1) {
      return super.getMyQuickReplies(
        pageIndex: pageIndex,
        pageSize: pageSize,
        accessToken: accessToken,
      );
    }

    throw const RadishApiClientException('加载更多轻回应失败');
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
