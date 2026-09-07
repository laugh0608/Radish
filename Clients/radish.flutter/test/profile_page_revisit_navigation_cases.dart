part of 'profile_page_test.dart';

void registerProfileRevisitNavigationTests() {
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

  testWidgets('renders and copies public profile web link', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final clipboard = _ClipboardRecorder()..install();
    addTearDown(clipboard.reset);

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
          environment: const AppEnvironment(
            name: 'test',
            apiBaseUrl: 'https://radish.example',
            authBaseUrl: 'https://radish.example',
            gatewayBaseUrl: 'https://radish.example',
            oidcClientId: 'radish-client',
            nativeOidcRedirectUri: 'radish://oidc/callback',
            nativeOidcPostLogoutRedirectUri: 'radish://oidc/logout-complete',
            oidcScopes: 'openid profile offline_access radish-api',
          ),
          publicUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('公开主页链接'), findsOneWidget);
    expect(find.text('https://radish.example/u/guest-42'), findsOneWidget);

    await tester.tap(find.text('复制公开链接'));
    await tester.pumpAndSettle();

    expect(clipboard.text, 'https://radish.example/u/guest-42');
    expect(find.text('公开链接已复制'), findsOneWidget);
    expect(find.text('已复制公开链接'), findsOneWidget);
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
    expect(
      openedTargets.first.postId,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
    );
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
    expect(
      openedTargets.last.postId,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
    );
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
    expect(find.text('继续回到上次打开的评论上下文。'), findsOneWidget);
    expect(find.text('帖子 post-1 · 评论 comment-1'), findsNothing);

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
}
