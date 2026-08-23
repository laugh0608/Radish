part of 'forum_detail_page_test.dart';

void runForumDetailQuickAnswerCases() {
  testWidgets('submits quick reply when session is authenticated',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布轻回应'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_quickReplyTextField(), '同感');
    final quickReplyButton = find.widgetWithText(FilledButton, '发布轻回应');
    await tester.ensureVisible(quickReplyButton);
    await tester.tap(quickReplyButton);
    await tester.pumpAndSettle();

    expect(find.text('current：同感'), findsOneWidget);
    expect(find.text('共 3 条轻回应'), findsOneWidget);
    expect(find.text('轻回应已发布，已显示在轻回应墙顶部。'), findsOneWidget);
  });

  testWidgets('submits quick reply without reloading detail or comments',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CountingQuickReplyForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布轻回应'),
      200,
      scrollable: scrollable,
    );

    final scrollableState = tester.state<ScrollableState>(scrollable);
    final positionBeforeSubmit = scrollableState.position.pixels;
    expect(positionBeforeSubmit, greaterThan(0));
    expect(repository.detailRequests, 1);
    expect(repository.rootCommentRequests, 1);

    await tester.enterText(_quickReplyTextField(), '我也来一句');
    final quickReplyButton = find.widgetWithText(FilledButton, '发布轻回应');
    await tester.ensureVisible(quickReplyButton);
    await tester.tap(quickReplyButton);
    await tester.pumpAndSettle();

    expect(find.text('current：我也来一句'), findsOneWidget);
    expect(find.text('轻回应已发布，已显示在轻回应墙顶部。'), findsOneWidget);
    expect(repository.detailRequests, 1);
    expect(repository.rootCommentRequests, 1);
    expect(repository.createQuickReplyRequests, 1);
    expect(scrollableState.position.pixels, greaterThan(0));
  });

  testWidgets('quick reply submit failure stays inside quick reply section',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _QuickReplySubmitFailingForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布轻回应'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_quickReplyTextField(), '发不出去的轻回应');
    final quickReplyButton = find.widgetWithText(FilledButton, '发布轻回应');
    await tester.ensureVisible(quickReplyButton);
    await tester.tap(quickReplyButton);
    await tester.pumpAndSettle();

    expect(find.text('轻回应发布失败'), findsOneWidget);
    expect(find.text('轻回应服务暂时不可用'), findsOneWidget);
    expect(find.text('current：发不出去的轻回应'), findsNothing);
    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('Root comment one'), findsOneWidget);
    expect(find.text('radish：学到了'), findsOneWidget);
  });

  testWidgets('renders question answers in detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _QuestionAnswerForumRepository(),
          postId: 'post-42',
          initialTitle: '问答详情',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('已采纳'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('2 个回答'), findsWidgets);
    expect(find.text('已解决'), findsWidgets);
    expect(find.text('已采纳'), findsOneWidget);
    expect(find.text('Accepted answer content'), findsOneWidget);
    expect(find.text('Second answer content'), findsOneWidget);
    expect(find.text('登录后可以回答问题'), findsOneWidget);
  });

  testWidgets('answer sign-in returns to composer in current detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: InMemoryNativeAuthGateway(),
      exchangeService: const _UnusedAuthorizationCodeExchangeService(),
    );
    final signInTargets = <ForumDetailHandoffTarget>[];
    var consumedLoginTargetCount = 0;

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _QuestionAnswerForumRepository(),
          postId: 'post-42',
          initialTitle: '问答详情',
          sessionController: sessionController,
          authController: authController,
          onRequestSignIn: (target) async {
            signInTargets.add(target);
          },
          onConsumeActiveDetailLoginTarget: () async {
            consumedLoginTargetCount += 1;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('登录后回答'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(FilledButton, '登录后回答'));
    await tester.pumpAndSettle();

    expect(signInTargets, hasLength(1));
    expect(signInTargets.single.postId, 'post-42');

    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );
    await tester.pumpAndSettle();

    expect(consumedLoginTargetCount, 1);
    expect(find.text('已回到回答区，可以继续发布。'), findsOneWidget);
    expect(find.text('发布回答'), findsOneWidget);
  });

  testWidgets('submits question answer when session is authenticated',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _RecordingAnswerForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '问答详情',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布回答'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_answerTextField(), 'Flutter 新回答内容');
    await tester.pump();
    final answerButton = find.widgetWithText(FilledButton, '发布回答');
    await tester.ensureVisible(answerButton);
    await tester.tap(answerButton);
    await tester.pumpAndSettle();

    expect(find.text('Flutter 新回答内容'), findsOneWidget);
    expect(find.text('回答已发布，已显示在回答区。'), findsOneWidget);
    expect(find.text('3 个回答'), findsWidgets);
    expect(repository.answerRequests, hasLength(1));
    expect(repository.answerRequests.single.postId, 'post-42');
    expect(repository.answerRequests.single.content, 'Flutter 新回答内容');
    expect(repository.answerRequests.single.accessToken, 'access-token');
    expect(
      repository.answerRequests.single.clientSubmissionId,
      startsWith('forum-answer:'),
    );
  });

  testWidgets('reuses answer submission key when retrying failed answer',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _AnswerSubmitFailingForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '问答详情',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布回答'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_answerTextField(), '失败后复用回答 key');
    await tester.pump();
    final answerButton = find.widgetWithText(FilledButton, '发布回答');
    await tester.ensureVisible(answerButton);
    await tester.tap(answerButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('重试发布'));
    await tester.pumpAndSettle();

    expect(find.text('回答发布失败'), findsOneWidget);
    expect(find.text('回答服务暂时不可用'), findsOneWidget);
    expect(repository.answerRequests, hasLength(2));
    expect(
      repository.answerRequests.first.clientSubmissionId,
      startsWith('forum-answer:'),
    );
    expect(
      repository.answerRequests.last.clientSubmissionId,
      repository.answerRequests.first.clientSubmissionId,
    );
  });
}
