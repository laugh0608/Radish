import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';

enum ForumFeedStatus {
  loading,
  ready,
  error,
}

class ForumFeedState {
  const ForumFeedState({
    required this.status,
    required this.sort,
    required this.pageIndex,
    required this.pageSize,
    this.isRefreshing = false,
    this.page,
    this.errorMessage,
    this.refreshIssueMessage,
  });

  const ForumFeedState.initial()
      : this(
          status: ForumFeedStatus.loading,
          sort: ForumFeedSort.newest,
          pageIndex: 1,
          pageSize: 20,
        );

  final ForumFeedStatus status;
  final ForumFeedSort sort;
  final int pageIndex;
  final int pageSize;
  final bool isRefreshing;
  final ForumPostPage? page;
  final String? errorMessage;
  final String? refreshIssueMessage;

  bool get isLoading => status == ForumFeedStatus.loading;

  bool get isReady => status == ForumFeedStatus.ready;

  bool get isError => status == ForumFeedStatus.error;

  bool get isBusy => isLoading || isRefreshing;

  bool get hasPreviousPage => pageIndex > 1;

  bool get hasNextPage => page != null && pageIndex < page!.pageCount;

  ForumFeedState copyWith({
    ForumFeedStatus? status,
    ForumFeedSort? sort,
    int? pageIndex,
    int? pageSize,
    bool? isRefreshing,
    ForumPostPage? page,
    bool clearPage = false,
    String? errorMessage,
    bool clearError = false,
    String? refreshIssueMessage,
    bool clearRefreshIssue = false,
  }) {
    return ForumFeedState(
      status: status ?? this.status,
      sort: sort ?? this.sort,
      pageIndex: pageIndex ?? this.pageIndex,
      pageSize: pageSize ?? this.pageSize,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      page: clearPage ? null : (page ?? this.page),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      refreshIssueMessage: clearRefreshIssue
          ? null
          : (refreshIssueMessage ?? this.refreshIssueMessage),
    );
  }
}

class ForumFeedController extends ChangeNotifier {
  ForumFeedController({
    required ForumRepository repository,
  }) : _repository = repository;

  final ForumRepository _repository;

  ForumFeedState _state = const ForumFeedState.initial();
  int _requestVersion = 0;

  ForumFeedState get state => _state;

  Future<void> loadInitial() async {
    await _load(
      pageIndex: _state.pageIndex,
      sort: _state.sort,
    );
  }

  Future<void> refresh() async {
    await _load(
      pageIndex: _state.pageIndex,
      sort: _state.sort,
      preserveCurrentPage: _state.page != null,
    );
  }

  Future<void> changeSort(ForumFeedSort sort) async {
    if (sort == _state.sort && _state.page != null) {
      return;
    }

    await _load(
      pageIndex: 1,
      sort: sort,
    );
  }

  Future<void> goToPage(int pageIndex) async {
    if (pageIndex < 1) {
      return;
    }

    if (pageIndex == _state.pageIndex && _state.page != null) {
      return;
    }

    await _load(
      pageIndex: pageIndex,
      sort: _state.sort,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required ForumFeedSort sort,
    bool preserveCurrentPage = false,
  }) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status:
          preserveCurrentPage ? ForumFeedStatus.ready : ForumFeedStatus.loading,
      sort: sort,
      pageIndex: pageIndex,
      isRefreshing: preserveCurrentPage,
      clearError: true,
      clearRefreshIssue: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getPostPage(
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
        sort: sort,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumFeedStatus.ready,
        sort: sort,
        pageIndex: page.page,
        isRefreshing: false,
        page: page,
        clearError: true,
        clearRefreshIssue: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      if (requestVersion != _requestVersion) {
        return;
      }

      if (preserveCurrentPage) {
        _setRefreshIssue(requestVersion, error.message);
        return;
      }

      _state = _state.copyWith(
        status: ForumFeedStatus.error,
        sort: sort,
        pageIndex: pageIndex,
        isRefreshing: false,
        errorMessage: error.message,
      );
      notifyListeners();
    } on FormatException catch (error) {
      if (requestVersion != _requestVersion) {
        return;
      }

      final message = '论坛列表返回格式异常：${error.message}';
      if (preserveCurrentPage) {
        _setRefreshIssue(requestVersion, message);
        return;
      }

      _state = _state.copyWith(
        status: ForumFeedStatus.error,
        sort: sort,
        pageIndex: pageIndex,
        isRefreshing: false,
        errorMessage: message,
      );
      notifyListeners();
    }
  }

  void _setRefreshIssue(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumFeedStatus.ready,
      isRefreshing: false,
      refreshIssueMessage: message,
    );
    notifyListeners();
  }
}
