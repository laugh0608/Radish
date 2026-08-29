part of 'forum_detail_page_test.dart';

void runForumDetailNavigationCases() {
  testWidgets('opens profile handoff from detail author and comment author',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          onOpenProfileUser: (userId) {
            openedUserId = userId;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('luobo').first);
    await tester.pumpAndSettle();
    expect(openedUserId, 'user-9');

    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('radish'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.text('radish'));
    await tester.pumpAndSettle();
    expect(openedUserId, 'user-1');
  });

  testWidgets('hides fallback author and category ids in detail metadata',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 1600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _MissingMetadataForumRepository(),
          postId: '2042219067430928384',
          onOpenProfileUser: (userId) {
            openedUserId = userId;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('未知用户'), findsOneWidget);
    expect(find.text('未分类'), findsOneWidget);
    expect(find.text('用户 2042219067430928399'), findsNothing);
    expect(find.text('分类 2042219067430928400'), findsNothing);

    await tester.tap(find.text('未知用户'));
    await tester.pumpAndSettle();

    expect(openedUserId, '2042219067430928399');
  });

  testWidgets('navigates directly to target child comment by commentId',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
          commentId: 'reply-2',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    final scrollableState =
        tester.state<ScrollableState>(find.byType(Scrollable).first);
    expect(scrollableState.position.pixels, greaterThan(0));

    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Child comment two'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('已加载 2 / 2 条回复'), findsOneWidget);
    expect(find.text('Child comment two'), findsOneWidget);
  });
}

abstract class _BaseForumRepository implements ForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    return const [
      ForumCategorySummary(
        id: 'category-1',
        name: 'General',
      ),
    ];
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: '论坛详情回流',
      summary: 'Tap through to the public native detail page.',
      content: '# Native detail\n\nForum detail body.',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      commentCount: 3,
      createTime: '2026-04-20T08:00:00Z',
      updateTime: '2026-04-20T10:30:00Z',
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: 'comment-1',
      parentCommentId: 'comment-1',
      isRootComment: false,
      rootPageIndex: 1,
      childPageIndex: 2,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 2,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: 'post-42',
          authorId: 'user-1',
          authorName: 'radish',
          content: '学到了',
          createTime: '2026-04-20T08:11:00Z',
        ),
        ForumQuickReplySummary(
          id: 'quick-2',
          postId: 'post-42',
          authorId: 'user-2',
          authorName: 'guest',
          content: '好耶 🙂',
          createTime: '2026-04-20T08:12:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T08:13:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }

  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    return ForumQuestionDetail(
      postId: postId,
      isSolved: false,
      answerCount: 1,
      answers: [
        ForumAnswerSummary(
          id: 'answer-created',
          postId: postId,
          authorId: 'user-current',
          authorName: 'current',
          content: content,
          createTime: '2026-04-20T08:14:00Z',
        ),
      ],
    );
  }

  @override
  Future<String> createPost({
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    return 'post-created';
  }

  @override
  Future<ForumContentEditResult> updatePost({
    required String postId,
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required int expectedContentRevision,
    required String accessToken,
    required String clientSubmissionId,
  }) async =>
      ForumContentEditResult(
        contentRevision: expectedContentRevision + 1,
      );

  @override
  Future<ForumContentEditResult> updateComment({
    required String commentId,
    required String content,
    required int expectedContentRevision,
    required String accessToken,
    required String clientSubmissionId,
  }) async =>
      ForumContentEditResult(
        contentRevision: expectedContentRevision + 1,
      );
}
