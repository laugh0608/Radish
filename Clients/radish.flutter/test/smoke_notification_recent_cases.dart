part of 'smoke_test.dart';

void registerSmokeNotificationRecentCases() {
  testWidgets('pending notification handoff opens shared native forum detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore(
      initialPendingHandoff: const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: 'Native discover wiring plan',
      ),
    );
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededBigIdDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
  });

  testWidgets('recent forum notification list opens shared native detail',
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
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: const _FakeForumNotificationRepository(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的').last);
    await tester.pumpAndSettle();
    expect(find.text('已登录用户 user-42'), findsOneWidget);
    await _openNotificationMenu(tester);
    expect(find.text('通知 3 条'), findsOneWidget);

    await tester.tap(find.text('查看通知'));
    await tester.pumpAndSettle();

    expect(find.text('系统维护'), findsOneWidget);
    expect(find.text('只读'), findsOneWidget);
    expect(find.text('帖子被评论'), findsOneWidget);
    expect(find.text('帖子收到轻回应'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '标记已读'), findsNWidgets(2));

    await tester.tap(find.widgetWithText(FilledButton, '标记已读').first);
    await tester.pumpAndSettle();

    expect(find.textContaining('系统 · 已读'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '标记已读'), findsOneWidget);

    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('已登录用户 user-42'), findsOneWidget);
    await _openNotificationMenu(tester);
    expect(find.text('通知 3 条'), findsOneWidget);
  });

  testWidgets('opening unread forum notification marks it read before detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final notificationRepository = _MutableForumNotificationRepository()
      ..notifications = const [
        NotificationListItem(
          id: 'notification-forum-comment',
          notificationId: 'forum-1',
          notification: NotificationPayload(
            title: '帖子被评论',
            content: '有人评论了你的帖子。',
            type: 'CommentReplied',
            businessType: 'Comment',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
              'commentId': 'comment-big-1',
            },
          ),
          isRead: false,
          createdAt: '2026-05-31T08:31:00',
        ),
      ];
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
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: notificationRepository,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的').last);
    await tester.pumpAndSettle();
    await _openNotificationList(tester);

    expect(find.text('帖子被评论'), findsOneWidget);
    expect(find.textContaining('评论 · 未读'), findsOneWidget);

    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(notificationRepository.markedReadNotificationIds, ['forum-1']);
    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
  });

  testWidgets('forum notification opens detail when auto mark read fails',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final notificationRepository = _MutableForumNotificationRepository()
      ..markAsReadError = const RadishApiClientException('已读接口不可用')
      ..notifications = const [
        NotificationListItem(
          id: 'notification-forum-comment',
          notificationId: 'forum-1',
          notification: NotificationPayload(
            title: '帖子被评论',
            content: '有人评论了你的帖子。',
            type: 'CommentReplied',
            businessType: 'Comment',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
              'commentId': 'comment-big-1',
            },
          ),
          isRead: false,
          createdAt: '2026-05-31T08:31:00',
        ),
      ];
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
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: notificationRepository,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的').last);
    await tester.pumpAndSettle();
    await _openNotificationList(tester);
    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(notificationRepository.markedReadNotificationIds, isEmpty);
    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('通知回流'), findsWidgets);
    expect(
      find.text('通知已打开，标记已读失败：已读接口不可用'),
      findsOneWidget,
    );
  });

  testWidgets('forum notification chip explains failure empty and refresh',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final notificationRepository = _MutableForumNotificationRepository()
      ..error = StateError('notification service unavailable');
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
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: notificationRepository,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await _openNotificationMenu(tester);
    expect(find.text('通知刷新失败'), findsOneWidget);
    expect(find.text('刷新通知'), findsOneWidget);
    expect(notificationRepository.callCount, 1);

    notificationRepository.error = null;
    await tester.tap(find.text('刷新通知'));
    await tester.pump();
    await tester.pumpAndSettle();

    await _openNotificationMenu(tester);
    expect(find.text('暂无通知'), findsOneWidget);
    expect(find.text('刷新通知'), findsOneWidget);
    expect(notificationRepository.callCount, 2);

    notificationRepository.target = const ForumDetailHandoffTarget(
      postId: '2042219067430928384',
      source: ForumDetailHandoffSource.notification,
      initialTitle: '帖子被评论',
      commentId: 'comment-big-1',
    );
    await tester.tap(find.text('刷新通知'));
    await tester.pump();
    await tester.pumpAndSettle();

    await _openNotificationMenu(tester);
    expect(find.text('通知 1 条'), findsOneWidget);
    expect(notificationRepository.callCount, 3);

    await tester.tap(find.text('查看通知'));
    await tester.pumpAndSettle();

    expect(find.text('帖子被评论'), findsOneWidget);

    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
  });

  testWidgets('recent browse handoff resumes forum detail from shell chip',
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

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
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

    await tester.tap(find.byKey(const Key('radish-recent-action')));
    await tester.pumpAndSettle();
    expect(find.text('继续阅读论坛'), findsOneWidget);
    await tester.tap(find.text('继续阅读论坛'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
  });

  test('recent browse store keeps newest deduplicated targets', () async {
    final followUpStore = InMemoryForumFollowUpStore();

    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-1',
        source: ForumDetailHandoffSource.shell,
        initialTitle: 'First post',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-2',
        source: ForumDetailHandoffSource.publicProfilePost,
        initialTitle: 'Second post',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-1',
        source: ForumDetailHandoffSource.publicProfileComment,
        initialTitle: 'First comment',
        commentId: 'comment-1',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-2',
        source: ForumDetailHandoffSource.discover,
        initialTitle: 'Second post refreshed',
      ),
    );

    final targets = await followUpStore.readRecentBrowseHandoffs();

    expect(targets.map((target) => target.postId), [
      'post-2',
      'post-1',
      'post-1',
    ]);
    expect(targets.first.initialTitle, 'Second post refreshed');
    expect(targets.first.source, ForumDetailHandoffSource.browseHistory);
    expect(targets[1].commentId, 'comment-1');
    expect(await followUpStore.readRecentBrowseHandoff(), targets.first);
  });

  test('recent document store keeps newest deduplicated targets', () async {
    final followUpStore = InMemoryDocsFollowUpStore();

    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'flutter-docs-scope',
        source: DocsDetailHandoffSource.discover,
        initialTitle: 'Flutter docs scope',
      ),
    );
    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'public-docs-reading-boundary',
        source: DocsDetailHandoffSource.docsList,
        initialTitle: 'Public docs reading boundary',
      ),
    );
    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'flutter-docs-scope',
        source: DocsDetailHandoffSource.docsLink,
        initialTitle: 'Flutter docs scope refreshed',
      ),
    );

    final targets = await followUpStore.readRecentDocumentTargets();

    expect(targets.map((target) => target.slug), [
      'flutter-docs-scope',
      'public-docs-reading-boundary',
    ]);
    expect(targets.first.initialTitle, 'Flutter docs scope refreshed');
    expect(targets.first.source, DocsDetailHandoffSource.browseHistory);
    expect(await followUpStore.readRecentDocumentTarget(), targets.first);
  });
}
