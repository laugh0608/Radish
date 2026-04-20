import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/docs/presentation/docs_page.dart';

void main() {
  testWidgets('renders docs from repository', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DocsPage(
          environment: const AppEnvironment.development(),
          repository: _SuccessDocsRepository(),
        ),
      ),
    );

    expect(find.text('Loading docs feed...'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('/docs/flutter-docs-scope'), findsWidgets);
    expect(find.text('2 documents'), findsOneWidget);
  });

  testWidgets('renders docs error state when repository fails', (tester) async {
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
}

class _FailingDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('Docs API is unreachable');
  }
}
