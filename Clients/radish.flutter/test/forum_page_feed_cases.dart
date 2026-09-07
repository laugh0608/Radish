part of 'forum_page_test.dart';

void registerForumFeedTests() {
  testWidgets('renders forum posts from repository', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
        find.text('How to wire Radish Flutter forum reading'), findsOneWidget);
    expect(find.text('置顶'), findsOneWidget);
    expect(find.text('提问'), findsOneWidget);
    expect(find.text('42 条评论'), findsOneWidget);
  });
  testWidgets('renders forum error state when repository fails',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _FailingForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('论坛暂不可用'), findsOneWidget);
    expect(find.text('论坛服务暂时不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });
  testWidgets('keeps forum posts visible while refresh is pending',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PendingRefreshForumRepository();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );

    repository.refreshCompleter = Completer<ForumPostPage>();
    await tester.tap(find.text('刷新'));
    await tester.pump();

    expect(find.text('正在刷新论坛列表'), findsOneWidget);
    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );
    expect(find.text('正在刷新'), findsOneWidget);

    repository.refreshCompleter!.complete(_updatedForumPostPage());
    await tester.pumpAndSettle();

    expect(find.text('正在刷新论坛列表'), findsNothing);
    expect(find.text('Updated forum refresh summary'), findsOneWidget);
  });
  testWidgets('keeps forum posts visible when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _RefreshFailingForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );

    await tester.tap(find.text('刷新'));
    await tester.pumpAndSettle();

    expect(find.text('刷新论坛失败，继续显示旧快照'), findsOneWidget);
    expect(find.text('论坛刷新服务暂时不可用'), findsOneWidget);
    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );
    expect(find.text('论坛暂不可用'), findsNothing);
  });
  testWidgets('clears forum refresh issue after successful refresh',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _FailThenRecoverForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('刷新'));
    await tester.pumpAndSettle();

    expect(find.text('刷新论坛失败，继续显示旧快照'), findsOneWidget);

    await tester.tap(find.text('刷新'));
    await tester.pumpAndSettle();

    expect(find.text('刷新论坛失败，继续显示旧快照'), findsNothing);
    expect(find.text('Updated forum refresh summary'), findsOneWidget);
  });
}
