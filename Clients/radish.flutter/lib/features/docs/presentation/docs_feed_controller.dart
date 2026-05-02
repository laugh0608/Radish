import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';

enum DocsFeedStatus {
  loading,
  ready,
  error,
}

class DocsFeedState {
  const DocsFeedState({
    required this.status,
    required this.pageIndex,
    required this.pageSize,
    this.keyword = '',
    this.isRefreshing = false,
    this.page,
    this.errorMessage,
    this.refreshIssueMessage,
  });

  const DocsFeedState.initial()
      : this(
          status: DocsFeedStatus.loading,
          pageIndex: 1,
          pageSize: 20,
        );

  final DocsFeedStatus status;
  final int pageIndex;
  final int pageSize;
  final String keyword;
  final bool isRefreshing;
  final DocsDocumentPage? page;
  final String? errorMessage;
  final String? refreshIssueMessage;

  bool get isLoading => status == DocsFeedStatus.loading;

  bool get isReady => status == DocsFeedStatus.ready;

  bool get isError => status == DocsFeedStatus.error;

  bool get isBusy => isLoading || isRefreshing;

  bool get hasPreviousPage => pageIndex > 1;

  bool get hasNextPage => page != null && pageIndex < page!.pageCount;

  bool get hasKeyword => keyword.isNotEmpty;

  DocsFeedState copyWith({
    DocsFeedStatus? status,
    int? pageIndex,
    int? pageSize,
    String? keyword,
    bool? isRefreshing,
    DocsDocumentPage? page,
    bool clearPage = false,
    String? errorMessage,
    bool clearError = false,
    String? refreshIssueMessage,
    bool clearRefreshIssue = false,
  }) {
    return DocsFeedState(
      status: status ?? this.status,
      pageIndex: pageIndex ?? this.pageIndex,
      pageSize: pageSize ?? this.pageSize,
      keyword: keyword ?? this.keyword,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      page: clearPage ? null : (page ?? this.page),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      refreshIssueMessage: clearRefreshIssue
          ? null
          : (refreshIssueMessage ?? this.refreshIssueMessage),
    );
  }
}

class DocsFeedController extends ChangeNotifier {
  DocsFeedController({
    required DocsRepository repository,
  }) : _repository = repository;

  final DocsRepository _repository;
  DocsFeedState _state = const DocsFeedState.initial();
  int _requestVersion = 0;

  DocsFeedState get state => _state;

  Future<void> loadInitial() async {
    await _load(pageIndex: _state.pageIndex);
  }

  Future<void> refresh() async {
    await _load(
      pageIndex: _state.pageIndex,
      preserveCurrentPage: _state.page != null,
    );
  }

  Future<void> goToPage(int pageIndex) async {
    if (pageIndex < 1) {
      return;
    }

    if (pageIndex == _state.pageIndex && _state.page != null) {
      return;
    }

    await _load(pageIndex: pageIndex);
  }

  Future<void> search(String keyword) async {
    final normalizedKeyword = _normalizeKeyword(keyword);
    if (normalizedKeyword == _state.keyword && _state.page != null) {
      return;
    }

    await _load(
      pageIndex: 1,
      keyword: normalizedKeyword,
    );
  }

  Future<void> clearSearch() async {
    if (_state.keyword.isEmpty && _state.page != null) {
      return;
    }

    await _load(
      pageIndex: 1,
      keyword: '',
    );
  }

  Future<void> _load({
    required int pageIndex,
    String? keyword,
    bool preserveCurrentPage = false,
  }) async {
    final normalizedKeyword = keyword ?? _state.keyword;
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status:
          preserveCurrentPage ? DocsFeedStatus.ready : DocsFeedStatus.loading,
      pageIndex: pageIndex,
      keyword: normalizedKeyword,
      isRefreshing: preserveCurrentPage,
      clearError: true,
      clearRefreshIssue: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getDocumentPage(
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
        keyword: normalizedKeyword,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: DocsFeedStatus.ready,
        pageIndex: page.page,
        isRefreshing: false,
        page: page,
        clearError: true,
        clearRefreshIssue: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      if (preserveCurrentPage) {
        _setRefreshIssue(requestVersion, error.message);
      } else {
        _setError(requestVersion, pageIndex, error.message);
      }
    } on FormatException catch (error) {
      final message = '文档列表返回格式异常：${error.message}';
      if (preserveCurrentPage) {
        _setRefreshIssue(requestVersion, message);
      } else {
        _setError(requestVersion, pageIndex, message);
      }
    }
  }

  void _setError(int requestVersion, int pageIndex, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DocsFeedStatus.error,
      pageIndex: pageIndex,
      isRefreshing: false,
      errorMessage: message,
    );
    notifyListeners();
  }

  void _setRefreshIssue(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DocsFeedStatus.ready,
      isRefreshing: false,
      refreshIssueMessage: message,
    );
    notifyListeners();
  }
}

String _normalizeKeyword(String keyword) {
  return keyword.trim();
}
