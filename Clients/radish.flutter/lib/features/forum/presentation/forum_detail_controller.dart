import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';

enum ForumDetailStatus {
  idle,
  loading,
  ready,
  error,
}

class ForumDetailState {
  const ForumDetailState({
    required this.status,
    this.postId,
    this.detail,
    this.errorMessage,
  });

  const ForumDetailState.idle()
      : this(
          status: ForumDetailStatus.idle,
        );

  final ForumDetailStatus status;
  final String? postId;
  final ForumPostDetail? detail;
  final String? errorMessage;

  bool get isIdle => status == ForumDetailStatus.idle;

  bool get isLoading => status == ForumDetailStatus.loading;

  bool get isReady => status == ForumDetailStatus.ready;

  bool get isError => status == ForumDetailStatus.error;

  ForumDetailState copyWith({
    ForumDetailStatus? status,
    String? postId,
    bool clearPostId = false,
    ForumPostDetail? detail,
    bool clearDetail = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ForumDetailState(
      status: status ?? this.status,
      postId: clearPostId ? null : (postId ?? this.postId),
      detail: clearDetail ? null : (detail ?? this.detail),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class ForumDetailController extends ChangeNotifier {
  ForumDetailController({
    required ForumRepository repository,
  }) : _repository = repository;

  final ForumRepository _repository;
  ForumDetailState _state = const ForumDetailState.idle();
  int _requestVersion = 0;

  ForumDetailState get state => _state;

  Future<void> openPost(String postId) async {
    final normalizedPostId = postId.trim();
    if (normalizedPostId.isEmpty) {
      return;
    }

    if (_state.postId == normalizedPostId &&
        (_state.isLoading || _state.isReady)) {
      return;
    }

    await _load(normalizedPostId);
  }

  Future<void> refresh() async {
    final postId = _state.postId;
    if (postId == null || postId.isEmpty) {
      return;
    }

    await _load(postId);
  }

  Future<void> _load(String postId) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumDetailStatus.loading,
      postId: postId,
      clearError: true,
    );
    notifyListeners();

    try {
      final detail = await _repository.getPostDetail(postId: postId);

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumDetailStatus.ready,
        postId: postId,
        detail: detail,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, postId, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        postId,
        'Unexpected forum detail payload: ${error.message}',
      );
    }
  }

  void _setError(int requestVersion, String postId, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumDetailStatus.error,
      postId: postId,
      errorMessage: message,
    );
    notifyListeners();
  }
}
