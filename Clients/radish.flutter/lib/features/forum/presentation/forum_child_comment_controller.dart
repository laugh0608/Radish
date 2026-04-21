import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';

enum ForumChildCommentStatus {
  idle,
  loading,
  ready,
  error,
}

class ForumChildCommentState {
  const ForumChildCommentState({
    required this.status,
    required this.pageSize,
    required this.pageIndex,
    required this.comments,
    required this.totalCount,
    this.parentId,
    this.errorMessage,
    this.isLoadingMore = false,
    this.loadMoreErrorMessage,
  });

  factory ForumChildCommentState.initial({
    int pageSize = 5,
    List<ForumCommentSummary> seededComments = const <ForumCommentSummary>[],
    int seededTotalCount = 0,
  }) {
    return ForumChildCommentState(
      status: seededComments.isEmpty
          ? ForumChildCommentStatus.idle
          : ForumChildCommentStatus.ready,
      pageSize: pageSize,
      pageIndex: seededComments.isEmpty ? 0 : 1,
      comments: seededComments,
      totalCount: seededTotalCount,
    );
  }

  final ForumChildCommentStatus status;
  final int pageSize;
  final int pageIndex;
  final List<ForumCommentSummary> comments;
  final int totalCount;
  final String? parentId;
  final String? errorMessage;
  final bool isLoadingMore;
  final String? loadMoreErrorMessage;

  bool get isIdle => status == ForumChildCommentStatus.idle;

  bool get isLoading => status == ForumChildCommentStatus.loading;

  bool get isReady => status == ForumChildCommentStatus.ready;

  bool get isError => status == ForumChildCommentStatus.error;

  bool get hasMore => isReady && comments.length < totalCount;

  ForumChildCommentState copyWith({
    ForumChildCommentStatus? status,
    int? pageSize,
    int? pageIndex,
    List<ForumCommentSummary>? comments,
    int? totalCount,
    String? parentId,
    bool clearParentId = false,
    String? errorMessage,
    bool clearError = false,
    bool? isLoadingMore,
    String? loadMoreErrorMessage,
    bool clearLoadMoreError = false,
  }) {
    return ForumChildCommentState(
      status: status ?? this.status,
      pageSize: pageSize ?? this.pageSize,
      pageIndex: pageIndex ?? this.pageIndex,
      comments: comments ?? this.comments,
      totalCount: totalCount ?? this.totalCount,
      parentId: clearParentId ? null : (parentId ?? this.parentId),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      loadMoreErrorMessage: clearLoadMoreError
          ? null
          : (loadMoreErrorMessage ?? this.loadMoreErrorMessage),
    );
  }
}

class ForumChildCommentController extends ChangeNotifier {
  ForumChildCommentController({
    required ForumRepository repository,
    required this.parentId,
    required int pageSize,
    List<ForumCommentSummary> seededComments = const <ForumCommentSummary>[],
    int? seededTotalCount,
  })  : _repository = repository,
        _state = ForumChildCommentState.initial(
          pageSize: pageSize,
          seededComments: seededComments,
          seededTotalCount: seededTotalCount ?? seededComments.length,
        );

  final ForumRepository _repository;
  final String parentId;
  ForumChildCommentState _state;
  int _requestVersion = 0;

  ForumChildCommentState get state => _state;

  Future<void> loadInitialIfNeeded() async {
    if (_state.isLoading ||
        _state.comments.isNotEmpty ||
        _state.totalCount <= 0 ||
        _state.parentId == parentId) {
      return;
    }

    await _loadInitial();
  }

  Future<void> refresh() async {
    if (_state.totalCount <= 0) {
      return;
    }

    await _loadInitial();
  }

  Future<void> loadPage(int pageIndex) async {
    if (pageIndex < 1) {
      return;
    }

    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumChildCommentStatus.loading,
      parentId: parentId,
      pageIndex: pageIndex > 1 ? pageIndex : 0,
      comments: const <ForumCommentSummary>[],
      clearError: true,
      isLoadingMore: false,
      clearLoadMoreError: true,
    );
    notifyListeners();

    try {
      final aggregatedComments = <ForumCommentSummary>[];
      var totalCount = 0;
      var lastLoadedPageIndex = 0;

      for (var currentPage = 1; currentPage <= pageIndex; currentPage += 1) {
        final page = await _repository.getChildCommentsPage(
          parentId: parentId,
          pageIndex: currentPage,
          pageSize: _state.pageSize,
        );

        if (requestVersion != _requestVersion) {
          return;
        }

        totalCount = page.totalCount;
        lastLoadedPageIndex = page.pageIndex;
        final existingIds = aggregatedComments.map((item) => item.id).toSet();
        final appended = page.comments
            .where((item) => !existingIds.contains(item.id))
            .toList(growable: false);
        aggregatedComments.addAll(appended);
      }

      _state = _state.copyWith(
        status: ForumChildCommentStatus.ready,
        parentId: parentId,
        pageIndex: aggregatedComments.isEmpty ? 0 : lastLoadedPageIndex,
        totalCount: totalCount,
        comments: aggregatedComments,
        clearError: true,
        isLoadingMore: false,
        clearLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setInitialError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setInitialError(
        requestVersion,
        'Unexpected child comments payload: ${error.message}',
      );
    }
  }

  Future<void> loadMore() async {
    if (_state.isLoading ||
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
      final page = await _repository.getChildCommentsPage(
        parentId: parentId,
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
        status: ForumChildCommentStatus.ready,
        parentId: parentId,
        pageIndex: page.pageIndex,
        totalCount: page.totalCount,
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
        'Unexpected child comments payload: ${error.message}',
      );
    }
  }

  Future<void> _loadInitial({int pageIndex = 1}) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ForumChildCommentStatus.loading,
      parentId: parentId,
      pageIndex: pageIndex > 1 ? pageIndex : 0,
      comments: const <ForumCommentSummary>[],
      clearError: true,
      isLoadingMore: false,
      clearLoadMoreError: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getChildCommentsPage(
        parentId: parentId,
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ForumChildCommentStatus.ready,
        parentId: parentId,
        pageIndex: page.comments.isEmpty ? 0 : page.pageIndex,
        totalCount: page.totalCount,
        comments: page.comments,
        clearError: true,
        isLoadingMore: false,
        clearLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setInitialError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setInitialError(
        requestVersion,
        'Unexpected child comments payload: ${error.message}',
      );
    }
  }

  void _setInitialError(int requestVersion, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ForumChildCommentStatus.error,
      parentId: parentId,
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
      status: ForumChildCommentStatus.ready,
      isLoadingMore: false,
      loadMoreErrorMessage: message,
    );
    notifyListeners();
  }
}
