part of 'forum_detail_page_test.dart';

void runForumDetailReadingCases() {
  testWidgets('renders public detail comments and loads more pages',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();
    expect(find.byKey(const Key('forum-detail-expanded')), findsOneWidget);
    expect(
      find.byKey(const Key('forum-detail-context-rail')),
      findsOneWidget,
    );
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('评论'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('评论'), findsOneWidget);
    expect(find.text('详情上下文'), findsOneWidget);
    expect(find.text('应用内打开'), findsWidgets);
    expect(find.text('公开地址待生成'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsNothing);
    expect(find.textContaining('支持问题回答、评论发布与回复'), findsOneWidget);
    expect(find.text('共 2 条轻回应'), findsOneWidget);
    expect(find.text('radish：学到了'), findsOneWidget);
    expect(find.text('已加载 2 / 3 条根评论'), findsWidgets);
    expect(find.text('Root comment one'), findsOneWidget);
    expect(find.text('回复 @luobo'), findsOneWidget);
    expect(find.text('加载更多评论'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('加载更多评论'),
      100,
      scrollable: scrollable,
    );
    await tester.tap(find.text('加载更多评论'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('已加载 3 / 3 条根评论'), findsWidgets);
    expect(find.text('Root comment three'), findsOneWidget);
    expect(find.text('加载更多评论'), findsNothing);

    await tester.tap(find.text('查看回复'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('已加载 1 / 2 条回复'), findsOneWidget);
    expect(find.text('Child comment one'), findsOneWidget);
    expect(find.text('加载更多回复'), findsOneWidget);

    await tester.tap(find.text('加载更多回复'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('已加载 2 / 2 条回复'), findsOneWidget);
    expect(find.text('Child comment two'), findsOneWidget);
    expect(find.text('加载更多回复'), findsNothing);
  });

  testWidgets('renders handoff source and comment target context',
      (tester) async {
    tester.view.physicalSize = const Size(390, 1200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: '2042219067430928384',
          handoffSource: ForumDetailHandoffSource.notification,
          commentId: 'reply-2',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-detail-compact')), findsOneWidget);
    expect(find.byKey(const Key('forum-detail-context-rail')), findsNothing);
    expect(find.text('详情上下文'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('公开地址待生成'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsNothing);
    expect(find.text('reply-2'), findsOneWidget);
  });

  testWidgets('refresh keeps public id as displayed public route',
      (tester) async {
    tester.view.physicalSize = const Size(390, 1200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PublicIdForumRepository();

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: '2042219761177198592',
          handoffSource: ForumDetailHandoffSource.discover,
          initialTitle: '测试问答帖子',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('/forum/post/pst_01HZPUBLICROUTEID'), findsWidgets);
    expect(find.text('/forum/post/2042219761177198592'), findsNothing);
    expect(repository.lastRootCommentsPostId, '2042219761177198592');
    expect(repository.lastQuickReplyPostId, '2042219761177198592');

    await tester.tap(find.text('刷新详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/pst_01HZPUBLICROUTEID'), findsWidgets);
    expect(find.text('/forum/post/2042219761177198592'), findsNothing);
  });

  testWidgets('copies public forum detail link from public id', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 1200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final clipboard = _ClipboardRecorder()..install();
    addTearDown(clipboard.reset);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PublicIdForumRepository(),
          postId: '2042219761177198592',
          handoffSource: ForumDetailHandoffSource.discover,
          initialTitle: '测试问答帖子',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
      find.text('https://localhost:5000/forum/post/pst_01HZPUBLICROUTEID'),
      findsOneWidget,
    );

    await tester.tap(find.text('复制公开链接'));
    await tester.pump();

    expect(
      clipboard.text,
      'https://localhost:5000/forum/post/pst_01HZPUBLICROUTEID',
    );
    expect(find.text('公开链接已复制'), findsOneWidget);
    expect(find.text('已复制公开链接'), findsOneWidget);
    expect(find.text('https://localhost:5000/forum/post/2042219761177198592'),
        findsNothing);
  });

  testWidgets('detail error keeps target source and retry context',
      (tester) async {
    tester.view.physicalSize = const Size(390, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _DetailFailingForumRepository(),
          postId: 'post-error-42',
          handoffSource: ForumDetailHandoffSource.profileRecentBrowse,
          commentId: 'comment-error-1',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载帖子详情'), findsOneWidget);
    expect(find.text('详情服务暂时不可用'), findsOneWidget);
    expect(find.text('我的最近阅读'), findsWidgets);
    expect(find.text('/forum/post/post-error-42'), findsNothing);
    expect(find.text('comment-error-1'), findsOneWidget);
    expect(
      find.textContaining('目标帖子：详情入口已保留。目标评论：comment-error-1'),
      findsOneWidget,
    );
    expect(find.text('重试'), findsOneWidget);
  });

  testWidgets('comment navigation failure keeps detail readable',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 1600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _CommentNavigationFailingForumRepository(),
          postId: 'post-42',
          handoffSource: ForumDetailHandoffSource.publicProfileComment,
          commentId: 'missing-comment-1',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
      find.text('暂时无法定位目标评论 missing-comment-1，已先打开帖子详情。'),
      findsOneWidget,
    );
    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('Native detail'), findsOneWidget);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('missing-comment-1'), findsOneWidget);
  });

  testWidgets('renders comment error state separately from detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _CommentFailingForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('暂时无法加载评论'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('暂时无法加载评论'), findsOneWidget);
    expect(find.text('评论服务暂时不可用'), findsOneWidget);
    expect(find.text('重试评论'), findsOneWidget);
  });

  testWidgets('renders empty comment state', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _EmptyCommentForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('这篇帖子暂无公开评论。'),
      200,
      scrollable: scrollable,
    );

    expect(
      find.text('这篇帖子暂无公开评论。'),
      findsOneWidget,
    );
  });

  testWidgets('uses a continuous compact reading surface at 390px',
      (tester) async {
    await _setForumViewport(tester, const Size(390, 1000));

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-detail-compact')), findsOneWidget);
    expect(
      find.byKey(const Key('forum-detail-continuous-reading')),
      findsOneWidget,
    );
    expect(
      find.byKey(const Key('forum-detail-inline-navigation')),
      findsOneWidget,
    );
    expect(find.text('社区 / 本帖'), findsOneWidget);
    expect(find.byKey(const Key('forum-detail-context-rail')), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('keeps one reading axis and inline context at 800px',
      (tester) async {
    await _setForumViewport(tester, const Size(800, 1100));

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-detail-medium')), findsOneWidget);
    expect(
      find.byKey(const Key('forum-detail-continuous-reading')),
      findsOneWidget,
    );
    expect(
      find.byKey(const Key('forum-detail-inline-navigation')),
      findsOneWidget,
    );
    expect(find.byKey(const Key('forum-detail-context-rail')), findsNothing);
    expect(tester.takeException(), isNull);
  });

  for (final themeId in RadishThemeId.values) {
    testWidgets(
      'keeps medium forum detail structure in ${themeId.value}',
      (tester) async {
        await _setForumViewport(tester, const Size(800, 1400));

        await tester.pumpWidget(
          _forumTestApp(
            themeId: themeId,
            home: ForumDetailPage(
              environment: const AppEnvironment.development(),
              repository: _PagedForumRepository(),
              postId: 'post-42',
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('forum-detail-medium')), findsOneWidget);
        expect(
          find.byKey(const Key('forum-detail-continuous-reading')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('forum-detail-inline-navigation')),
          findsOneWidget,
        );
        expect(
            find.byKey(const Key('forum-detail-context-rail')), findsNothing);
        expect(find.text('Native detail'), findsOneWidget);
        expect(find.text('评论'), findsWidgets);
        expect(tester.takeException(), isNull, reason: themeId.value);
      },
    );
  }

  testWidgets('uses exact 220 820 250 expanded columns at 1440px',
      (tester) async {
    await _setForumViewport(tester, const Size(1440, 1200));

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-detail-expanded')), findsOneWidget);
    expect(
      tester
          .getSize(find.byKey(const Key('forum-detail-community-rail-220')))
          .width,
      220,
    );
    expect(
      tester
          .getSize(find.byKey(const Key('forum-detail-reading-axis-820')))
          .width,
      820,
    );
    expect(
      tester
          .getSize(find.byKey(const Key('forum-detail-thread-rail-250')))
          .width,
      250,
    );
    expect(find.text('社区导航'), findsOneWidget);
    expect(find.text('线程索引'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('keeps long content readable and reduced-motion jumps stable',
      (tester) async {
    await _setForumViewport(tester, const Size(390, 844));

    await tester.pumpWidget(
      _forumTestApp(
        home: MediaQuery(
          data: const MediaQueryData(disableAnimations: true),
          child: ForumDetailPage(
            environment: const AppEnvironment.development(),
            repository: _LongContentForumRepository(),
            postId: 'post-long',
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final inlineNavigation =
        find.byKey(const Key('forum-detail-inline-navigation'));
    await tester.tap(
      find.descendant(
        of: inlineNavigation,
        matching: find.widgetWithText(OutlinedButton, '评论'),
      ),
    );
    await tester.pump();

    final readingScroll = find.descendant(
      of: find.byKey(const Key('forum-detail-reading-scroll')),
      matching: find.byType(Scrollable),
    );
    final scrollableState = tester.state<ScrollableState>(readingScroll.first);
    expect(scrollableState.position.pixels, greaterThan(0));
    await tester.scrollUntilVisible(
      find.textContaining('长评论结尾'),
      300,
      scrollable: readingScroll.first,
    );
    expect(find.textContaining('长评论结尾'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
