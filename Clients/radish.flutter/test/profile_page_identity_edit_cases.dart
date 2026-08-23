part of 'profile_page_test.dart';

void registerProfileIdentityEditTests() {
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
    expect(find.text('评论 comment-1'), findsNothing);
  });

  testWidgets('edits my profile and refreshes the public summary', (
    tester,
  ) async {
    final repository = _EditableProfileRepository();
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
        home: Scaffold(
          body: ProfilePage(
            sessionController: sessionController,
            authController: authController,
            repository: repository,
          ),
        ),
      ),
    );

    await tester.pumpAndSettle();
    expect(find.text('luobo'), findsOneWidget);

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();

    expect(find.text('编辑个人资料'), findsOneWidget);
    await tester.enterText(
      find.byKey(const Key('profile-edit-user-name')),
      'native_user',
    );
    await tester.enterText(
      find.byKey(const Key('profile-edit-email')),
      'native@example.com',
    );
    await tester.enterText(
      find.byKey(const Key('profile-edit-age')),
      '26',
    );
    await tester.enterText(
      find.byKey(const Key('profile-edit-address')),
      '移动端资料维护',
    );

    await tester.tap(find.text('保存'));
    await tester.pumpAndSettle();

    expect(find.text('编辑个人资料'), findsNothing);
    expect(find.text('个人资料更新成功'), findsOneWidget);
    expect(find.text('native_user'), findsOneWidget);
    expect(repository.updateCount, 1);
    expect(repository.lastRequest?.userName, 'native_user');
    expect(repository.lastRequest?.userEmail, 'native@example.com');
    expect(repository.lastRequest?.age, 26);
    expect(repository.lastRequest?.address, '移动端资料维护');
  });

  testWidgets(
      'keeps profile editor open and current profile unchanged on save failure',
      (
    tester,
  ) async {
    final repository = _ProfileUpdateFailingRepository();
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
        home: Scaffold(
          body: ProfilePage(
            sessionController: sessionController,
            authController: authController,
            repository: repository,
          ),
        ),
      ),
    );

    await tester.pumpAndSettle();
    expect(find.text('luobo'), findsOneWidget);

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('profile-edit-user-name')),
      'taken_user',
    );

    await tester.tap(find.text('保存'));
    await tester.pumpAndSettle();

    expect(find.text('编辑个人资料'), findsOneWidget);
    expect(find.text('用户名已被占用'), findsOneWidget);
    expect(find.text('taken_user'), findsOneWidget);
    expect(find.text('Native Author'), findsNothing);
    expect(repository.updateCount, 1);
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
      find.text(_longRecentBrowseTitle),
      200,
      scrollable: scrollable,
    );
    expect(tester.takeException(), isNull);
    expect(find.text('评论上下文'), findsWidgets);
    expect(find.text('帖子 $_longPostId · 评论 $_longCommentId'), findsNothing);

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
}
