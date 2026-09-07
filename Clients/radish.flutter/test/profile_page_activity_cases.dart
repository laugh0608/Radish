part of 'profile_page_test.dart';

void registerProfileActivityTests() {
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
    expect(openedTargets.single.postId, 'pst_018f6b6f7c7d70008f8f8f8f8f8f804');
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
    expect(openedTargets.single.postId, 'pst_018f6b6f7c7d70008f8f8f8f8f8f814');
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
    expect(find.text('轻回应回看'), findsWidgets);
    expect(find.text('帖子 post-1 · 轻回应 quick-1'), findsNothing);
    expect(find.text('原帖回流'), findsOneWidget);

    await tester.tap(find.text('回到原帖'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f');
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
}
