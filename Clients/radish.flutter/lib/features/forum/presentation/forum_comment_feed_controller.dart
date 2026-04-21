import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';

enum ForumCommentFeedStatus {
  idle,
  loading,
  ready,
  error,
}

class ForumCommentFeedState {
  const ForumCommentFeedState({
    required this.status,
    required this.pageSize,
    required this.pageIndex,
    required this.comments,
    required this.totalCount,
    this.postId,
    this.pageCount = 0,
    this.errorMessage,
    this.isLoadingMore = false,
    this.loadMoreErrorMessage,
  });

  const ForumCommentFeedState.initial()
      : this(
          status: ForumCommentFeedStatus.idle,
          pageSize: 20,
          pageIndex: 0,
          comments: const <ForumCommentSummary>[],
          totalCount: 0,
        );

  final ForumCommentFeedStatus status;
  final int pageSize;
  final int pageIndex;
  final List<ForumCommentSummary> comments;
  final int totalCount;
  final String? postId;
  final int pageCount;
  final String? errorMessage;
  final bool isLoadingMore;
  final String? loadMoreErrorMessage;

  bool get isIdle => status == ForumCommentFeedStatus.idle;

  bool get isLoading => status == ForumCommentFeedStatus.loading;

  bool get isReady => status == ForumCommentFeedStatus.ready;

  bool get isError => status == ForumCommentFeedStatus.error;

  bool get isEmpty => isReady && comments.isEmpty;

  bool get hasMore {
    if (!isReady || pageCount <= 0) {
      return false;
    }

    return pageIndex < pageCount;
  }

  ForumCommentFeedState copyWith({
    ForumCommentFeedStatus? status,
    int? pageSize,
    int? pageIndex,
    List<ForumCommentSummary>? comments,
    int? totalCount,
    String? postId,
    bool clearPostId = false,
    int? pageCount,
    String? errorMessage,
    bool clearError = false,
    bool? isLoadingMore,
    String? loadMoreErrorMessage,
    bool clearLoadMoreError = false,
  }) {
    return ForumCommentFeedState(
      status: status ?? this.status,
      pageSize: pageSize ?? this.pageSize,
      pageIndex: pageIndex ?? this.pageIndex,
      comments: comments ?? this.comments,
      totalCount: totalCount ?? this.totalCount,
      postId: clearPostId ? null : (postId ?? this.postId),
      pageCount: pageCount ?? this.pageCount,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      loadMoreErrorMessage: clearLoadMoreError
          ? null
          : (loadMoreErrorMessage ?? this.loadMoreErrorMessage),
    );
  }
}

class ForumCommentFeedController extends ChangeNotifier {
  ForumCommentFeedController({
    required ForumRepository repository,
  }) : _repository = repository;

  final ForumRepository _repository;
  ForumCommentFeedState _state = const ForumCommentFeedState.initial();
  int _requestVersion = 0;

  ForumCommentFeedState get state => _state;

  Future<void> openPost(String postId) async {
    final normalizedPostId = postId.trim();
    if (normalizedPostId.isEmpty) {
      return;
    }

    if (_state.postId == normalizedPostId &&
        (_state.isLoading || _state.isReady)) {
      return;
    }

    await _loadInitial(normalizedPostId);
  }

  Future<void> refresh() async {
    final postId = _state.postId;
    if (postId == null || postId.isEmpty) {
      return;
    }

    await _loadInitial(postId);
  }

  Future<void> loadPage(int pageIndex) async {
    final postId = _state.postId;
    if (postId == null || postId.isEmpty || pageIndex < 1) {
      return;
    }

    await _loadInitial(
      postId,
      pageIndex: pageIndex,
    );
  }

  Future<void> loadMore() async {
    final postId = _state.postId;
    if (postId == null ||
        postId.isEmpty ||
        !_state.isReady ||
        _state.isLoadingMore ||
        !_state.hasMore) {
      return;
    }

    final requestVersion = ++_requestVersion;
    final nextPage = _state.pageIndex + 1;
    _state = _state.copyWith(
      isLoadingMore: true,
      clearLoadMoreError: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getRootCommentsPage(
        postId: postId,
        pageIndex: nextPage,
        pageSize: _state.pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      final existingIds = _state.comments.map((item) => item.id).toSet();
      final appended = page.comments
          .where((item) => !existingIds.contains(item.id))
          .toList(growable: false);

      _state = _state.copyWith(
        status: ForumCommentFeedStatus.ready,
        pageIndex: page.page,
        pageCount: page.pageCount,
        totalCount: page.dataCount,
        comments: <ForumCommentSummary>[
          ..._state.comments,
          ...appended,
        ],
        isLoadingMore: false,
        clearLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setLoadMoreError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setLoadMoreError(
        requestVersion,
        'Unexpected forum comments payload: ${error.message}',
      );
    }
  }

  Future<void> _loadInitial(String postId, {int pageIndex = 1}) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumCommentFeedStatus.loading,
      postId: postId,
      pageIndex: pageIndex > 1 ? pageIndex : 0,
      pageCount: 0,
      totalCount: 0,
      comments: const <ForumCommentSummary>[],
      clearError: true,
      isLoadingMore: false,
      clearLoadMoreError: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getRootCommentsPage(
        postId: postId,
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumCommentFeedStatus.ready,
        postId: postId,
        pageIndex: page.comments.isEmpty ? 0 : page.page,
        pageCount: page.pageCount,
        totalCount: page.dataCount,
        comments: page.comments,
        clearError: true,
        isLoadingMore: false,
        clearLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setInitialError(requestVersion, postId, error.message);
    } on FormatException catch (error) {
      _setInitialError(
        requestVersion,
        postId,
        'Unexpected forum comments payload: ${error.message}',
      );
    }
  }

  void _setInitialError(int requestVersion, String postId, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumCommentFeedStatus.error,
      postId: postId,
      errorMessage: message,
      isLoadingMore: false,
    );
    notifyListeners();
  }

  void _setLoadMoreError(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumCommentFeedStatus.ready,
      isLoadingMore: false,
      loadMoreErrorMessage: message,
    );
    notifyListeners();
  }
}
