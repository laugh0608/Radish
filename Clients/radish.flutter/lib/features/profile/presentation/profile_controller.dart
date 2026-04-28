import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

enum ProfileStatus {
  idle,
  loading,
  ready,
  error,
}

class ProfileState {
  const ProfileState({
    required this.status,
    this.userId,
    this.profile,
    this.stats,
    this.posts = const <PublicProfilePostSummary>[],
    this.comments = const <PublicProfileCommentSummary>[],
    this.commentsPage = 1,
    this.commentsPageSize = 3,
    this.commentsTotal = 0,
    this.isLoadingMoreComments = false,
    this.commentsLoadMoreErrorMessage,
    this.myQuickReplies = const <UserQuickReplySummary>[],
    this.myQuickRepliesPage = 1,
    this.myQuickRepliesPageSize = 3,
    this.myQuickRepliesTotal = 0,
    this.includesMyQuickReplies = false,
    this.isLoadingMoreMyQuickReplies = false,
    this.myQuickRepliesErrorMessage,
    this.myQuickRepliesLoadMoreErrorMessage,
    this.errorMessage,
  });

  const ProfileState.idle()
      : this(
          status: ProfileStatus.idle,
        );

  final ProfileStatus status;
  final String? userId;
  final PublicProfileSummary? profile;
  final PublicProfileStats? stats;
  final List<PublicProfilePostSummary> posts;
  final List<PublicProfileCommentSummary> comments;
  final int commentsPage;
  final int commentsPageSize;
  final int commentsTotal;
  final bool isLoadingMoreComments;
  final String? commentsLoadMoreErrorMessage;
  final List<UserQuickReplySummary> myQuickReplies;
  final int myQuickRepliesPage;
  final int myQuickRepliesPageSize;
  final int myQuickRepliesTotal;
  final bool includesMyQuickReplies;
  final bool isLoadingMoreMyQuickReplies;
  final String? myQuickRepliesErrorMessage;
  final String? myQuickRepliesLoadMoreErrorMessage;
  final String? errorMessage;

  bool get isIdle => status == ProfileStatus.idle;

  bool get isLoading => status == ProfileStatus.loading;

  bool get isReady => status == ProfileStatus.ready;

  bool get isError => status == ProfileStatus.error;

  bool get hasMoreMyQuickReplies =>
      includesMyQuickReplies &&
      myQuickRepliesTotal > myQuickReplies.length &&
      myQuickRepliesPageSize > 0;

  bool get hasMoreComments =>
      commentsTotal > comments.length && commentsPageSize > 0;

  ProfileState copyWith({
    ProfileStatus? status,
    String? userId,
    bool clearUserId = false,
    PublicProfileSummary? profile,
    bool clearProfile = false,
    PublicProfileStats? stats,
    bool clearStats = false,
    List<PublicProfilePostSummary>? posts,
    bool clearPosts = false,
    List<PublicProfileCommentSummary>? comments,
    bool clearComments = false,
    int? commentsPage,
    int? commentsPageSize,
    int? commentsTotal,
    bool? isLoadingMoreComments,
    String? commentsLoadMoreErrorMessage,
    bool clearCommentsLoadMoreError = false,
    List<UserQuickReplySummary>? myQuickReplies,
    bool clearMyQuickReplies = false,
    int? myQuickRepliesPage,
    int? myQuickRepliesPageSize,
    int? myQuickRepliesTotal,
    bool? includesMyQuickReplies,
    bool? isLoadingMoreMyQuickReplies,
    String? myQuickRepliesErrorMessage,
    bool clearMyQuickRepliesError = false,
    String? myQuickRepliesLoadMoreErrorMessage,
    bool clearMyQuickRepliesLoadMoreError = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ProfileState(
      status: status ?? this.status,
      userId: clearUserId ? null : (userId ?? this.userId),
      profile: clearProfile ? null : (profile ?? this.profile),
      stats: clearStats ? null : (stats ?? this.stats),
      posts: clearPosts
          ? const <PublicProfilePostSummary>[]
          : (posts ?? this.posts),
      comments: clearComments
          ? const <PublicProfileCommentSummary>[]
          : (comments ?? this.comments),
      commentsPage: commentsPage ?? this.commentsPage,
      commentsPageSize: commentsPageSize ?? this.commentsPageSize,
      commentsTotal: commentsTotal ?? this.commentsTotal,
      isLoadingMoreComments:
          isLoadingMoreComments ?? this.isLoadingMoreComments,
      commentsLoadMoreErrorMessage: clearCommentsLoadMoreError
          ? null
          : (commentsLoadMoreErrorMessage ?? this.commentsLoadMoreErrorMessage),
      myQuickReplies: clearMyQuickReplies
          ? const <UserQuickReplySummary>[]
          : (myQuickReplies ?? this.myQuickReplies),
      myQuickRepliesPage: myQuickRepliesPage ?? this.myQuickRepliesPage,
      myQuickRepliesPageSize:
          myQuickRepliesPageSize ?? this.myQuickRepliesPageSize,
      myQuickRepliesTotal: myQuickRepliesTotal ?? this.myQuickRepliesTotal,
      includesMyQuickReplies:
          includesMyQuickReplies ?? this.includesMyQuickReplies,
      isLoadingMoreMyQuickReplies:
          isLoadingMoreMyQuickReplies ?? this.isLoadingMoreMyQuickReplies,
      myQuickRepliesErrorMessage: clearMyQuickRepliesError
          ? null
          : (myQuickRepliesErrorMessage ?? this.myQuickRepliesErrorMessage),
      myQuickRepliesLoadMoreErrorMessage: clearMyQuickRepliesLoadMoreError
          ? null
          : (myQuickRepliesLoadMoreErrorMessage ??
              this.myQuickRepliesLoadMoreErrorMessage),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class ProfileController extends ChangeNotifier {
  ProfileController({
    required ProfileRepository repository,
  }) : _repository = repository;

  final ProfileRepository _repository;
  ProfileState _state = const ProfileState.idle();
  int _requestVersion = 0;

  ProfileState get state => _state;

  Future<void> loadForUser(
    String? userId, {
    bool includeMyQuickReplies = false,
    String? accessToken,
  }) async {
    final normalizedUserId = userId?.trim();
    if (normalizedUserId == null || normalizedUserId.isEmpty) {
      _requestVersion++;
      _state = const ProfileState.idle();
      notifyListeners();
      return;
    }

    if (_state.userId == normalizedUserId &&
        _state.includesMyQuickReplies == includeMyQuickReplies &&
        (_state.isLoading || _state.isReady)) {
      return;
    }

    await _load(
      normalizedUserId,
      includeMyQuickReplies: includeMyQuickReplies,
      accessToken: accessToken,
    );
  }

  Future<void> refresh({
    String? accessToken,
  }) async {
    final userId = _state.userId;
    if (userId == null || userId.isEmpty) {
      return;
    }

    await _load(
      userId,
      includeMyQuickReplies: _state.includesMyQuickReplies,
      accessToken: accessToken,
    );
  }

  Future<void> loadMoreComments() async {
    final userId = _state.userId;
    if (!_state.isReady ||
        userId == null ||
        userId.isEmpty ||
        !_state.hasMoreComments ||
        _state.isLoadingMoreComments) {
      return;
    }

    final requestVersion = ++_requestVersion;
    final nextPage = _state.commentsPage + 1;
    final pageSize = _state.commentsPageSize <= 0 ? 3 : _state.commentsPageSize;
    _state = _state.copyWith(
      isLoadingMoreComments: true,
      clearCommentsLoadMoreError: true,
    );
    notifyListeners();

    try {
      final commentPage = await _repository.getPublicComments(
        userId: userId,
        pageIndex: nextPage,
        pageSize: pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      final existingIds = _state.comments.map((item) => item.id).toSet();
      final nextComments = <PublicProfileCommentSummary>[
        ..._state.comments,
        ...commentPage.comments.where((item) => !existingIds.contains(item.id)),
      ];

      _state = _state.copyWith(
        comments: nextComments,
        commentsPage: commentPage.page,
        commentsPageSize: commentPage.pageSize,
        commentsTotal: commentPage.dataCount,
        isLoadingMoreComments: false,
        clearCommentsLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setCommentsLoadMoreError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setCommentsLoadMoreError(
        requestVersion,
        '公开评论返回格式异常：${error.message}',
      );
    }
  }

  Future<void> loadMoreMyQuickReplies({
    required String accessToken,
  }) async {
    final normalizedAccessToken = accessToken.trim();
    if (!_state.isReady ||
        !_state.includesMyQuickReplies ||
        !_state.hasMoreMyQuickReplies ||
        _state.isLoadingMoreMyQuickReplies ||
        normalizedAccessToken.isEmpty) {
      return;
    }

    final requestVersion = ++_requestVersion;
    final nextPage = _state.myQuickRepliesPage + 1;
    final pageSize =
        _state.myQuickRepliesPageSize <= 0 ? 3 : _state.myQuickRepliesPageSize;
    _state = _state.copyWith(
      isLoadingMoreMyQuickReplies: true,
      clearMyQuickRepliesLoadMoreError: true,
    );
    notifyListeners();

    try {
      final quickReplyPage = await _repository.getMyQuickReplies(
        pageIndex: nextPage,
        pageSize: pageSize,
        accessToken: normalizedAccessToken,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      final existingIds = _state.myQuickReplies.map((item) => item.id).toSet();
      final nextItems = <UserQuickReplySummary>[
        ..._state.myQuickReplies,
        ...quickReplyPage.items.where((item) => !existingIds.contains(item.id)),
      ];

      _state = _state.copyWith(
        myQuickReplies: nextItems,
        myQuickRepliesPage: quickReplyPage.page,
        myQuickRepliesPageSize: quickReplyPage.pageSize,
        myQuickRepliesTotal: quickReplyPage.total,
        isLoadingMoreMyQuickReplies: false,
        clearMyQuickRepliesLoadMoreError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setMyQuickRepliesLoadMoreError(requestVersion, error.message);
    } on FormatException catch (error) {
      _setMyQuickRepliesLoadMoreError(
        requestVersion,
        '我的轻回应返回格式异常：${error.message}',
      );
    }
  }

  Future<void> _load(
    String userId, {
    required bool includeMyQuickReplies,
    String? accessToken,
  }) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ProfileStatus.loading,
      userId: userId,
      includesMyQuickReplies: includeMyQuickReplies,
      clearStats: true,
      clearPosts: true,
      clearComments: true,
      commentsPage: 1,
      commentsPageSize: 3,
      commentsTotal: 0,
      isLoadingMoreComments: false,
      clearCommentsLoadMoreError: true,
      clearMyQuickReplies: true,
      myQuickRepliesPage: 1,
      myQuickRepliesPageSize: 3,
      myQuickRepliesTotal: 0,
      clearMyQuickRepliesError: true,
      isLoadingMoreMyQuickReplies: false,
      clearMyQuickRepliesLoadMoreError: true,
      clearError: true,
    );
    notifyListeners();

    try {
      final results = await Future.wait<Object>([
        _repository.getPublicProfile(userId: userId),
        _repository.getPublicStats(userId: userId),
        _repository.getPublicPosts(
          userId: userId,
          pageIndex: 1,
          pageSize: 3,
        ),
        _repository.getPublicComments(
          userId: userId,
          pageIndex: 1,
          pageSize: 3,
        ),
      ]);

      if (requestVersion != _requestVersion) {
        return;
      }

      final profile = results[0] as PublicProfileSummary;
      final stats = results[1] as PublicProfileStats;
      final posts = (results[2] as PublicProfilePostPage).posts;
      final commentPage = results[3] as PublicProfileCommentPage;
      final comments = commentPage.comments;
      var myQuickReplies = const <UserQuickReplySummary>[];
      var myQuickRepliesPage = 1;
      var myQuickRepliesPageSize = 3;
      var myQuickRepliesTotal = 0;
      String? myQuickRepliesErrorMessage;

      final normalizedAccessToken = accessToken?.trim();
      if (includeMyQuickReplies &&
          normalizedAccessToken != null &&
          normalizedAccessToken.isNotEmpty) {
        try {
          final quickReplyPage = await _repository.getMyQuickReplies(
            pageIndex: 1,
            pageSize: 3,
            accessToken: normalizedAccessToken,
          );
          myQuickReplies = quickReplyPage.items;
          myQuickRepliesPage = quickReplyPage.page;
          myQuickRepliesPageSize = quickReplyPage.pageSize;
          myQuickRepliesTotal = quickReplyPage.total;
        } on RadishApiClientException catch (error) {
          myQuickRepliesErrorMessage = error.message;
        } on FormatException catch (error) {
          myQuickRepliesErrorMessage = '我的轻回应返回格式异常：${error.message}';
        }
      }

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ProfileStatus.ready,
        userId: userId,
        profile: profile,
        stats: stats,
        posts: posts,
        comments: comments,
        commentsPage: commentPage.page,
        commentsPageSize: commentPage.pageSize,
        commentsTotal: commentPage.dataCount,
        isLoadingMoreComments: false,
        clearCommentsLoadMoreError: true,
        myQuickReplies: myQuickReplies,
        myQuickRepliesPage: myQuickRepliesPage,
        myQuickRepliesPageSize: myQuickRepliesPageSize,
        myQuickRepliesTotal: myQuickRepliesTotal,
        includesMyQuickReplies: includeMyQuickReplies,
        isLoadingMoreMyQuickReplies: false,
        myQuickRepliesErrorMessage: myQuickRepliesErrorMessage,
        clearMyQuickRepliesError: myQuickRepliesErrorMessage == null,
        clearMyQuickRepliesLoadMoreError: true,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, userId, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        userId,
        '公开资料返回格式异常：${error.message}',
      );
    }
  }

  void _setError(int requestVersion, String userId, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ProfileStatus.error,
      userId: userId,
      errorMessage: message,
    );
    notifyListeners();
  }

  void _setCommentsLoadMoreError(
    int requestVersion,
    String message,
  ) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      isLoadingMoreComments: false,
      commentsLoadMoreErrorMessage: message,
    );
    notifyListeners();
  }

  void _setMyQuickRepliesLoadMoreError(
    int requestVersion,
    String message,
  ) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      isLoadingMoreMyQuickReplies: false,
      myQuickRepliesLoadMoreErrorMessage: message,
    );
    notifyListeners();
  }
}
