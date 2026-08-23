part of 'forum_page_test.dart';

void registerForumAdaptiveTests() {
  testWidgets('uses a compact continuous feed and fullscreen composer task',
      (tester) async {
    await _setForumPageViewport(tester, const Size(390, 1100));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-layout-compact')), findsOneWidget);
    expect(find.byKey(const Key('forum-continuous-feed')), findsOneWidget);
    expect(find.byKey(const Key('forum-community-insight')), findsNothing);

    await _openForumComposer(tester);

    expect(
      find.byKey(const Key('forum-composer-task-compact')),
      findsOneWidget,
    );
    expect(find.byKey(const Key('forum-composer-scroll')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('uses a bounded medium feed and composer task', (tester) async {
    await _setForumPageViewport(tester, const Size(800, 1100));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-layout-medium')), findsOneWidget);
    expect(find.byKey(const Key('forum-community-insight')), findsNothing);

    await _openForumComposer(tester);

    expect(
      find.byKey(const Key('forum-composer-task-bounded')),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('uses a 904px expanded main axis with community insight',
      (tester) async {
    await _setForumPageViewport(tester, const Size(1440, 1200));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-layout-expanded')), findsOneWidget);
    expect(
      tester.getSize(find.byKey(const Key('forum-main-axis-904'))).width,
      904,
    );
    expect(find.byKey(const Key('forum-community-insight')), findsOneWidget);
    expect(find.text('当前阅读'), findsOneWidget);
    expect(find.text('能力边界'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('keeps compact composer keyboard-safe with reduced motion',
      (tester) async {
    await _setForumPageViewport(tester, const Size(390, 844));

    await tester.pumpWidget(
      _forumPageTestApp(
        mediaQueryData: const MediaQueryData(
          disableAnimations: true,
          viewInsets: EdgeInsets.only(bottom: 300),
        ),
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('forum-open-composer')));
    await tester.pump();

    expect(
      find.byKey(const Key('forum-composer-task-compact')),
      findsOneWidget,
    );
    expect(find.byKey(const Key('forum-composer-scroll')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('bounds long feed text without compact overflow', (tester) async {
    await _setForumPageViewport(tester, const Size(390, 1000));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _LongForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-post-long-post-id')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('shows loading then the resolved forum snapshot', (tester) async {
    await _setForumPageViewport(tester, const Size(390, 1000));
    final repository = _PendingInitialForumRepository();

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: repository,
        ),
      ),
    );
    await tester.pump();

    expect(find.text('正在加载论坛'), findsOneWidget);

    repository.pageCompleter.complete(_updatedForumPostPage());
    await tester.pumpAndSettle();
    expect(find.text('Updated forum refresh summary'), findsOneWidget);
  });

  testWidgets('shows the continuous feed empty state', (tester) async {
    await _setForumPageViewport(tester, const Size(800, 1000));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _EmptyForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('forum-feed-empty')), findsOneWidget);
    expect(find.text('当前没有公开帖子'), findsOneWidget);
  });

  testWidgets('keeps category failure inside the composer boundary',
      (tester) async {
    await _setForumPageViewport(tester, const Size(800, 1000));

    await tester.pumpWidget(
      _forumPageTestApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _CategoryFailingForumRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await _openForumComposer(tester);

    expect(find.text('分类暂不可用'), findsOneWidget);
    expect(find.text('论坛分类服务暂时不可用'), findsOneWidget);
    expect(find.text('重试分类'), findsOneWidget);
    expect(
        find.text('How to wire Radish Flutter forum reading'), findsOneWidget);
  });

  for (final themeId in RadishThemeId.values) {
    testWidgets('keeps representative forum structure in ${themeId.value}',
        (tester) async {
      await _setForumPageViewport(tester, const Size(800, 1000));

      await tester.pumpWidget(
        _forumPageTestApp(
          themeId: themeId,
          home: ForumPage(
            environment: const AppEnvironment.development(),
            repository: _SuccessForumRepository(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('forum-layout-medium')), findsOneWidget);
      expect(find.byKey(const Key('forum-continuous-feed')), findsOneWidget);
      expect(find.text('How to wire Radish Flutter forum reading'),
          findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
}
