import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_issue.dart';

enum DocsFeedStatus { loading, ready, unavailable }

class DocsFeedQuery {
  const DocsFeedQuery({
    required this.pageIndex,
    required this.pageSize,
    required this.keyword,
  });

  final int pageIndex;
  final int pageSize;
  final String keyword;

  bool matches(DocsFeedQuery other) {
    return pageIndex == other.pageIndex &&
        pageSize == other.pageSize &&
        keyword == other.keyword;
  }
}

class DocsFeedState {
  const DocsFeedState({
    required this.status,
    required this.query,
    this.isRefreshing = false,
    this.page,
    this.issue,
  });

  const DocsFeedState.initial()
      : this(
          status: DocsFeedStatus.loading,
          query: const DocsFeedQuery(
            pageIndex: 1,
            pageSize: 20,
            keyword: '',
          ),
        );

  final DocsFeedStatus status;
  final DocsFeedQuery query;
  final bool isRefreshing;
  final DocsDocumentPage? page;
  final DocsIssue? issue;

  int get pageIndex => query.pageIndex;
  int get pageSize => query.pageSize;
  String get keyword => query.keyword;
  String? get errorMessage => issue?.message;
  String? get refreshIssueMessage => isStale ? issue?.message : null;
  bool get isLoading => status == DocsFeedStatus.loading;
  bool get isReady => status == DocsFeedStatus.ready;
  bool get isUnavailable => status == DocsFeedStatus.unavailable;
  bool get isError => isUnavailable;
  bool get isStale => page != null && issue != null && !isRefreshing;
  bool get isBusy => isLoading || isRefreshing;
  bool get hasPreviousPage => pageIndex > 1;
  bool get hasNextPage => page != null && pageIndex < page!.pageCount;
  bool get hasKeyword => keyword.isNotEmpty;
}

class DocsFeedController extends ChangeNotifier {
  DocsFeedController({required DocsRepository repository})
      : _repository = repository;

  final DocsRepository _repository;
  DocsFeedState _state = const DocsFeedState.initial();
  int _requestVersion = 0;
  bool _isDisposed = false;

  DocsFeedState get state => _state;

  Future<void> loadInitial() => _load(_state.query);

  Future<void> refresh() {
    return _load(
      _state.query,
      preserveCurrentPage: _state.page != null,
    );
  }

  Future<void> goToPage(int pageIndex) {
    if (pageIndex < 1 ||
        (pageIndex == _state.pageIndex && _state.page != null)) {
      return Future<void>.value();
    }
    return _load(
      DocsFeedQuery(
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
        keyword: _state.keyword,
      ),
    );
  }

  Future<void> search(String keyword) {
    final normalizedKeyword = keyword.trim();
    if (normalizedKeyword == _state.keyword && _state.page != null) {
      return Future<void>.value();
    }
    return _load(
      DocsFeedQuery(
        pageIndex: 1,
        pageSize: _state.pageSize,
        keyword: normalizedKeyword,
      ),
    );
  }

  Future<void> clearSearch() => search('');

  Future<void> _load(
    DocsFeedQuery query, {
    bool preserveCurrentPage = false,
  }) async {
    final requestVersion = ++_requestVersion;
    _state = DocsFeedState(
      status:
          preserveCurrentPage ? DocsFeedStatus.ready : DocsFeedStatus.loading,
      query: query,
      isRefreshing: preserveCurrentPage,
      page: preserveCurrentPage ? _state.page : null,
    );
    _notify();

    try {
      final page = await _repository.getDocumentPage(
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword,
      );
      if (!_canCommit(requestVersion, query)) {
        return;
      }
      _state = DocsFeedState(
        status: DocsFeedStatus.ready,
        query: DocsFeedQuery(
          pageIndex: page.page,
          pageSize: query.pageSize,
          keyword: query.keyword,
        ),
        page: page,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        requestVersion,
        query,
        DocsIssue.fromApi(error),
        preserveCurrentPage: preserveCurrentPage,
      );
    } on FormatException catch (error) {
      _commitIssue(
        requestVersion,
        query,
        DocsIssue.invalidResponse(error, resourceLabel: '文档列表'),
        preserveCurrentPage: preserveCurrentPage,
      );
    }
  }

  void _commitIssue(
    int requestVersion,
    DocsFeedQuery query,
    DocsIssue issue, {
    required bool preserveCurrentPage,
  }) {
    if (!_canCommit(requestVersion, query)) {
      return;
    }
    _state = DocsFeedState(
      status: preserveCurrentPage
          ? DocsFeedStatus.ready
          : DocsFeedStatus.unavailable,
      query: query,
      page: preserveCurrentPage ? _state.page : null,
      issue: issue,
    );
    _notify();
  }

  bool _canCommit(int requestVersion, DocsFeedQuery query) {
    return !_isDisposed &&
        requestVersion == _requestVersion &&
        _state.query.matches(query);
  }

  void _notify() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    _requestVersion++;
    super.dispose();
  }
}
