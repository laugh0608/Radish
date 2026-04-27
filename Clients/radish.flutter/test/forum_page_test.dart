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
    expect(find.text('评论'), findsOneWidget);
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
}
