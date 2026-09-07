part of 'docs_page_test.dart';

void registerDocsAdaptiveTests() {
  testWidgets('compact keeps catalog and reader as exclusive tasks', (
    tester,
  ) async {
    await _setDocsTestViewport(tester, const Size(390, 900));
    await _pumpDocsPage(tester, repository: _SuccessDocsRepository());

    expect(
        find.byKey(const Key('docs-layout-compact-catalog')), findsOneWidget);
    expect(find.byKey(const Key('docs-reader-surface')), findsNothing);

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      160,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('docs-layout-compact-reader')), findsOneWidget);
    expect(find.byKey(const Key('docs-catalog-flow')), findsNothing);
    expect(find.text('返回文档列表'), findsOneWidget);

    await tester.tap(find.text('返回文档列表'));
    await tester.pumpAndSettle();

    expect(
        find.byKey(const Key('docs-layout-compact-catalog')), findsOneWidget);
  });

  testWidgets('medium keeps catalog and body visible together', (tester) async {
    await _setDocsTestViewport(tester, const Size(800, 1100));
    await _pumpDocsPage(tester, repository: _SuccessDocsRepository());

    expect(find.byKey(const Key('docs-layout-medium')), findsOneWidget);
    expect(find.byKey(const Key('docs-catalog-pane')), findsOneWidget);
    expect(find.byKey(const Key('docs-reader-empty')), findsOneWidget);

    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('docs-catalog-pane')), findsOneWidget);
    expect(find.byKey(const Key('docs-reader-surface')), findsOneWidget);
    expect(find.byKey(const Key('docs-document-body')), findsOneWidget);
  });

  testWidgets('expanded uses the frozen 280 and 904 reading axes', (
    tester,
  ) async {
    await _setDocsTestViewport(tester, const Size(1440, 1100));
    await _pumpDocsPage(tester, repository: _SuccessDocsRepository());

    expect(find.byKey(const Key('docs-layout-expanded')), findsOneWidget);
    expect(
      tester.getSize(find.byKey(const Key('docs-catalog-axis-280'))).width,
      280,
    );
    expect(
      tester.getSize(find.byKey(const Key('docs-reader-main-axis-904'))).width,
      904,
    );
  });

  for (final themeId in RadishThemeId.values) {
    testWidgets('medium Docs structure is stable in ${themeId.value}', (
      tester,
    ) async {
      await _setDocsTestViewport(tester, const Size(800, 1000));
      await _pumpDocsPage(
        tester,
        repository: _SuccessDocsRepository(),
        themeId: themeId,
      );

      expect(find.byKey(const Key('docs-layout-medium')), findsOneWidget);
      expect(find.byKey(const Key('docs-catalog-pane')), findsOneWidget);
      expect(find.byKey(const Key('docs-reader-empty')), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('handoff route reuses reader surface and authoritative title', (
    tester,
  ) async {
    await _setDocsTestViewport(tester, const Size(1440, 1100));
    await _pumpDocsPage(
      tester,
      repository: _SuccessDocsRepository(),
      handoffTarget: const DocsDetailHandoffTarget(
        slug: 'flutter-docs-scope',
        source: DocsDetailHandoffSource.discover,
        initialTitle: '过期的入口标题',
      ),
    );

    expect(find.byKey(const Key('docs-reader-route-axis-904')), findsOneWidget);
    expect(find.byKey(const Key('docs-reader-surface')), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsWidgets);
    expect(find.text('过期的入口标题'), findsNothing);
  });

  testWidgets(
      'reader keeps old body visible through refresh stale and recovery', (
    tester,
  ) async {
    await _setDocsTestViewport(tester, const Size(390, 1000));
    final repository = _WidgetDetailRefreshDocsRepository();
    await _pumpDocsPage(tester, repository: repository);

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      160,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('authoritative version one'), findsOneWidget);

    await tester.tap(find.text('刷新详情'));
    await tester.pump();

    expect(find.text('正在刷新文档详情，当前仍展示上次可用正文。'), findsOneWidget);
    expect(find.text('authoritative version one'), findsOneWidget);

    repository.refreshCompleter!.completeError(
      const RadishApiClientException(
        '正文刷新失败',
        statusCode: 503,
        code: 'Docs.RefreshUnavailable',
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('刷新文档详情失败'), findsOneWidget);
    expect(find.text('authoritative version one'), findsOneWidget);

    await tester.tap(find.text('重试'));
    await tester.pumpAndSettle();

    expect(find.text('authoritative version two'), findsOneWidget);
    expect(find.text('刷新文档详情失败'), findsNothing);
  });

  testWidgets('reader renders an explicit empty body state', (tester) async {
    await _setDocsTestViewport(tester, const Size(390, 900));
    await _pumpDocsPage(tester, repository: _EmptyBodyDocsRepository());

    await tester.scrollUntilVisible(
      find.text('打开文档').first,
      160,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('打开文档').first);
    await tester.pumpAndSettle();

    expect(find.text('这篇文档暂无正文内容。'), findsOneWidget);
  });
}

Future<void> _setDocsTestViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Future<void> _pumpDocsPage(
  WidgetTester tester, {
  required DocsRepository repository,
  RadishThemeId themeId = RadishThemeId.guofeng,
  DocsDetailHandoffTarget? handoffTarget,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      theme: buildRadishTheme(themeId),
      home: DocsPage(
        environment: const AppEnvironment.development(),
        repository: repository,
        handoffTarget: handoffTarget,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

class _WidgetDetailRefreshDocsRepository extends _SuccessDocsRepository {
  var detailCalls = 0;
  Completer<DocsDocumentDetail>? refreshCompleter;

  @override
  Future<DocsDocumentDetail> getDocumentDetail({required String slug}) {
    detailCalls += 1;
    if (detailCalls == 1) {
      return Future.value(
        _controllerDetail(slug, 'authoritative version one'),
      );
    }
    if (detailCalls == 2) {
      refreshCompleter = Completer<DocsDocumentDetail>();
      return refreshCompleter!.future;
    }
    return Future.value(
      _controllerDetail(slug, 'authoritative version two'),
    );
  }
}

class _EmptyBodyDocsRepository extends _SuccessDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({required String slug}) async {
    return DocsDocumentDetail(
      id: 'empty-body',
      title: 'Empty body document',
      slug: slug,
      markdownContent: '',
    );
  }
}
