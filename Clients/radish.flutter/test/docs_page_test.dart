import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/docs/presentation/docs_page.dart';

void main() {
  testWidgets('renders docs from repository', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('/docs/flutter-docs-scope'), findsWidgets);
    expect(find.text('共 2 篇文档'), findsOneWidget);
  });

  testWidgets('opens docs detail and returns to docs list', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsOneWidget);
    expect(find.text('返回文档列表'), findsOneWidget);
    expect(find.text('正文'), findsOneWidget);
    expect(find.text('Overview'), findsOneWidget);
    expect(find.text('Read docs in Flutter.'), findsOneWidget);

    await tester.tap(find.text('返回文档列表'));
    await tester.pumpAndSettle();

    expect(find.text('文档'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
  });

  testWidgets('keeps docs list visible while refresh is pending', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PendingRefreshDocsRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: repository,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter docs scope'), findsOneWidget);

    repository.refreshCompleter = Completer<DocsDocumentPage>();
    await tester.tap(find.text('刷新文档'));
    await tester.pump();

    expect(find.text('正在刷新文档列表，当前仍展示上次可用文档。'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('正在刷新'), findsOneWidget);

    repository.refreshCompleter!.complete(_updatedDocsDocumentPage());
    await tester.pumpAndSettle();

    expect(find.text('正在刷新文档列表，当前仍展示上次可用文档。'), findsNothing);
    expect(find.text('Updated docs refresh summary'), findsOneWidget);
  });

  testWidgets('keeps docs list visible when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _RefreshFailingDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter docs scope'), findsOneWidget);

    await tester.tap(find.text('刷新文档'));
    await tester.pumpAndSettle();

    expect(find.text('刷新文档失败'), findsOneWidget);
    expect(find.text('文档刷新服务暂时不可用'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('暂时无法加载文档'), findsNothing);
  });

  testWidgets('clears docs refresh issue after successful refresh', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _FailThenRecoverDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('刷新文档'));
    await tester.pumpAndSettle();

    expect(find.text('刷新文档失败'), findsOneWidget);

    await tester.tap(find.text('刷新文档'));
    await tester.pumpAndSettle();

    expect(find.text('刷新文档失败'), findsNothing);
    expect(find.text('Updated docs refresh summary'), findsOneWidget);
  });

  testWidgets('searches docs and opens result detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final recordedTargets = <DocsDetailHandoffTarget>[];
    final scrollable = find.byType(Scrollable).first;

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
          onRecordDocumentTarget: recordedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'boundary');
    await tester.tap(find.text('搜索文档'));
    await tester.pumpAndSettle();

    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsNothing);

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('Public docs reading boundary detail'), findsOneWidget);
    expect(recordedTargets.last.slug, 'public-docs-reading-boundary');
    expect(recordedTargets.last.source, DocsDetailHandoffSource.docsList);
  });

  testWidgets('system back from searched detail returns to search results', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'boundary');
    await tester.tap(find.text('搜索文档'));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('Public docs reading boundary detail'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('文档'), findsOneWidget);
    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);
    expect(find.text('Public docs reading boundary detail'), findsNothing);
  });

  testWidgets('searched detail return restores list scroll context', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(420, 800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _ManySearchDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), ' guide ');
    await tester.tap(find.text('搜索文档'));
    await tester.pumpAndSettle();

    expect(find.text('“guide” 共 16 篇文档'), findsOneWidget);

    await tester.ensureVisible(find.text('Guide doc 12'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Guide doc 12'));
    await tester.pumpAndSettle();

    expect(find.text('Guide doc 12 detail'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('“guide” 共 16 篇文档'), findsOneWidget);
    expect(find.text('Guide doc 12'), findsOneWidget);
    expect(tester.getTopLeft(find.text('Guide doc 12')).dy, lessThan(800));
    expect(find.text('Guide doc 12 detail'), findsNothing);
  });

  testWidgets('long docs slugs do not overflow on narrow screens', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 1400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _LongSlugDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(
      find.text('A very long public docs slug card'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pumpAndSettle();

    expect(find.text('只读上下文'), findsOneWidget);
    expect(find.text('A very long public docs slug card'), findsWidgets);
    expect(tester.takeException(), isNull);
  });

  testWidgets('opens linked docs from inline docs detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final recordedTargets = <DocsDetailHandoffTarget>[];
    final scrollable = find.byType(Scrollable).first;

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
          onRecordDocumentTarget: recordedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    final linkedDocsAction = find.text('公开阅读边界').last;
    await tester.ensureVisible(linkedDocsAction);
    await tester.tap(linkedDocsAction);
    await tester.pumpAndSettle();

    expect(find.text('Public docs reading boundary detail'), findsOneWidget);
    expect(find.text('Boundary body.'), findsOneWidget);
    expect(recordedTargets.last.slug, 'public-docs-reading-boundary');
    expect(recordedTargets.last.source, DocsDetailHandoffSource.docsLink);
  });

  testWidgets('opens handoff docs detail as route and records recent target', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final recordedTargets = <DocsDetailHandoffTarget>[];

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
          handoffTarget: const DocsDetailHandoffTarget(
            slug: 'flutter-docs-scope',
            source: DocsDetailHandoffSource.discover,
            initialTitle: 'Radish Flutter docs scope',
          ),
          onRecordDocumentTarget: recordedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('来源：发现'), findsOneWidget);
    expect(find.text('只读上下文'), findsOneWidget);
    expect(find.text('仅阅读，不提供编辑、发布或版本治理入口'), findsOneWidget);
    expect(find.text('返回来源'), findsOneWidget);
    expect(find.text('Read docs in Flutter.'), findsOneWidget);
    expect(recordedTargets, hasLength(1));
    expect(recordedTargets.single.slug, 'flutter-docs-scope');
    expect(recordedTargets.single.source, DocsDetailHandoffSource.discover);

    await tester.tap(find.text('返回来源'));
    await tester.pumpAndSettle();

    expect(find.text('文档'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
  });

  testWidgets('opens linked docs from handoff detail route', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final recordedTargets = <DocsDetailHandoffTarget>[];

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
          handoffTarget: const DocsDetailHandoffTarget(
            slug: 'flutter-docs-scope',
            source: DocsDetailHandoffSource.discover,
            initialTitle: 'Radish Flutter docs scope',
          ),
          onRecordDocumentTarget: recordedTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    final linkedDocsAction = find.text('公开阅读边界').last;
    await tester.ensureVisible(linkedDocsAction);
    await tester.tap(linkedDocsAction);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：文档内链'), findsOneWidget);
    expect(find.text('来源：文档内链'), findsOneWidget);
    expect(find.text('Public docs reading boundary detail'), findsOneWidget);
    expect(recordedTargets.last.slug, 'public-docs-reading-boundary');
    expect(recordedTargets.last.source, DocsDetailHandoffSource.docsLink);

    await tester.tap(find.text('返回来源').last);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsWidgets);
  });

  testWidgets('renders docs error state when repository fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _FailingDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载文档'), findsOneWidget);
    expect(find.text('文档服务暂时不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });

  testWidgets('renders docs detail error state when detail request fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).first;

    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _DetailFailingDocsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载文档详情'), findsOneWidget);
    expect(find.text('文档详情服务暂时不可用'), findsOneWidget);
    expect(
      find.text(
        '目标地址：/docs/flutter-docs-scope。可以返回来源后重试，或稍后再次打开文档详情。',
      ),
      findsOneWidget,
    );
  });
}

class _SuccessDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    final normalizedKeyword = keyword?.trim().toLowerCase();
    final documents = _documents.where((document) {
      if (normalizedKeyword == null || normalizedKeyword.isEmpty) {
        return true;
      }

      return document.title.toLowerCase().contains(normalizedKeyword) ||
          document.slug.toLowerCase().contains(normalizedKeyword) ||
          (document.summary?.toLowerCase().contains(normalizedKeyword) ??
              false);
    }).toList();

    return DocsDocumentPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: documents.length,
      pageCount: 1,
      documents: documents,
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    if (slug == 'public-docs-reading-boundary') {
      return const DocsDocumentDetail(
        id: '3002',
        title: 'Public docs reading boundary detail',
        slug: 'public-docs-reading-boundary',
        summary: 'Keep editing and governance outside the first native batch.',
        sourceType: 'Markdown',
        visibility: 1,
        status: 1,
        publishedAt: '2026-04-19T08:00:00Z',
        markdownContent: '# Boundary detail\nBoundary body.',
      );
    }

    return DocsDocumentDetail(
      id: '3001',
      title: 'Radish Flutter docs scope',
      slug: slug,
      summary: 'Read-only docs list wiring for the native client.',
      sourceType: 'Markdown',
      visibility: 1,
      status: 1,
      modifyTime: '2026-04-20T08:00:00Z',
      markdownContent:
          '# Overview\nRead docs in Flutter.\n\n- Keep it read-only\n- 继续阅读 [公开阅读边界](/docs/public-docs-reading-boundary)\n```txt\npublic detail\n/docs/not-a-link-in-code\n```',
    );
  }
}

const _documents = [
  DocsDocumentSummary(
    id: '3001',
    title: 'Radish Flutter docs scope',
    slug: 'flutter-docs-scope',
    summary: 'Read-only docs list wiring for the native client.',
    modifyTime: '2026-04-20T08:00:00Z',
  ),
  DocsDocumentSummary(
    id: '3002',
    title: 'Public docs reading boundary',
    slug: 'public-docs-reading-boundary',
    summary: 'Keep editing and governance outside the first native batch.',
    publishedAt: '2026-04-19T08:00:00Z',
  ),
];

class _FailingDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    throw const RadishApiClientException('文档服务暂时不可用');
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    throw const RadishApiClientException('文档服务暂时不可用');
  }
}

class _PendingRefreshDocsRepository extends _SuccessDocsRepository {
  int _calls = 0;
  Completer<DocsDocumentPage>? refreshCompleter;

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getDocumentPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        keyword: keyword,
      );
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _RefreshFailingDocsRepository extends _SuccessDocsRepository {
  int _calls = 0;

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getDocumentPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        keyword: keyword,
      );
    }

    throw const RadishApiClientException('文档刷新服务暂时不可用');
  }
}

class _FailThenRecoverDocsRepository extends _SuccessDocsRepository {
  int _calls = 0;

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getDocumentPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        keyword: keyword,
      );
    }

    if (_calls == 2) {
      throw const RadishApiClientException('文档刷新服务暂时不可用');
    }

    return Future.value(_updatedDocsDocumentPage());
  }
}

DocsDocumentPage _updatedDocsDocumentPage() {
  return const DocsDocumentPage(
    page: 1,
    pageSize: 20,
    dataCount: 1,
    pageCount: 1,
    documents: [
      DocsDocumentSummary(
        id: '3003',
        title: 'Updated docs refresh summary',
        slug: 'updated-docs-refresh-summary',
        summary: 'Updated public docs summary after refresh.',
        modifyTime: '2026-04-20T09:00:00Z',
      ),
    ],
  );
}

class _DetailFailingDocsRepository extends _SuccessDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    throw const RadishApiClientException('文档详情服务暂时不可用');
  }
}

class _LongSlugDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    return const DocsDocumentPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      documents: [
        DocsDocumentSummary(
          id: '9001',
          title: 'A very long public docs slug card',
          slug:
              'guide-m15-test-rollback-rehearsal-2026-04-06-with-extra-native-client-overflow-check',
          summary:
              'This document intentionally uses a long slug for narrow screens.',
          publishedAt: '2026-04-19T08:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    return DocsDocumentDetail(
      id: '9001',
      title: 'A very long public docs slug card',
      slug: slug,
      markdownContent: '# Long slug',
    );
  }
}

class _ManySearchDocsRepository implements DocsRepository {
  static final _documents = List<DocsDocumentSummary>.generate(
    16,
    (index) {
      final number = index + 1;
      return DocsDocumentSummary(
        id: 'guide-$number',
        title: 'Guide doc $number',
        slug: 'guide-doc-$number',
        summary: 'Searchable guide result $number.',
        publishedAt: '2026-04-20T08:00:00Z',
      );
    },
  );

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    final normalizedKeyword = keyword?.trim().toLowerCase();
    final documents = _documents.where((document) {
      if (normalizedKeyword == null || normalizedKeyword.isEmpty) {
        return true;
      }

      return document.title.toLowerCase().contains(normalizedKeyword) ||
          document.slug.toLowerCase().contains(normalizedKeyword) ||
          (document.summary?.toLowerCase().contains(normalizedKeyword) ??
              false);
    }).toList();

    return DocsDocumentPage(
      page: 1,
      pageSize: pageSize,
      dataCount: documents.length,
      pageCount: 1,
      documents: documents,
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    final title =
        _documents.firstWhere((document) => document.slug == slug).title;

    return DocsDocumentDetail(
      id: slug,
      title: '$title detail',
      slug: slug,
      markdownContent: '# $title\nSearch result detail body.',
      visibility: 1,
      status: 1,
      publishedAt: '2026-04-20T08:00:00Z',
    );
  }
}
