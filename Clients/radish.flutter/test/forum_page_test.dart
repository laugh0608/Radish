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

    expect(find.text('Loading forum feed...'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(
        find.text('How to wire Radish Flutter forum reading'), findsOneWidget);
    expect(find.text('Top'), findsOneWidget);
    expect(find.text('Question'), findsOneWidget);
    expect(find.text('42 comments'), findsOneWidget);
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

    expect(find.text('Forum feed unavailable'), findsOneWidget);
    expect(find.text('Forum API is unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
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
      dataCount: 0,
      pageCount: 0,
      comments: [],
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
    throw const RadishApiClientException('Forum API is unreachable');
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    throw const RadishApiClientException('Forum detail API is unreachable');
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    throw const RadishApiClientException('Comments API is unreachable');
  }
}
