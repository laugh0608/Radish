import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/forum/presentation/forum_detail_page.dart';

void main() {
  testWidgets('renders public detail comments and loads more pages',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: _PagedForumRepository(),
          postId: 'post-42',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('评论'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('评论'), findsOneWidget);
    expect(find.text('只读上下文'), findsOneWidget);
    expect(find.text('应用内打开'), findsWidgets);
    expect(find.text('公开地址待生成'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsNothing);
    expect(
      find.text('仅阅读与最小轻回应，不提供评论提交、点赞、投票或编辑入口'),
      findsOneWidget,
    );
    expect(find.text('共 2 条轻回应'), findsOneWidget);
    expect(find.text('radish：学到了'), findsOneWidget);
    expect(find.text('已加载 2 / 3 条根评论'), findsWidgets);
    expect(find.text('Root comment one'), findsOneWidget);
    expect(find.text('回复 @luobo'), findsOneWidget);
    expect(find.text('加载更多评论'), findsOneWidget);

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
      MaterialApp(
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

    expect(find.text('只读上下文'), findsOneWidget);
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
      MaterialApp(
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

  testWidgets('detail error keeps target source and retry context',
      (tester) async {
    tester.view.physicalSize = const Size(390, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
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
      MaterialApp(
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
      MaterialApp(
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
      MaterialApp(
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
      MaterialApp(
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

    await tester.enterText(find.byType(TextField).last, '同感');
    await tester.tap(find.text('发布轻回应'));
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
      MaterialApp(
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

    await tester.enterText(find.byType(TextField).last, '我也来一句');
    await tester.tap(find.text('发布轻回应'));
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
      MaterialApp(
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

    await tester.enterText(find.byType(TextField).last, '发不出去的轻回应');
    await tester.tap(find.text('发布轻回应'));
    await tester.pumpAndSettle();

    expect(find.text('轻回应发布失败'), findsOneWidget);
    expect(find.text('轻回应服务暂时不可用'), findsOneWidget);
    expect(find.text('current：发不出去的轻回应'), findsNothing);
    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('Root comment one'), findsOneWidget);
    expect(find.text('radish：学到了'), findsOneWidget);
  });

  testWidgets('opens profile handoff from detail author and comment author',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;

    await tester.pumpWidget(
      MaterialApp(
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

  testWidgets('navigates directly to target child comment by commentId',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
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
}

class _PagedForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    if (pageIndex == 1) {
      return ForumCommentPage.fromJson({
        'voPageIndex': 1,
        'voPageSize': 2,
        'voTotal': 3,
        'voItems': const [
          {
            'voId': 'comment-1',
            'voPostId': 'post-42',
            'voContent': 'Root comment one',
            'voAuthorId': 'user-1',
            'voAuthorName': 'radish',
            'voLikeCount': 3,
            'voReplyCount': 1,
            'voIsSofa': true,
            'voCreateTime': '2026-04-20T08:05:00Z',
            'voChildrenTotal': 2,
          },
          {
            'voId': 'comment-2',
            'voPostId': 'post-42',
            'voContent': 'Root comment two',
            'voAuthorId': 'user-2',
            'voAuthorName': 'guest',
            'voLikeCount': 1,
            'voReplyToUserName': 'luobo',
            'voReplyToCommentSnapshot': 'Original point from the author.',
            'voCreateTime': '2026-04-20T08:08:00Z',
          },
        ],
      });
    }

    return ForumCommentPage.fromJson({
      'voPageIndex': 2,
      'voPageSize': 2,
      'voTotal': 3,
      'voItems': const [
        {
          'voId': 'comment-3',
          'voPostId': 'post-42',
          'voContent': 'Root comment three',
          'voAuthorId': 'user-3',
          'voAuthorName': 'reader',
          'voLikeCount': 0,
          'voCreateTime': '2026-04-20T08:10:00Z',
        },
      ],
    });
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (parentId != 'comment-1') {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 0,
        comments: [],
      );
    }

    if (pageIndex == 1) {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 2,
        comments: [
          ForumCommentSummary(
            id: 'reply-1',
            postId: 'post-42',
            content: 'Child comment one',
            authorId: 'user-4',
            authorName: 'child-a',
            parentId: 'comment-1',
            rootId: 'comment-1',
            replyToUserName: 'radish',
            replyToCommentSnapshot: 'Root comment one',
            createTime: '2026-04-20T08:06:00Z',
          ),
        ],
      );
    }

    return const ForumChildCommentPage(
      pageIndex: 2,
      pageSize: 5,
      totalCount: 2,
      comments: [
        ForumCommentSummary(
          id: 'reply-2',
          postId: 'post-42',
          content: 'Child comment two',
          authorId: 'user-5',
          authorName: 'child-b',
          parentId: 'comment-1',
          rootId: 'comment-1',
          createTime: '2026-04-20T08:07:00Z',
        ),
      ],
    );
  }
}

class _PublicIdForumRepository extends _PagedForumRepository {
  String? lastRootCommentsPostId;
  String? lastQuickReplyPostId;

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return const ForumPostDetail(
      id: '2042219761177198592',
      publicId: 'pst_01HZPUBLICROUTEID',
      title: '测试问答帖子',
      summary: '测试测试',
      content: '# 测试问答帖子\n\n测试测试',
      contentType: 'markdown',
      categoryId: 'category-1',
      categoryName: '生活随笔',
      authorId: 'user-9',
      authorName: 'test',
      isQuestion: true,
      isSolved: true,
      commentCount: 3,
      createTime: '2026-04-09T12:35:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    lastRootCommentsPostId = postId;
    return super.getRootCommentsPage(
      postId: postId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      sortBy: sortBy,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) {
    lastQuickReplyPostId = postId;
    return super.getQuickReplyWall(postId: postId, take: take);
  }
}

class _CountingQuickReplyForumRepository extends _PagedForumRepository {
  int detailRequests = 0;
  int rootCommentRequests = 0;
  int createQuickReplyRequests = 0;

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    detailRequests += 1;
    return super.getPostDetail(postId: postId);
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    rootCommentRequests += 1;
    return super.getRootCommentsPage(
      postId: postId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      sortBy: sortBy,
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    createQuickReplyRequests += 1;
    return super.createQuickReply(
      postId: postId,
      content: content,
      accessToken: accessToken,
    );
  }
}

class _QuickReplySubmitFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }
}

class _DetailFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    throw const RadishApiClientException('详情服务暂时不可用');
  }
}

class _CommentNavigationFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) {
    throw const RadishApiClientException('评论定位服务暂时不可用');
  }
}

class _CommentFailingForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    throw const RadishApiClientException('评论服务暂时不可用');
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('回复服务暂时不可用');
  }
}

class _EmptyCommentForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return ForumCommentPage.fromJson({
      'voPageIndex': 1,
      'voPageSize': pageSize,
      'voTotal': 0,
      'voItems': const [],
    });
  }
}
