part of 'profile_page_test.dart';

void registerProfileAuthBoundaryTests() {
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
