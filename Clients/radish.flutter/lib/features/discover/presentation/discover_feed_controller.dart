import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/discover_models.dart';
import '../data/discover_repository.dart';

enum DiscoverFeedStatus {
  loading,
  ready,
  error,
}

class DiscoverFeedState {
  const DiscoverFeedState({
    required this.status,
    required this.pageSize,
    this.isRefreshing = false,
    this.snapshot,
    this.errorMessage,
    this.refreshIssueMessage,
  });

  const DiscoverFeedState.initial()
      : this(
          status: DiscoverFeedStatus.loading,
          pageSize: 4,
        );

  final DiscoverFeedStatus status;
  final int pageSize;
  final bool isRefreshing;
  final DiscoverSnapshot? snapshot;
  final String? errorMessage;
  final String? refreshIssueMessage;

  bool get isLoading => status == DiscoverFeedStatus.loading;

  bool get isReady => status == DiscoverFeedStatus.ready;

  bool get isError => status == DiscoverFeedStatus.error;

  bool get isBusy => isLoading || isRefreshing;

  DiscoverFeedState copyWith({
    DiscoverFeedStatus? status,
    int? pageSize,
    bool? isRefreshing,
    DiscoverSnapshot? snapshot,
    bool clearSnapshot = false,
    String? errorMessage,
    bool clearError = false,
    String? refreshIssueMessage,
    bool clearRefreshIssue = false,
  }) {
    return DiscoverFeedState(
      status: status ?? this.status,
      pageSize: pageSize ?? this.pageSize,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      snapshot: clearSnapshot ? null : (snapshot ?? this.snapshot),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      refreshIssueMessage: clearRefreshIssue
          ? null
          : (refreshIssueMessage ?? this.refreshIssueMessage),
    );
  }
}

class DiscoverFeedController extends ChangeNotifier {
  DiscoverFeedController({
    required DiscoverRepository repository,
  }) : _repository = repository;

  final DiscoverRepository _repository;
  DiscoverFeedState _state = const DiscoverFeedState.initial();
  int _requestVersion = 0;

  DiscoverFeedState get state => _state;

  Future<void> loadInitial() async {
    await refresh();
  }

  Future<void> refresh() async {
    final requestVersion = ++_requestVersion;
    final hasSnapshot = _state.snapshot != null;
    _state = _state.copyWith(
      status:
          hasSnapshot ? DiscoverFeedStatus.ready : DiscoverFeedStatus.loading,
      isRefreshing: hasSnapshot,
      clearError: true,
      clearRefreshIssue: true,
    );
    notifyListeners();

    try {
      final snapshot = await _repository.getSnapshot(
        pageSize: _state.pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: DiscoverFeedStatus.ready,
        isRefreshing: false,
        snapshot: snapshot,
        clearError: true,
        clearRefreshIssue: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      if (hasSnapshot) {
        _setRefreshIssue(requestVersion, error.message);
      } else {
        _setError(requestVersion, error.message);
      }
    } on FormatException catch (error) {
      final message = '发现内容返回格式异常：${error.message}';
      if (hasSnapshot) {
        _setRefreshIssue(requestVersion, message);
      } else {
        _setError(requestVersion, message);
      }
    }
  }

  void _setError(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DiscoverFeedStatus.error,
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
      status: DiscoverFeedStatus.ready,
      isRefreshing: false,
      refreshIssueMessage: message,
    );
    notifyListeners();
  }
}
