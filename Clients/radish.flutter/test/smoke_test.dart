import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';

void main() {
  testWidgets('restores into guest shell when no session exists',
      (tester) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
      ),
    );

    expect(find.text('Restoring session'), findsOneWidget);

    await tester.pump();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('Discover'), findsOneWidget);
    expect(find.text('Guest'), findsOneWidget);
  });

  testWidgets('restores authenticated session into profile boundary', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: const AuthSession(
          accessToken: 'access-token',
          refreshToken: 'refresh-token',
          userId: 'user-42',
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Restored session for user user-42'), findsOneWidget);
  });
}

class _FakeDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [],
      documents: [],
      products: [],
    );
  }
}

class _FakeDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const DocsDocumentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      documents: [],
    );
  }
}

class _FakeForumRepository implements ForumRepository {
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
}
