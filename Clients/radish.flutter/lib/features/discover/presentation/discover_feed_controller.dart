import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/discover_models.dart';
import '../data/discover_repository.dart';

enum DiscoverFeedStatus { loading, ready, error }

enum DiscoverFeedIssueKind { unavailable, invalidResponse, request }

class DiscoverFeedIssue {
  const DiscoverFeedIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory DiscoverFeedIssue.fromApi(RadishApiClientException error) {
    final unavailable = error.code == 'PublicDiscover.SourceUnavailable' ||
        error.statusCode == 503 ||
        error.statusCode == null;
    return DiscoverFeedIssue(
      kind: unavailable
          ? DiscoverFeedIssueKind.unavailable
          : DiscoverFeedIssueKind.request,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory DiscoverFeedIssue.invalidResponse(FormatException error) {
    return DiscoverFeedIssue(
      kind: DiscoverFeedIssueKind.invalidResponse,
      message: '发现内容返回格式异常：${error.message}',
      code: 'PublicDiscover.InvalidResponse',
    );
  }

  final DiscoverFeedIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;

  bool get isUnavailable => kind == DiscoverFeedIssueKind.unavailable;
}

class DiscoverFeedState {
  const DiscoverFeedState({
    required this.status,
    required this.pageSize,
    this.isRefreshing = false,
    this.isLoadingMore = false,
    this.snapshot,
    this.error,
    this.refreshIssue,
    this.loadMoreIssue,
  });

  const DiscoverFeedState.initial()
      : this(status: DiscoverFeedStatus.loading, pageSize: 10);

  final DiscoverFeedStatus status;
  final int pageSize;
  final bool isRefreshing;
  final bool isLoadingMore;
  final DiscoverFeedSnapshot? snapshot;
  final DiscoverFeedIssue? error;
  final DiscoverFeedIssue? refreshIssue;
  final DiscoverFeedIssue? loadMoreIssue;

  bool get isLoading => status == DiscoverFeedStatus.loading;
  bool get isReady => status == DiscoverFeedStatus.ready;
  bool get isError => status == DiscoverFeedStatus.error;
  bool get isBusy => isLoading || isRefreshing || isLoadingMore;

  DiscoverFeedState copyWith({
    DiscoverFeedStatus? status,
    int? pageSize,
    bool? isRefreshing,
    bool? isLoadingMore,
    DiscoverFeedSnapshot? snapshot,
    bool clearSnapshot = false,
    DiscoverFeedIssue? error,
    bool clearError = false,
    DiscoverFeedIssue? refreshIssue,
    bool clearRefreshIssue = false,
    DiscoverFeedIssue? loadMoreIssue,
    bool clearLoadMoreIssue = false,
  }) {
    return DiscoverFeedState(
      status: status ?? this.status,
      pageSize: pageSize ?? this.pageSize,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      snapshot: clearSnapshot ? null : (snapshot ?? this.snapshot),
      error: clearError ? null : (error ?? this.error),
      refreshIssue:
          clearRefreshIssue ? null : (refreshIssue ?? this.refreshIssue),
      loadMoreIssue:
          clearLoadMoreIssue ? null : (loadMoreIssue ?? this.loadMoreIssue),
    );
  }
}

class DiscoverFeedController extends ChangeNotifier {
  DiscoverFeedController({required DiscoverRepository repository})
      : _repository = repository;

  final DiscoverRepository _repository;
  DiscoverFeedState _state = const DiscoverFeedState.initial();
  int _requestGeneration = 0;

  DiscoverFeedState get state => _state;

  Future<void> loadInitial() => refresh();

  Future<void> refresh() async {
    final requestGeneration = ++_requestGeneration;
    final hasSnapshot = _state.snapshot != null;
    _state = _state.copyWith(
      status:
          hasSnapshot ? DiscoverFeedStatus.ready : DiscoverFeedStatus.loading,
      isRefreshing: hasSnapshot,
      isLoadingMore: false,
      clearError: true,
      clearRefreshIssue: true,
      clearLoadMoreIssue: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getFeed(pageSize: _state.pageSize);
      if (requestGeneration != _requestGeneration) {
        return;
      }

      _state = _state.copyWith(
        status: DiscoverFeedStatus.ready,
        isRefreshing: false,
        snapshot: DiscoverFeedSnapshot.fromPage(page),
        clearError: true,
        clearRefreshIssue: true,
        clearLoadMoreIssue: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _handleRefreshIssue(
        requestGeneration,
        hasSnapshot,
        DiscoverFeedIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _handleRefreshIssue(
        requestGeneration,
        hasSnapshot,
        DiscoverFeedIssue.invalidResponse(error),
      );
    }
  }

  Future<void> loadMore() async {
    final snapshot = _state.snapshot;
    final cursor = snapshot?.nextCursor;
    if (!_state.isReady ||
        snapshot == null ||
        !snapshot.hasMore ||
        cursor == null ||
        _state.isRefreshing ||
        _state.isLoadingMore) {
      return;
    }

    final requestGeneration = _requestGeneration;
    _state = _state.copyWith(
      isLoadingMore: true,
      clearLoadMoreIssue: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getFeed(
        pageSize: _state.pageSize,
        cursor: cursor,
      );
      if (requestGeneration != _requestGeneration ||
          _state.snapshot?.nextCursor != cursor) {
        return;
      }

      _state = _state.copyWith(
        isLoadingMore: false,
        snapshot: snapshot.append(page),
        clearLoadMoreIssue: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _handleLoadMoreIssue(
        requestGeneration,
        cursor,
        DiscoverFeedIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _handleLoadMoreIssue(
        requestGeneration,
        cursor,
        DiscoverFeedIssue.invalidResponse(error),
      );
    }
  }

  void _handleRefreshIssue(
    int requestGeneration,
    bool hasSnapshot,
    DiscoverFeedIssue issue,
  ) {
    if (requestGeneration != _requestGeneration) {
      return;
    }

    _state = _state.copyWith(
      status: hasSnapshot ? DiscoverFeedStatus.ready : DiscoverFeedStatus.error,
      isRefreshing: false,
      error: hasSnapshot ? null : issue,
      clearError: hasSnapshot,
      refreshIssue: hasSnapshot ? issue : null,
      clearRefreshIssue: !hasSnapshot,
    );
    notifyListeners();
  }

  void _handleLoadMoreIssue(
    int requestGeneration,
    String cursor,
    DiscoverFeedIssue issue,
  ) {
    if (requestGeneration != _requestGeneration ||
        _state.snapshot?.nextCursor != cursor) {
      return;
    }

    _state = _state.copyWith(
      isLoadingMore: false,
      loadMoreIssue: issue,
    );
    notifyListeners();
  }

  @override
  void dispose() {
    _requestGeneration += 1;
    super.dispose();
  }
}
