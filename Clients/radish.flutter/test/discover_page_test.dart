import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
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
      find.text('发现上下文'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('发现上下文'), findsOneWidget);
    expect(find.text('来源：/discover'), findsOneWidget);
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

  testWidgets('renders partial discover issues without hiding ready sections', (
    tester,
  ) async {
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
          repository: _PartialIssueDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('部分发现内容暂时不可用'), findsOneWidget);
    expect(find.textContaining('商城精选暂时不可用'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Native discover wiring plan'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('Native discover wiring plan'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Flutter MVP overview'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('Flutter MVP overview'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('当前暂无可展示的公开商品。'),
      300,
      scrollable: scrollable,
    );
    expect(find.text('当前暂无可展示的公开商品。'), findsOneWidget);
  });

  testWidgets('keeps current summaries visible while refresh is pending', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PendingRefreshDiscoverRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: repository,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Native discover wiring plan'), findsOneWidget);

    repository.refreshCompleter = Completer<DiscoverSnapshot>();
    await tester.tap(find.text('刷新发现'));
    await tester.pump();

    expect(find.text('正在刷新发现内容，当前仍展示上次可用摘要。'), findsOneWidget);
    expect(find.text('Native discover wiring plan'), findsOneWidget);
    expect(find.text('正在刷新'), findsOneWidget);

    repository.refreshCompleter!.complete(_updatedDiscoverSnapshot());
    await tester.pumpAndSettle();

    expect(find.text('正在刷新发现内容，当前仍展示上次可用摘要。'), findsNothing);
    expect(find.text('Updated discover summary'), findsOneWidget);
  });

  testWidgets('clears partial discover issues after a successful refresh', (
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
          repository: _IssueClearingDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('部分发现内容暂时不可用'), findsOneWidget);
    expect(find.textContaining('商城精选暂时不可用'), findsOneWidget);

    await tester.tap(find.text('刷新发现'));
    await tester.pumpAndSettle();

    expect(find.text('部分发现内容暂时不可用'), findsNothing);
    expect(find.textContaining('商城精选暂时不可用'), findsNothing);
    expect(find.text('Profile Rename Card'), findsOneWidget);
  });

  testWidgets('renders section issues when every discover section fails', (
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
          repository: _AllSectionsIssueDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('部分发现内容暂时不可用'), findsOneWidget);
    expect(find.textContaining('论坛精选暂时不可用'), findsOneWidget);
    expect(find.textContaining('文档精选暂时不可用'), findsOneWidget);
    expect(find.textContaining('商城精选暂时不可用'), findsOneWidget);
    expect(find.text('发现页暂无可公开阅读的内容。'), findsNothing);
  });

  testWidgets('keeps current summaries when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _RefreshFailingDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Native discover wiring plan'), findsOneWidget);

    await tester.tap(find.text('刷新发现'));
    await tester.pumpAndSettle();

    expect(find.text('刷新发现失败'), findsOneWidget);
    expect(find.text('刷新服务暂时不可用'), findsOneWidget);
    expect(find.text('Native discover wiring plan'), findsOneWidget);
    expect(find.text('暂时无法加载发现内容'), findsNothing);
  });

  test('http discover repository keeps ready sections when one section fails',
      () async {
    final repository = HttpDiscoverRepository(
      apiClient: _SectionFailingApiClient(),
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final snapshot = await repository.getSnapshot(pageSize: 4);

    expect(snapshot.forumPosts.single.id, '2042219067430928384');
    expect(snapshot.documents.single.slug, 'flutter-mvp-overview');
    expect(snapshot.products, isEmpty);
    expect(snapshot.sectionIssues.single.section, DiscoverSection.shop);
    expect(snapshot.sectionIssues.single.title, '商城精选暂时不可用');
    expect(snapshot.sectionIssues.single.message, '商城服务暂时不可用');
  });

  testWidgets('keeps discover summary text constrained on narrow screens', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 1600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable);

    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _LongTextDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('发现上下文'),
      250,
      scrollable: scrollable,
    );
    expect(find.text('发现上下文'), findsOneWidget);
    expect(find.text('公开只读'), findsWidgets);
    await tester.scrollUntilVisible(
      find.textContaining('A very long discover post title'),
      250,
      scrollable: scrollable,
    );
    expect(
      find.textContaining('A very long discover post title'),
      findsOneWidget,
    );
    await tester.scrollUntilVisible(
      find.textContaining('/docs/flutter-mvp-overview-with-a-very-long-slug'),
      250,
      scrollable: scrollable,
    );
    expect(
      find.textContaining('/docs/flutter-mvp-overview-with-a-very-long-slug'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
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
    var leaderboardOpened = false;
    var shopOpened = false;
    DocsDocumentSummary? openedDocument;
    ForumDetailHandoffTarget? openedForumTarget;
    DiscoverProductSummary? openedProduct;
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
          onOpenLeaderboard: () {
            leaderboardOpened = true;
          },
          onOpenShop: () {
            shopOpened = true;
          },
          onOpenDocument: (document) {
            openedDocument = document;
          },
          onOpenForumDetailTarget: (target) {
            openedForumTarget = target;
          },
          onOpenShopProduct: (product) {
            openedProduct = product;
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
      find.text('打开 @luobo'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开 @luobo'));
    await tester.scrollUntilVisible(
      find.text('打开帖子'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开帖子'));
    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档'));
    await tester.scrollUntilVisible(
      find.text('查看详情'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('查看详情'));
    await tester.scrollUntilVisible(
      find.text('查看全部商品'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('查看全部商品'));
    await tester.scrollUntilVisible(
      find.text('打开榜单').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开榜单').first);

    expect(forumOpened, isTrue);
    expect(docsOpened, isTrue);
    expect(leaderboardOpened, isTrue);
    expect(shopOpened, isTrue);
    expect(openedDocument?.slug, 'flutter-mvp-overview');
    expect(openedForumTarget?.postId, '2042219067430928384');
    expect(openedForumTarget?.initialTitle, 'Native discover wiring plan');
    expect(openedForumTarget?.source, ForumDetailHandoffSource.discover);
    expect(openedProduct?.id, '4001');
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

class _PendingRefreshDiscoverRepository implements DiscoverRepository {
  int _calls = 0;
  Completer<DiscoverSnapshot>? refreshCompleter;

  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return _SuccessDiscoverRepository().getSnapshot(pageSize: pageSize);
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _IssueClearingDiscoverRepository implements DiscoverRepository {
  int _calls = 0;

  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return _PartialIssueDiscoverRepository().getSnapshot(pageSize: pageSize);
    }

    return _SuccessDiscoverRepository().getSnapshot(pageSize: pageSize);
  }
}

class _AllSectionsIssueDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: <ForumPostSummary>[],
      documents: <DocsDocumentSummary>[],
      products: <DiscoverProductSummary>[],
      sectionIssues: [
        DiscoverSectionIssue(
          section: DiscoverSection.forum,
          message: '论坛服务暂时不可用',
        ),
        DiscoverSectionIssue(
          section: DiscoverSection.docs,
          message: '文档服务暂时不可用',
        ),
        DiscoverSectionIssue(
          section: DiscoverSection.shop,
          message: '商城服务暂时不可用',
        ),
      ],
    );
  }
}

class _RefreshFailingDiscoverRepository implements DiscoverRepository {
  int _calls = 0;

  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return _SuccessDiscoverRepository().getSnapshot(pageSize: pageSize);
    }

    throw const RadishApiClientException('刷新服务暂时不可用');
  }
}

class _PartialIssueDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    final snapshot = await _SuccessDiscoverRepository().getSnapshot(
      pageSize: pageSize,
    );

    return DiscoverSnapshot(
      forumPosts: snapshot.forumPosts,
      documents: snapshot.documents,
      products: const <DiscoverProductSummary>[],
      sectionIssues: const [
        DiscoverSectionIssue(
          section: DiscoverSection.shop,
          message: '商城服务暂时不可用',
        ),
      ],
    );
  }
}

DiscoverSnapshot _updatedDiscoverSnapshot() {
  return const DiscoverSnapshot(
    forumPosts: [
      ForumPostSummary(
        id: '2042219067430928385',
        title: 'Updated discover summary',
        summary: 'Updated public summary after refresh.',
        categoryId: '9',
        categoryName: 'Engineering',
        authorId: '1024',
        authorName: 'luobo',
        commentCount: 8,
        viewCount: 256,
      ),
    ],
    documents: [
      DocsDocumentSummary(
        id: '3002',
        title: 'Updated Flutter MVP overview',
        slug: 'updated-flutter-mvp-overview',
        summary: 'Updated native client scope and boundaries.',
      ),
    ],
    products: [
      DiscoverProductSummary(
        id: '4002',
        name: 'Updated Profile Rename Card',
        productType: 'Consumable',
        price: 160,
      ),
    ],
  );
}

class _LongTextDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: '204221906743092838488888888888888888888',
          title:
              'A very long discover post title that should stay constrained on a narrow Android viewport',
          summary:
              'This summary intentionally keeps going so the native discover page can prove that long public previews do not break the summary card layout.',
          categoryId: '9',
          categoryName:
              'Engineering-and-product-context-with-a-long-category-name',
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
          title:
              'Flutter MVP overview with a long title that stays inside the card',
          slug:
              'flutter-mvp-overview-with-a-very-long-slug-for-narrow-discover-cards',
          summary:
              'Current native client scope and boundaries with enough content to exercise truncation.',
          modifyTime: '2026-04-18T10:00:00Z',
        ),
      ],
      products: [
        DiscoverProductSummary(
          id: '4001',
          name:
              'Profile Rename Card With A Long Display Name For Discover Summary',
          productType: 'Consumable',
          price: 120,
          soldCount: 3,
        ),
      ],
    );
  }
}

class _SectionFailingApiClient implements RadishApiClient {
  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    final path = uri.path;
    if (path == '/api/v1/Shop/GetProducts') {
      throw const RadishApiClientException('商城服务暂时不可用');
    }

    if (path == '/api/v1/Post/GetList') {
      return decode({
        'data': [
          {
            'voId': '2042219067430928384',
            'voTitle': 'Native discover wiring plan',
            'voSummary': 'Connect real summaries.',
            'voCategoryId': '9',
            'voCategoryName': 'Engineering',
            'voAuthorId': '1024',
            'voAuthorName': 'luobo',
            'voCommentCount': 6,
            'voViewCount': 128,
          },
        ],
      });
    }

    if (path == '/api/v1/Wiki/GetList') {
      return decode({
        'data': [
          {
            'voId': '3001',
            'voTitle': 'Flutter MVP overview',
            'voSlug': 'flutter-mvp-overview',
            'voSummary': 'Current native client scope and boundaries.',
            'voModifyTime': '2026-04-18T10:00:00Z',
          },
        ],
      });
    }

    throw RadishApiClientException('Unexpected path: $path');
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }
}
