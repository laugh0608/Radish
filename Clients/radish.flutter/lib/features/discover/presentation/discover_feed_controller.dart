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
    this.snapshot,
    this.errorMessage,
  });

  const DiscoverFeedState.initial()
      : this(
          status: DiscoverFeedStatus.loading,
          pageSize: 4,
        );

  final DiscoverFeedStatus status;
  final int pageSize;
  final DiscoverSnapshot? snapshot;
  final String? errorMessage;

  bool get isLoading => status == DiscoverFeedStatus.loading;

  bool get isReady => status == DiscoverFeedStatus.ready;

  bool get isError => status == DiscoverFeedStatus.error;

  DiscoverFeedState copyWith({
    DiscoverFeedStatus? status,
    int? pageSize,
    DiscoverSnapshot? snapshot,
    bool clearSnapshot = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return DiscoverFeedState(
      status: status ?? this.status,
      pageSize: pageSize ?? this.pageSize,
      snapshot: clearSnapshot ? null : (snapshot ?? this.snapshot),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
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
    _state = _state.copyWith(
      status: DiscoverFeedStatus.loading,
      clearError: true,
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
        snapshot: snapshot,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        'Unexpected discover payload: ${error.message}',
      );
    }
  }

  void _setError(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DiscoverFeedStatus.error,
      errorMessage: message,
    );
    notifyListeners();
  }
}
