part of 'smoke_test.dart';

void registerSmokeShellCases() {
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

    expect(find.text('Radish'), findsOneWidget);
    expect(find.byTooltip('账户：游客'), findsOneWidget);
  });

  testWidgets('renders Web-family compact shell without overflow',
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

    expect(find.text('Radish'), findsOneWidget);
    expect(find.byKey(const Key('radish-mobile-tab-bar')), findsOneWidget);
    expect(find.byTooltip('登录后查看通知'), findsOneWidget);
    expect(find.byTooltip('账户：游客'), findsOneWidget);
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

    expect(find.text('Radish'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 1);
    expect(find.text('Radish'), findsOneWidget);
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

    expect(find.byTooltip('账户：user-42'), findsOneWidget);
    expect(find.text('已登录用户 user-42'), findsOneWidget);
  });

  testWidgets('authenticated profile opens read-only private account routes',
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
        profileRepository: _SeededProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        walletRepository: const _SeededWalletRepository(),
        experienceRepository: const _SeededExperienceRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('查看商城订单'), findsOneWidget);
    expect(find.text('查看背包'), findsOneWidget);
    expect(find.text('查看胡萝卜资产'), findsOneWidget);
    expect(find.text('查看经验记录'), findsOneWidget);
    expect(find.text('查看账号浏览历史'), findsOneWidget);

    await tester.tap(find.text('查看商城订单'));
    await tester.pumpAndSettle();

    expect(find.text('我的订单'), findsWidgets);
    expect(find.textContaining('RO202605310001'), findsOneWidget);
    expect(find.text('已加载 1 / 1 个订单'), findsOneWidget);
    expect(find.text('查看订单详情'), findsOneWidget);

    await tester.tap(find.text('查看订单详情'));
    await tester.pumpAndSettle();

    expect(find.text('来源：订单列表'), findsOneWidget);
    expect(find.text('完成支付'), findsOneWidget);
    expect(find.text('订单完成'), findsOneWidget);
    expect(find.text('扣款流水'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '查看扣款流水'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '查看扣款流水'));
    await tester.pumpAndSettle();

    expect(find.text('订单扣款流水'), findsWidgets);
    expect(find.text('返回订单详情'), findsOneWidget);
    expect(find.text('当前筛选：Order #9001'), findsOneWidget);
    expect(find.text('已加载 1 / 1 条流水'), findsOneWidget);
    expect(find.text('匹配流水'), findsOneWidget);
    expect(find.text('商城消费'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('订单详情'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的订单'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看商城订单'), findsOneWidget);

    await tester.tap(find.text('查看背包'));
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
    expect(find.text('已加载 1 个权益、1 个道具'), findsOneWidget);
    expect(find.text('早鸟徽章'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('查看来源订单'), findsOneWidget);
    expect(find.text('查看来源商品'), findsWidgets);

    await tester.tap(find.text('查看来源订单'));
    await tester.pumpAndSettle();

    expect(find.text('来源：背包来源'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);
    expect(find.text('完成支付'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);

    await tester.tap(find.text('查看来源商品').first);
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：背包来源'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看背包'), findsOneWidget);

    await tester.tap(find.text('查看胡萝卜资产'));
    await tester.pumpAndSettle();

    expect(find.text('胡萝卜资产'), findsWidgets);
    expect(find.text('已加载 2 / 2 条流水'), findsOneWidget);
    expect(find.text('余额概览'), findsOneWidget);
    expect(find.text('1200 胡萝卜'), findsOneWidget);
    expect(find.text('系统赠送'), findsOneWidget);
    expect(find.text('商城消费'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看胡萝卜资产'), findsOneWidget);

    await tester.tap(find.text('查看经验记录'));
    await tester.pumpAndSettle();

    expect(find.text('经验记录'), findsWidgets);
    expect(find.text('已加载 2 / 2 条经验流水'), findsOneWidget);
    expect(find.text('等级概览'), findsOneWidget);
    expect(find.text('Lv.3 练气'), findsOneWidget);
    expect(find.text('发帖奖励'), findsOneWidget);
    expect(find.text('评论奖励'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看经验记录'), findsOneWidget);

    await tester.tap(find.text('查看账号浏览历史'));
    await tester.pumpAndSettle();

    expect(find.text('账号浏览历史'), findsWidgets);
    expect(find.text('已加载 3 / 3 条记录'), findsOneWidget);
    expect(find.text('论坛详情回流'), findsOneWidget);
    expect(find.text('Native docs'), findsOneWidget);
    expect(find.text('Early Access Badge'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '打开商品'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：账号浏览历史'), findsOneWidget);
    expect(find.text('返回账号浏览历史'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('账号浏览历史'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看账号浏览历史'), findsOneWidget);
  });
}
