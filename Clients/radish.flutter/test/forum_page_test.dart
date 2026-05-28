import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/forum/presentation/forum_page.dart';

void main() {
  testWidgets('renders forum posts from repository', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessForumRepository(),
        ),
      ),
    );

    expect(find.text('正在加载论坛内容...'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(
        find.text('How to wire Radish Flutter forum reading'), findsOneWidget);
    expect(find.text('置顶'), findsOneWidget);
    expect(find.text('提问'), findsOneWidget);
    expect(find.text('42 条评论'), findsOneWidget);
  });

  testWidgets('renders forum error state when repository fails',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _FailingForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载论坛'), findsOneWidget);
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
      MaterialApp(
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

    expect(find.text('正在刷新论坛列表，当前仍展示上次可用帖子。'), findsOneWidget);
    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );
    expect(find.text('正在刷新'), findsOneWidget);

    repository.refreshCompleter!.complete(_updatedForumPostPage());
    await tester.pumpAndSettle();

    expect(find.text('正在刷新论坛列表，当前仍展示上次可用帖子。'), findsNothing);
    expect(find.text('Updated forum refresh summary'), findsOneWidget);
  });

  testWidgets('keeps forum posts visible when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
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

    expect(find.text('刷新论坛失败'), findsOneWidget);
    expect(find.text('论坛刷新服务暂时不可用'), findsOneWidget);
    expect(
      find.text('How to wire Radish Flutter forum reading'),
      findsOneWidget,
    );
    expect(find.text('暂时无法加载论坛'), findsNothing);
  });

  testWidgets('clears forum refresh issue after successful refresh',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: ForumPage(
          environment: const AppEnvironment.development(),
          repository: _FailThenRecoverForumRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('刷新'));
    await tester.pumpAndSettle();

    expect(find.text('刷新论坛失败'), findsOneWidget);

    await tester.tap(find.text('刷新'));
    await tester.pumpAndSettle();

    expect(find.text('刷新论坛失败'), findsNothing);
    expect(find.text('Updated forum refresh summary'), findsOneWidget);
  });

  testWidgets('opens author profile handoff from forum feed', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;

    await tester.pumpWidget(
      MaterialApp(
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
      MaterialApp(
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
      MaterialApp(
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
    expect(find.text('帖子详情'), findsWidgets);
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
        MaterialApp(
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

class _SuccessForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return ForumPostPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: 1,
      pageCount: 1,
      posts: const [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'How to wire Radish Flutter forum reading',
          summary:
              'Use the public read-only feed contract first, then expand into detail.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'Luobo',
          viewCount: 256,
          likeCount: 18,
          commentCount: 42,
          answerCount: 3,
          isTop: true,
          isQuestion: true,
          createTime: '2026-04-18T10:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: 'How to wire Radish Flutter forum reading',
      summary:
          'Use the public read-only feed contract first, then expand into detail.',
      content: '# Detail\n\nForum detail body.',
      contentType: 'Markdown',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: '1024',
      authorName: 'Luobo',
      commentCount: 42,
      answerCount: 3,
      isTop: true,
      isQuestion: true,
      createTime: '2026-04-18T10:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      comments: [
        ForumCommentSummary(
          id: 'comment-2048',
          postId: '2042219067430928384',
          content: 'Pinned target comment for handoff verification.',
          authorId: '1025',
          authorName: 'Reader',
          createTime: '2026-04-18T12:00:00Z',
        ),
      ],
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
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 1,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: '2042219067430928384',
          authorId: '1025',
          authorName: 'Reader',
          content: '学到了',
          createTime: '2026-04-18T12:10:00Z',
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
      authorId: 'current-user',
      authorName: 'current',
      content: content,
      createTime: '2026-04-18T12:15:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }
}

class _FailingForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    throw const RadishApiClientException('论坛服务暂时不可用');
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    throw const RadishApiClientException('帖子详情服务暂时不可用');
  }

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

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) {
    throw const RadishApiClientException('评论定位服务暂时不可用');
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应发布服务暂时不可用');
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) {
    throw const RadishApiClientException('评论发布服务暂时不可用');
  }
}

class _PendingRefreshForumRepository extends _SuccessForumRepository {
  int _calls = 0;
  Completer<ForumPostPage>? refreshCompleter;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _RefreshFailingForumRepository extends _SuccessForumRepository {
  int _calls = 0;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    throw const RadishApiClientException('论坛刷新服务暂时不可用');
  }
}

class _FailThenRecoverForumRepository extends _SuccessForumRepository {
  int _calls = 0;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    if (_calls == 2) {
      throw const RadishApiClientException('论坛刷新服务暂时不可用');
    }

    return Future.value(_updatedForumPostPage());
  }
}

ForumPostPage _updatedForumPostPage() {
  return const ForumPostPage(
    page: 1,
    pageSize: 20,
    dataCount: 1,
    pageCount: 1,
    posts: [
      ForumPostSummary(
        id: '2042219067430928385',
        title: 'Updated forum refresh summary',
        summary: 'Updated public forum summary after refresh.',
        categoryId: '9',
        categoryName: 'Engineering',
        authorId: '1024',
        authorName: 'Luobo',
        viewCount: 512,
        likeCount: 20,
        commentCount: 48,
        createTime: '2026-04-18T11:00:00Z',
      ),
    ],
  );
}
