import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
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
          commentId: 'reply-2',
          initialTitle: '论坛详情回流',
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();
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
