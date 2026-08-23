part of 'smoke_test.dart';

void registerSmokeHandoffCases() {
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
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
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

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('论坛详情回流'), findsOneWidget);
    expect(
      find.text(
        '按最新或热门连续浏览公开帖子，完整讨论继续进入原生详情。',
      ),
      findsOneWidget,
    );
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

    expect(find.text('论坛详情回流'), findsWidgets);
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
    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: scrollable,
    );
    await tester.drag(scrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);
  });

  testWidgets('profile comment reply returns to profile after detail pop',
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

    final profileScrollable = find.byType(Scrollable).last;
    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.drag(profileScrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);

    await tester.tap(find.widgetWithText(TextButton, '回复').first);
    await tester.pumpAndSettle();
    await tester.enterText(
      _forumCommentTextField(hintText: '写下你的回复...'),
      '从个人主页评论回复',
    );
    await tester.pump();
    final replyButton = find.widgetWithText(FilledButton, '发布回复');
    await tester.ensureVisible(replyButton);
    await tester.tap(replyButton);
    await tester.pumpAndSettle();

    expect(find.text('回复已发布，已更新当前评论区。'), findsOneWidget);
    expect(find.text('从个人主页评论回复'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开评论'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('profile post handoff returns to profile after detail pop',
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
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: scrollable,
    );
    await tester.tap(openPostButton);
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
    expect(find.text('原帖回流'), findsWidgets);

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
      find.byKey(const Key('discover-item-docs:1')),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.byKey(const Key('discover-item-docs:1')));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);

    Navigator.of(tester.element(find.text('文档详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('发现'), findsWidgets);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
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
      find.byKey(const Key('discover-item-docs:1')),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.byKey(const Key('discover-item-docs:1')));
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
    final lifecycleGateway = _RecordingAppLifecycleGateway();

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
        appLifecycleGateway: lifecycleGateway,
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

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);
    expect(find.text('Doc public-docs-reading-boundary'), findsNothing);
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
}
