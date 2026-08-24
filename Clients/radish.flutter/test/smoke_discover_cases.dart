part of 'smoke_test.dart';

void registerSmokeDiscoverCases() {
  testWidgets('discover handoff opens guest profile target in shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).last;
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.byKey(const Key('discover-contributor-user-9')),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.byKey(const Key('discover-contributor-user-9')));
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('用户 user-9'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('社区正在发生'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover forum card opens detail and returns to discover',
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
      find.byKey(const Key('discover-item-post:1')),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.byKey(const Key('discover-item-post:1')));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-1'), findsOneWidget);
    expect(find.text('打开来源：发现'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('社区正在发生'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover forum shortcut returns to discover on Android back',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('进入论坛'));
    await tester.pumpAndSettle();

    expect(
      find.text(
        '按最新或热门连续浏览公开帖子，完整讨论继续进入原生详情。',
      ),
      findsOneWidget,
    );

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('社区正在发生'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover docs shortcut returns to discover on Android back',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('进入文档'));
    await tester.pumpAndSettle();

    expect(find.text('浏览公开文档列表。当前不开放编辑、发布和治理操作。'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('社区正在发生'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets(
      'discover leaderboard shortcut returns to discover on Android back',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开榜单').first,
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开榜单').first);
    await tester.pumpAndSettle();

    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('当前暂无可展示的经验榜排名。'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('社区正在发生'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover shop context opens read-only detail and returns',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开商城'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开商城'));
    await tester.pumpAndSettle();

    expect(find.text('商品列表'), findsOneWidget);
    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('公开商品详情'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsWidgets);
    expect(find.text('单商品购买'), findsOneWidget);
    expect(find.text('登录后购买'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('商品列表'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('社区正在发生'), findsOneWidget);
  });

  testWidgets(
      'authenticated discover product purchase returns to discover context',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        walletRepository: const _SeededWalletRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开商城'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开商城'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('来源：公开商品列表'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);
    expect(find.text('当前可购买'), findsOneWidget);

    await tester.enterText(find.byType(TextField), '123456');
    await tester.tap(find.text('确认购买 1 件'));
    await tester.pumpAndSettle();

    expect(find.text('订单详情'), findsWidgets);
    expect(find.text('来源：购买结果'), findsOneWidget);
    expect(find.text('返回商品详情'), findsOneWidget);
    expect(find.text('订单 RO202605310001'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('Profile Rename Card'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('商品列表'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('社区正在发生'), findsOneWidget);
  });

  testWidgets('discover shop shortcut opens read-only product list and returns',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开商城'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开商城'));
    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：公开商品列表'), findsOneWidget);
    expect(find.text('返回商城'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('社区正在发生'), findsOneWidget);
  });

  testWidgets('leaderboard user can open public profile and return to ranking',
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
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();

    expect(find.text('luobo'), findsWidgets);
    await tester.tap(find.text('打开公开主页').first);
    await tester.pumpAndSettle();

    expect(find.text('公开主页'), findsOneWidget);
    expect(find.text('正在阅读公开主页 9'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsWidgets);
  });

  testWidgets(
      'leaderboard profile detail pop preserves return to ranking on Android back',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
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
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();

    await tester.tap(find.text('打开公开主页').first);
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 9'), findsOneWidget);

    final scrollable = find.byType(Scrollable).last;
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: scrollable,
    );
    await tester.ensureVisible(openPostButton);
    await tester.pumpAndSettle();
    await tester.drag(scrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openPostButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('正在阅读公开主页 9'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsWidgets);
  });

  testWidgets(
      'leaderboard profile post and comment details handle Android back to source',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('打开公开主页').first);
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 9'), findsOneWidget);

    final profileScrollable = find.byType(Scrollable).last;
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.tap(openPostButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('正在阅读公开主页 9'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);

    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('First public child comment'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开评论'), findsOneWidget);
    expect(find.text('正在阅读公开主页 9'), findsOneWidget);
    expect(find.text('First public child comment'), findsNothing);
    expect(find.text('帖子详情'), findsNothing);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsWidgets);
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
      find.byKey(const Key('discover-contributor-user-9')),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.byKey(const Key('discover-contributor-user-9')));
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
}
