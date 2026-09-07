part of 'docs_page_test.dart';

void registerDocsControllerTests() {
  group('Docs controllers', () {
    test('feed rejects a late response from the previous query target',
        () async {
      final repository = _QueryRaceDocsRepository();
      final controller = DocsFeedController(repository: repository);
      addTearDown(controller.dispose);

      final first = controller.search('first');
      final second = controller.search('second');

      repository.completeQuery('first', _controllerPage('first'));
      await first;

      expect(controller.state.keyword, 'second');
      expect(controller.state.page, isNull);
      expect(controller.state.isLoading, isTrue);

      repository.completeQuery('second', _controllerPage('second'));
      await second;

      expect(controller.state.keyword, 'second');
      expect(controller.state.page!.documents.single.title, 'second');
    });

    test('feed keeps the authoritative page and structured refresh issue',
        () async {
      final controller = DocsFeedController(
        repository: _StructuredRefreshIssueDocsRepository(),
      );
      addTearDown(controller.dispose);

      await controller.loadInitial();
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.page!.documents.single.title, 'initial');
      expect(controller.state.issue!.kind, DocsIssueKind.unavailable);
      expect(controller.state.issue!.code, 'Docs.RefreshUnavailable');
      expect(controller.state.issue!.statusCode, 503);
    });

    test('new feed query clears the previous query snapshot', () async {
      final repository = _QueryRaceDocsRepository();
      final controller = DocsFeedController(repository: repository);
      addTearDown(controller.dispose);

      final initial = controller.search('initial');
      repository.completeQuery('initial', _controllerPage('initial'));
      await initial;

      final next = controller.search('next');
      expect(controller.state.page, isNull);
      expect(controller.state.keyword, 'next');

      repository.completeQuery('next', _controllerPage('next'));
      await next;
    });

    test('reader keeps old body while same slug refresh becomes stale',
        () async {
      final repository = _DetailSequenceDocsRepository();
      final controller = DocsDetailController(repository: repository);
      addTearDown(controller.dispose);

      final initial = controller.openDocument('alpha');
      repository.completeNext(_controllerDetail('alpha', 'version one'));
      await initial;

      final refresh = controller.refresh();
      expect(controller.state.isRefreshing, isTrue);
      expect(controller.state.detail!.markdownContent, 'version one');

      repository.failNext(
        const RadishApiClientException(
          '正文刷新服务暂时不可用',
          statusCode: 503,
          code: 'Docs.DetailRefreshUnavailable',
        ),
      );
      await refresh;

      expect(controller.state.isStale, isTrue);
      expect(controller.state.detail!.markdownContent, 'version one');
      expect(controller.state.issue!.code, 'Docs.DetailRefreshUnavailable');

      final recover = controller.refresh();
      repository.completeNext(_controllerDetail('alpha', 'version two'));
      await recover;

      expect(controller.state.isReady, isTrue);
      expect(controller.state.detail!.markdownContent, 'version two');
      expect(controller.state.issue, isNull);
    });

    test('reader clears old body and rejects a late cross-slug response',
        () async {
      final repository = _SlugRaceDocsRepository();
      final controller = DocsDetailController(repository: repository);
      addTearDown(controller.dispose);

      final alpha = controller.openDocument('alpha');
      final beta = controller.openDocument('beta');

      expect(controller.state.slug, 'beta');
      expect(controller.state.detail, isNull);

      repository.completeSlug('alpha', _controllerDetail('alpha', 'old'));
      await alpha;
      expect(controller.state.slug, 'beta');
      expect(controller.state.detail, isNull);

      repository.completeSlug('beta', _controllerDetail('beta', 'new'));
      await beta;
      expect(controller.state.detail!.slug, 'beta');
      expect(controller.state.detail!.markdownContent, 'new');
    });

    test('reader clears a ready body immediately when slug changes', () async {
      final repository = _SlugRaceDocsRepository();
      final controller = DocsDetailController(repository: repository);
      addTearDown(controller.dispose);

      final alpha = controller.openDocument('alpha');
      repository.completeSlug(
          'alpha', _controllerDetail('alpha', 'alpha body'));
      await alpha;
      expect(controller.state.detail!.slug, 'alpha');

      final beta = controller.openDocument('beta');
      expect(controller.state.slug, 'beta');
      expect(controller.state.detail, isNull);
      expect(controller.state.isLoading, isTrue);

      repository.completeSlug('beta', _controllerDetail('beta', 'beta body'));
      await beta;
    });

    test('reader keeps target and structured issue on initial unavailable',
        () async {
      final controller = DocsDetailController(
        repository: _FailThenRecoverDetailDocsRepository(),
      );
      addTearDown(controller.dispose);

      await controller.openDocument('recoverable');
      expect(controller.state.isUnavailable, isTrue);
      expect(controller.state.slug, 'recoverable');
      expect(controller.state.issue!.kind, DocsIssueKind.unavailable);
      expect(controller.state.issue!.code, 'Docs.DetailUnavailable');

      await controller.refresh();
      expect(controller.state.isReady, isTrue);
      expect(controller.state.detail!.slug, 'recoverable');
    });

    test('invalid response remains distinct from request failures', () async {
      final controller = DocsFeedController(
        repository: _InvalidResponseDocsRepository(),
      );
      addTearDown(controller.dispose);

      await controller.loadInitial();

      expect(controller.state.isUnavailable, isTrue);
      expect(controller.state.issue!.kind, DocsIssueKind.invalidResponse);
      expect(controller.state.issue!.code, 'Docs.InvalidResponse');
    });

    test('close and dispose invalidate pending reader responses', () async {
      final closeRepository = _SlugRaceDocsRepository();
      final closeController = DocsDetailController(repository: closeRepository);
      final pendingClose = closeController.openDocument('close-target');
      closeController.close();
      closeRepository.completeSlug(
        'close-target',
        _controllerDetail('close-target', 'late close body'),
      );
      await pendingClose;
      expect(closeController.state.isIdle, isTrue);
      closeController.dispose();

      final disposeRepository = _SlugRaceDocsRepository();
      final disposeController = DocsDetailController(
        repository: disposeRepository,
      );
      var notifications = 0;
      disposeController.addListener(() => notifications += 1);
      final pendingDispose = disposeController.openDocument('dispose-target');
      expect(notifications, 1);
      disposeController.dispose();
      disposeRepository.completeSlug(
        'dispose-target',
        _controllerDetail('dispose-target', 'late disposed body'),
      );
      await pendingDispose;
      expect(notifications, 1);
    });
  });
}

DocsDocumentPage _controllerPage(String title) {
  return DocsDocumentPage(
    page: 1,
    pageSize: 20,
    dataCount: 1,
    pageCount: 1,
    documents: [
      DocsDocumentSummary(
        id: 'id-$title',
        title: title,
        slug: 'slug-$title',
      ),
    ],
  );
}

DocsDocumentDetail _controllerDetail(String slug, String body) {
  return DocsDocumentDetail(
    id: 'id-$slug',
    title: 'title-$slug',
    slug: slug,
    markdownContent: body,
  );
}

class _QueryRaceDocsRepository extends _SuccessDocsRepository {
  final Map<String, Completer<DocsDocumentPage>> _queries = {};

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    final key = keyword ?? '';
    return (_queries[key] ??= Completer<DocsDocumentPage>()).future;
  }

  void completeQuery(String keyword, DocsDocumentPage page) {
    _queries[keyword]!.complete(page);
  }
}

class _StructuredRefreshIssueDocsRepository extends _SuccessDocsRepository {
  var _calls = 0;

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    _calls += 1;
    if (_calls == 1) {
      return _controllerPage('initial');
    }
    throw const RadishApiClientException(
      '文档刷新服务暂时不可用',
      statusCode: 503,
      code: 'Docs.RefreshUnavailable',
    );
  }
}

class _DetailSequenceDocsRepository extends _SuccessDocsRepository {
  final List<Completer<DocsDocumentDetail>> _pending = [];

  @override
  Future<DocsDocumentDetail> getDocumentDetail({required String slug}) {
    final completer = Completer<DocsDocumentDetail>();
    _pending.add(completer);
    return completer.future;
  }

  void completeNext(DocsDocumentDetail detail) {
    _pending.removeAt(0).complete(detail);
  }

  void failNext(Object error) {
    _pending.removeAt(0).completeError(error);
  }
}

class _SlugRaceDocsRepository extends _SuccessDocsRepository {
  final Map<String, Completer<DocsDocumentDetail>> _details = {};

  @override
  Future<DocsDocumentDetail> getDocumentDetail({required String slug}) {
    return (_details[slug] ??= Completer<DocsDocumentDetail>()).future;
  }

  void completeSlug(String slug, DocsDocumentDetail detail) {
    _details[slug]!.complete(detail);
  }
}

class _FailThenRecoverDetailDocsRepository extends _SuccessDocsRepository {
  var _calls = 0;

  @override
  Future<DocsDocumentDetail> getDocumentDetail({required String slug}) async {
    _calls += 1;
    if (_calls == 1) {
      throw const RadishApiClientException(
        '文档详情暂时不可用',
        statusCode: 503,
        code: 'Docs.DetailUnavailable',
      );
    }
    return _controllerDetail(slug, 'recovered body');
  }
}

class _InvalidResponseDocsRepository extends _SuccessDocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) {
    throw const FormatException('missing data envelope');
  }
}
