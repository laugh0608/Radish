part of 'smoke_test.dart';

void registerSmokeAuthCases() {
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

    expect(find.byTooltip('账户：user-42'), findsOneWidget);
    expect(find.byTooltip('账户：游客'), findsNothing);
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

    expect(find.byTooltip('账户：游客'), findsOneWidget);
    expect(find.text('会话需要恢复'), findsOneWidget);
    expect(find.text('重新登录'), findsOneWidget);
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

    expect(find.byTooltip('账户：user-88'), findsOneWidget);
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
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-108'), findsOneWidget);
    expect(find.byTooltip('账户：user-108'), findsOneWidget);
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
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-rebuild-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-208'), findsOneWidget);
    expect(find.byTooltip('账户：user-208'), findsOneWidget);
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

    await _openAccountMenu(tester);
    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text('浏览器返回应用前，登录已取消。'),
      findsOneWidget,
    );

    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
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

    await _openAccountMenu(tester);
    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-rebuild-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
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

    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-detail-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
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

    await tester.tap(find.text('重试登录').first);
    await tester.pumpAndSettle();
    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'notification-detail-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('已登录'), findsWidgets);
    expect(find.text('登录并保留当前位置'), findsNothing);
  });

  testWidgets('quick reply sign-in returns to composer in current detail',
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
            userId: 'user-212',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-212',
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
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('登录后发布'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    expect(find.text('登录后可以发布轻回应'), findsOneWidget);

    await tester.tap(find.text('登录后发布'));
    await tester.pumpAndSettle();
    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'quick-reply-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已回到轻回应区，可以继续发布。'), findsOneWidget);
    expect(find.text('发布轻回应'), findsOneWidget);
    expect(find.text('登录后发布'), findsNothing);
  });

  testWidgets('comment sign-in returns to composer in current detail',
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
            userId: 'user-213',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-213',
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
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('登录后评论'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    expect(find.text('登录后可以发表评论'), findsOneWidget);

    await tester.tap(find.text('登录后评论'));
    await tester.pumpAndSettle();
    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'comment-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已回到评论区，可以继续发布。'), findsOneWidget);
    expect(find.text('发布评论'), findsOneWidget);
    expect(find.text('登录后评论'), findsNothing);
  });

  testWidgets('forum post sign-in keeps draft and publishes after callback',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
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
            userId: 'user-214',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-214',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final repository = _RecordingPostForumRepository();

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: repository,
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.enterText(_forumTextFieldByLabel('标题'), '登录回流发帖');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 回流');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '匿名态进入登录后，回来继续发布同一份草稿。',
    );

    await tester.tap(find.widgetWithText(FilledButton, '发布帖子'));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, isEmpty);
    expect(find.text('登录后可继续提交当前帖子。'), findsOneWidget);
    expect(find.text('登录回流发帖'), findsOneWidget);
    expect(find.text('匿名态进入登录后，回来继续发布同一份草稿。'), findsOneWidget);

    gateway.setPendingCallback(
      NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'post-login-code',
        state: gateway.lastAuthorizeUri!.queryParameters['state'],
      ),
    );

    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('radish-account-action')), findsOneWidget);
    expect(find.text('已回到发帖表单，可以继续发布。'), findsOneWidget);
    expect(find.text('登录回流发帖'), findsOneWidget);
    expect(find.text('匿名态进入登录后，回来继续发布同一份草稿。'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '发布帖子'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(1));
    expect(repository.createPostRequests.single.title, '登录回流发帖');
    expect(repository.createPostRequests.single.categoryId, 'category-1');
    expect(repository.createPostRequests.single.tagNames, ['flutter', '回流']);
    expect(repository.createPostRequests.single.accessToken, isNotEmpty);
    expect(
      repository.createPostRequests.single.clientSubmissionId,
      startsWith('forum-post:'),
    );
    expect(find.text('/forum/post/post-created'), findsOneWidget);
    expect(find.text('帖子详情'), findsWidgets);
  });
}
