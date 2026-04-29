import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/discover/presentation/discover_page.dart';

void main() {
  testWidgets('renders discover summaries from repository', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable);

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _SuccessDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('论坛精选'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('论坛精选'), findsOneWidget);
    expect(find.text('Native discover wiring plan'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('文档精选'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('文档精选'), findsOneWidget);
    expect(find.text('Flutter MVP overview'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('商城精选'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('商城精选'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('只读边界'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('只读边界'), findsOneWidget);
  });

  testWidgets('renders discover error state when repository fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _FailingDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载发现内容'), findsOneWidget);
    expect(find.text('发现内容服务暂时不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });

  testWidgets('supports native tab and profile handoff actions from discover',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable);
    var forumOpened = false;
    var docsOpened = false;
    DocsDocumentSummary? openedDocument;
    String? openedProfileUserId;

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _SuccessDiscoverRepository(),
          onOpenForum: () {
            forumOpened = true;
          },
          onOpenDocs: () {
            docsOpened = true;
          },
          onOpenDocument: (document) {
            openedDocument = document;
          },
          onOpenProfileUser: (userId) {
            openedProfileUserId = userId;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('进入论坛'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('进入论坛'));
    await tester.scrollUntilVisible(
      find.text('进入文档'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('进入文档'));
    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档'));
    await tester.scrollUntilVisible(
      find.text('打开 @luobo'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开 @luobo'));

    expect(forumOpened, isTrue);
    expect(docsOpened, isTrue);
    expect(openedDocument?.slug, 'flutter-mvp-overview');
    expect(openedProfileUserId, '1024');
  });
}

class _SuccessDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Connect real summaries without expanding into details.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'luobo',
          commentCount: 6,
          viewCount: 128,
          isEssence: true,
        ),
      ],
      documents: [
        DocsDocumentSummary(
          id: '3001',
          title: 'Flutter MVP overview',
          slug: 'flutter-mvp-overview',
          summary: 'Current native client scope and boundaries.',
          modifyTime: '2026-04-18T10:00:00Z',
        ),
      ],
      products: [
        DiscoverProductSummary(
          id: '4001',
          name: 'Profile Rename Card',
          productType: 'Consumable',
          price: 120,
          soldCount: 3,
        ),
      ],
    );
  }
}

class _FailingDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) {
    throw const RadishApiClientException('发现内容服务暂时不可用');
  }
}
