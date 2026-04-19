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
    this.page,
    this.errorMessage,
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
  final ForumPostPage? page;
  final String? errorMessage;

  bool get isLoading => status == ForumFeedStatus.loading;

  bool get isReady => status == ForumFeedStatus.ready;

  bool get isError => status == ForumFeedStatus.error;

  bool get hasPreviousPage => pageIndex > 1;

  bool get hasNextPage => page != null && pageIndex < page!.pageCount;

  ForumFeedState copyWith({
    ForumFeedStatus? status,
    ForumFeedSort? sort,
    int? pageIndex,
    int? pageSize,
    ForumPostPage? page,
    bool clearPage = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ForumFeedState(
      status: status ?? this.status,
      sort: sort ?? this.sort,
      pageIndex: pageIndex ?? this.pageIndex,
      pageSize: pageSize ?? this.pageSize,
      page: clearPage ? null : (page ?? this.page),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
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
  }) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumFeedStatus.loading,
      sort: sort,
      pageIndex: pageIndex,
      clearError: true,
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
        page: page,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumFeedStatus.error,
        sort: sort,
        pageIndex: pageIndex,
        errorMessage: error.message,
      );
      notifyListeners();
    } on FormatException catch (error) {
      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumFeedStatus.error,
        sort: sort,
        pageIndex: pageIndex,
        errorMessage: 'Unexpected forum payload: ${error.message}',
      );
      notifyListeners();
    }
  }
}
