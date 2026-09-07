part of 'forum_page_test.dart';

void registerForumNavigationTests() {
  testWidgets('opens author profile handoff from forum feed', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
          onOpenProfileUser: (userId) {
            openedUserId = userId;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('Luobo'));
    await tester.pumpAndSettle();

    expect(openedUserId, '1024');
  });
  testWidgets('uses shared shell handoff when opening detail from forum feed',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final openedTargets = <ForumDetailHandoffTarget>[];

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
          onOpenForumDetailTarget: openedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(openedTargets, hasLength(1));
    expect(openedTargets.first.postId, '2042219067430928384');
    expect(openedTargets.first.initialTitle,
        'How to wire Radish Flutter forum reading');
    expect(openedTargets.first.source, ForumDetailHandoffSource.shell);
    expect(find.text('帖子详情'), findsNothing);
  });
  testWidgets('opens forum detail handoff target from external shell state',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    var consumed = 0;

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
          handoffTarget: const ForumDetailHandoffTarget(
            postId: '2042219067430928384',
            initialTitle: 'How to wire Radish Flutter forum reading',
            commentId: 'comment-2048',
          ),
          onConsumeHandoffTarget: () {
            consumed += 1;
          },
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(consumed, 1);
    expect(find.text('How to wire Radish Flutter forum reading'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('评论'), findsWidgets);
  });
  testWidgets('can reopen the same forum detail target after it is consumed',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    var consumed = 0;
    ForumDetailHandoffTarget? handoffTarget;

    Future<void> pumpForumPage() async {
      await tester.pumpWidget(
        _forumPageTestApp(
          home: ForumPage(
            environment: const AppEnvironment.development(),
            repository: _SuccessForumRepository(),
            handoffTarget: handoffTarget,
            onConsumeHandoffTarget: () {
              consumed += 1;
              handoffTarget = null;
            },
          ),
        ),
      );
    }

    handoffTarget = const ForumDetailHandoffTarget(
      postId: '2042219067430928384',
      initialTitle: 'How to wire Radish Flutter forum reading',
    );
    await pumpForumPage();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(consumed, 1);
    expect(find.text('帖子详情'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    await pumpForumPage();
    await tester.pumpAndSettle();
    expect(find.text('帖子详情'), findsNothing);

    handoffTarget = const ForumDetailHandoffTarget(
      postId: '2042219067430928384',
      initialTitle: 'How to wire Radish Flutter forum reading',
    );
    await pumpForumPage();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(consumed, 2);
    expect(find.text('帖子详情'), findsWidgets);
  });
}
