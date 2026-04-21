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
          initialTitle: 'Forum detail handoff',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Comments'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('Comments'), findsOneWidget);
    expect(find.text('Loaded 2 / 3 root comments'), findsWidgets);
    expect(find.text('Root comment one'), findsOneWidget);
    expect(find.text('Reply to @luobo'), findsOneWidget);
    expect(find.text('Load more comments'), findsOneWidget);

    await tester.tap(find.text('Load more comments'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Loaded 3 / 3 root comments'), findsWidgets);
    expect(find.text('Root comment three'), findsOneWidget);
    expect(find.text('Load more comments'), findsNothing);

    await tester.tap(find.text('Show replies'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Loaded 1 / 2 replies'), findsOneWidget);
    expect(find.text('Child comment one'), findsOneWidget);
    expect(find.text('Load more replies'), findsOneWidget);

    await tester.tap(find.text('Load more replies'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Loaded 2 / 2 replies'), findsOneWidget);
    expect(find.text('Child comment two'), findsOneWidget);
    expect(find.text('Load more replies'), findsNothing);
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
          initialTitle: 'Forum detail handoff',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Comments unavailable'),
      200,
      scrollable: scrollable,
    );

    expect(find.text('Forum detail handoff'), findsWidgets);
    expect(find.text('Comments unavailable'), findsOneWidget);
    expect(find.text('Comments API is unreachable'), findsOneWidget);
    expect(find.text('Retry comments'), findsOneWidget);
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
          initialTitle: 'Forum detail handoff',
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('No public comments are available for this post yet.'),
      200,
      scrollable: scrollable,
    );

    expect(
      find.text('No public comments are available for this post yet.'),
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
          initialTitle: 'Forum detail handoff',
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
      title: 'Forum detail handoff',
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
      return ForumCommentPage(
        page: 1,
        pageSize: pageSize,
        dataCount: 3,
        pageCount: 2,
        comments: const [
          ForumCommentSummary(
            id: 'comment-1',
            postId: 'post-42',
            content: 'Root comment one',
            authorId: 'user-1',
            authorName: 'radish',
            likeCount: 3,
            replyCount: 1,
            isSofa: true,
            createTime: '2026-04-20T08:05:00Z',
            childrenTotal: 2,
          ),
          ForumCommentSummary(
            id: 'comment-2',
            postId: 'post-42',
            content: 'Root comment two',
            authorId: 'user-2',
            authorName: 'guest',
            likeCount: 1,
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'Original point from the author.',
            createTime: '2026-04-20T08:08:00Z',
          ),
        ],
      );
    }

    return ForumCommentPage(
      page: 2,
      pageSize: pageSize,
      dataCount: 3,
      pageCount: 2,
      comments: const [
        ForumCommentSummary(
          id: 'comment-3',
          postId: 'post-42',
          content: 'Root comment three',
          authorId: 'user-3',
          authorName: 'reader',
          likeCount: 0,
          createTime: '2026-04-20T08:10:00Z',
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
    throw const RadishApiClientException('Comments API is unreachable');
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('Replies API is unreachable');
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
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 0,
      comments: [],
    );
  }
}
