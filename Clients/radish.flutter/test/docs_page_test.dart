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

    final scrollable = find.byType(Scrollable);

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

  testWidgets('opens linked docs from inline docs detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final recordedTargets = <DocsDetailHandoffTarget>[];
    final scrollable = find.byType(Scrollable);

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

    final scrollable = find.byType(Scrollable);

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
  });
}

class _SuccessDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return DocsDocumentPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: 2,
      pageCount: 1,
      documents: const [
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
          summary:
              'Keep editing and governance outside the first native batch.',
          publishedAt: '2026-04-19T08:00:00Z',
        ),
      ],
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

class _FailingDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
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

class _DetailFailingDocsRepository extends _SuccessDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    throw const RadishApiClientException('文档详情服务暂时不可用');
  }
}
