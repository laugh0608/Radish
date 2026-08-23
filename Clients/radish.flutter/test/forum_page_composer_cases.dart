part of 'forum_page_test.dart';

void registerForumComposerTests() {
  testWidgets('publishes a text post for authenticated forum user',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _SuccessForumRepository();
    final openedTargets = <ForumDetailHandoffTarget>[];
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: 'access-token-42',
          refreshToken: 'refresh-token-42',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: const _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          sessionController: sessionController,
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await _openForumComposer(tester);

    await tester.enterText(_forumTextFieldByLabel('标题'), 'Flutter 端发帖联调');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 发布');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '这是一条来自 Flutter 端的纯文本帖子。',
    );
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(1));
    expect(repository.createPostRequests.single.title, 'Flutter 端发帖联调');
    expect(repository.createPostRequests.single.categoryId, '9');
    expect(repository.createPostRequests.single.tagNames, ['flutter', '发布']);
    expect(repository.createPostRequests.single.accessToken, 'access-token-42');
    expect(
      repository.createPostRequests.single.clientSubmissionId,
      startsWith('forum-post:'),
    );
    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-created-1');
    expect(openedTargets.single.initialTitle, 'Flutter 端发帖联调');
    expect(find.byKey(const Key('forum-composer-task-bounded')), findsNothing);
  });
  testWidgets('opens created post detail with public route after publishing',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CreatedPostPublicRouteForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: 'access-token-42',
          refreshToken: 'refresh-token-42',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: const _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await _openForumComposer(tester);

    await tester.enterText(_forumTextFieldByLabel('标题'), 'Flutter 新帖公开链路');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 链路');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '发布后应打开带 PublicId 的公开详情。',
    );
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(1));
    expect(
      repository.createPostRequests.single.clientSubmissionId,
      startsWith('forum-post:'),
    );
    expect(repository.lastDetailPostId, 'post-created-1');
    expect(find.text('Flutter 新帖公开链路'), findsWidgets);
    expect(find.text('/forum/post/pst_created_flutter'), findsOneWidget);
    expect(find.text('/forum/post/post-created-1'), findsNothing);
  });
  testWidgets('keeps post draft when publishing fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CreatePostFailingForumRepository();
    final openedTargets = <ForumDetailHandoffTarget>[];
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: 'access-token-42',
          refreshToken: 'refresh-token-42',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: const _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          sessionController: sessionController,
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await _openForumComposer(tester);

    await tester.enterText(_forumTextFieldByLabel('标题'), '失败后保留草稿');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 失败');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '发帖失败后应该留在当前表单。',
    );
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(1));
    expect(openedTargets, isEmpty);
    expect(find.text('发帖服务暂时不可用'), findsOneWidget);
    expect(find.text('帖子已发布，正在打开详情。'), findsNothing);
    expect(find.text('失败后保留草稿'), findsOneWidget);
    expect(find.text('发帖失败后应该留在当前表单。'), findsOneWidget);
  });
  testWidgets('reuses post submission key when retrying failed publish',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CreatePostFailingForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: 'access-token-42',
          refreshToken: 'refresh-token-42',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: const _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await _openForumComposer(tester);

    await tester.enterText(_forumTextFieldByLabel('标题'), '失败后复用 key');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 重试');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '同一份发帖草稿重试时应复用提交意图 ID。',
    );
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(2));
    expect(
      repository.createPostRequests.first.clientSubmissionId,
      startsWith('forum-post:'),
    );
    expect(
      repository.createPostRequests.last.clientSubmissionId,
      repository.createPostRequests.first.clientSubmissionId,
    );
  });
  testWidgets('keeps anonymous post draft through sign-in and publish',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _SuccessForumRepository();
    final openedTargets = <ForumDetailHandoffTarget>[];
    var signInRequestCount = 0;
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          sessionController: sessionController,
          onOpenForumDetailTarget: openedTargets.add,
          onRequestSignInForForum: () async {
            signInRequestCount += 1;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    await _openForumComposer(tester);

    await tester.enterText(_forumTextFieldByLabel('标题'), '匿名发帖登录回流');
    await tester.enterText(_forumTextFieldByLabel('标签'), 'flutter, 登录');
    await tester.enterText(
      _forumTextFieldByLabel('正文'),
      '登录前填写的纯文本草稿应该留在表单里。',
    );
    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(signInRequestCount, 1);
    expect(repository.createPostRequests, isEmpty);
    expect(find.text('登录后可继续提交当前帖子。'), findsOneWidget);
    expect(find.text('匿名发帖登录回流'), findsOneWidget);
    expect(find.text('登录前填写的纯文本草稿应该留在表单里。'), findsOneWidget);

    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token-42',
        refreshToken: 'refresh-token-42',
        userId: 'user-42',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('已回到发帖表单，可以继续发布。'), findsOneWidget);
    expect(find.text('匿名发帖登录回流'), findsOneWidget);
    expect(find.text('登录前填写的纯文本草稿应该留在表单里。'), findsOneWidget);

    await tester.tap(find.byKey(const Key('forum-composer-submit')));
    await tester.pumpAndSettle();

    expect(repository.createPostRequests, hasLength(1));
    expect(repository.createPostRequests.single.title, '匿名发帖登录回流');
    expect(repository.createPostRequests.single.categoryId, '9');
    expect(repository.createPostRequests.single.accessToken, 'access-token-42');
    expect(
      repository.createPostRequests.single.clientSubmissionId,
      startsWith('forum-post:'),
    );
    expect(openedTargets, hasLength(1));
    expect(openedTargets.single.postId, 'post-created-1');
    expect(openedTargets.single.initialTitle, '匿名发帖登录回流');
  });
}
