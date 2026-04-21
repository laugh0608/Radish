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
    expect(find.text('2 documents'), findsOneWidget);
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
      find.text('Open document').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open document').first);
    await tester.pumpAndSettle();

    expect(find.text('Docs detail'), findsOneWidget);
    expect(find.text('Back to docs list'), findsOneWidget);
    expect(find.text('Markdown body'), findsOneWidget);
    expect(find.text('Overview'), findsOneWidget);
    expect(find.text('Read docs in Flutter.'), findsOneWidget);

    await tester.tap(find.text('Back to docs list'));
    await tester.pumpAndSettle();

    expect(find.text('Docs feed'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
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

    expect(find.text('Docs feed unavailable'), findsOneWidget);
    expect(find.text('Docs API is unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
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
      find.text('Open document').first,
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('Open document').first);
    await tester.pumpAndSettle();

    expect(find.text('Docs detail unavailable'), findsOneWidget);
    expect(find.text('Docs detail API is unreachable'), findsOneWidget);
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
          '# Overview\nRead docs in Flutter.\n\n- Keep it read-only\n```txt\npublic detail\n```',
    );
  }
}

class _FailingDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('Docs API is unreachable');
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    throw const RadishApiClientException('Docs API is unreachable');
  }
}

class _DetailFailingDocsRepository extends _SuccessDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    throw const RadishApiClientException('Docs detail API is unreachable');
  }
}
