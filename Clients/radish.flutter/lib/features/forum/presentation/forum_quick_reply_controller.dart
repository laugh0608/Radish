import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';

enum ForumQuickReplyStatus {
  idle,
  loading,
  ready,
  error,
}

class ForumQuickReplyState {
  const ForumQuickReplyState({
    required this.status,
    required this.items,
    required this.total,
    this.postId,
    this.errorMessage,
    this.isSubmitting = false,
    this.submitErrorMessage,
    this.submitSuccessMessage,
  });

  const ForumQuickReplyState.initial()
      : this(
          status: ForumQuickReplyStatus.idle,
          items: const <ForumQuickReplySummary>[],
          total: 0,
        );

  final ForumQuickReplyStatus status;
  final String? postId;
  final List<ForumQuickReplySummary> items;
  final int total;
  final String? errorMessage;
  final bool isSubmitting;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;

  bool get isIdle => status == ForumQuickReplyStatus.idle;

  bool get isLoading => status == ForumQuickReplyStatus.loading;

  bool get isReady => status == ForumQuickReplyStatus.ready;

  bool get isError => status == ForumQuickReplyStatus.error;

  ForumQuickReplyState copyWith({
    ForumQuickReplyStatus? status,
    String? postId,
    bool clearPostId = false,
    List<ForumQuickReplySummary>? items,
    int? total,
    String? errorMessage,
    bool clearError = false,
    bool? isSubmitting,
    String? submitErrorMessage,
    bool clearSubmitError = false,
    String? submitSuccessMessage,
    bool clearSubmitSuccess = false,
  }) {
    return ForumQuickReplyState(
      status: status ?? this.status,
      postId: clearPostId ? null : (postId ?? this.postId),
      items: items ?? this.items,
      total: total ?? this.total,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      isSubmitting: isSubmitting ?? this.isSubmitting,
      submitErrorMessage: clearSubmitError
          ? null
          : (submitErrorMessage ?? this.submitErrorMessage),
      submitSuccessMessage: clearSubmitSuccess
          ? null
          : (submitSuccessMessage ?? this.submitSuccessMessage),
    );
  }
}

class ForumQuickReplyController extends ChangeNotifier {
  ForumQuickReplyController({
    required ForumRepository repository,
  }) : _repository = repository;

  final ForumRepository _repository;
  ForumQuickReplyState _state = const ForumQuickReplyState.initial();
  int _requestVersion = 0;

  ForumQuickReplyState get state => _state;

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

  Future<bool> submit({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    final normalizedPostId = postId.trim();
    final normalizedContent = content.trim();
    final normalizedAccessToken = accessToken.trim();
    if (normalizedPostId.isEmpty ||
        normalizedContent.isEmpty ||
        normalizedAccessToken.isEmpty ||
        _state.isSubmitting) {
      return false;
    }

    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      isSubmitting: true,
      clearSubmitError: true,
      clearSubmitSuccess: true,
    );
    notifyListeners();

    try {
      final quickReply = await _repository.createQuickReply(
        postId: normalizedPostId,
        content: normalizedContent,
        accessToken: normalizedAccessToken,
      );

      if (requestVersion != _requestVersion) {
        return false;
      }

      final existingIds = _state.items.map((item) => item.id).toSet();
      final nextItems = existingIds.contains(quickReply.id)
          ? _state.items
          : <ForumQuickReplySummary>[
              quickReply,
              ..._state.items,
            ];

      _state = _state.copyWith(
        status: ForumQuickReplyStatus.ready,
        postId: normalizedPostId,
        items: nextItems,
        total: _state.total + (existingIds.contains(quickReply.id) ? 0 : 1),
        isSubmitting: false,
        clearError: true,
        clearSubmitError: true,
        submitSuccessMessage: existingIds.contains(quickReply.id)
            ? '轻回应已发布。'
            : '轻回应已发布，已显示在轻回应墙顶部。',
      );
      notifyListeners();
      return true;
    } on RadishApiClientException catch (error) {
      _setSubmitError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setSubmitError(
        requestVersion,
        '轻回应返回格式异常：${error.message}',
      );
    }

    return false;
  }

  Future<void> _load(String postId) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumQuickReplyStatus.loading,
      postId: postId,
      clearError: true,
      clearSubmitError: true,
      clearSubmitSuccess: true,
    );
    notifyListeners();

    try {
      final wall = await _repository.getQuickReplyWall(postId: postId);

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumQuickReplyStatus.ready,
        postId: postId,
        items: wall.items,
        total: wall.total,
        clearError: true,
        clearSubmitSuccess: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setLoadError(requestVersion, postId, error.message);
    } on FormatException catch (error) {
      _setLoadError(
        requestVersion,
        postId,
        '轻回应返回格式异常：${error.message}',
      );
    }
  }

  void _setLoadError(int requestVersion, String postId, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumQuickReplyStatus.error,
      postId: postId,
      errorMessage: message,
      isSubmitting: false,
    );
    notifyListeners();
  }

  void _setSubmitError(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      isSubmitting: false,
      submitErrorMessage: message,
    );
    notifyListeners();
  }
}
