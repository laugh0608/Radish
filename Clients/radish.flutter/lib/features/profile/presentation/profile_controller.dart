import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

enum ProfileResourceStatus { idle, loading, ready, unavailable, stale }

enum ProfileIssueKind { unavailable, invalidResponse, request }

class ProfileIssue {
  const ProfileIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory ProfileIssue.fromApi(RadishApiClientException error) {
    final unavailable = error.statusCode == null || error.statusCode == 503;
    return ProfileIssue(
      kind:
          unavailable ? ProfileIssueKind.unavailable : ProfileIssueKind.request,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory ProfileIssue.invalidResponse(
    FormatException error, {
    required String resourceLabel,
  }) {
    return ProfileIssue(
      kind: ProfileIssueKind.invalidResponse,
      message: '$resourceLabel返回格式异常：${error.message}',
      code: 'Profile.InvalidResponse',
    );
  }

  final ProfileIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;
}

class ProfileSnapshot<T> {
  const ProfileSnapshot({
    required this.status,
    this.data,
    this.issue,
    this.isRefreshing = false,
  });

  const ProfileSnapshot.idle() : this(status: ProfileResourceStatus.idle);

  final ProfileResourceStatus status;
  final T? data;
  final ProfileIssue? issue;
  final bool isRefreshing;

  bool get hasData => data != null;
  bool get isLoading => status == ProfileResourceStatus.loading;
  bool get isUnavailable => status == ProfileResourceStatus.unavailable;
  bool get isStale => status == ProfileResourceStatus.stale;

  ProfileSnapshot<T> startLoad() {
    if (data == null) {
      return ProfileSnapshot<T>(status: ProfileResourceStatus.loading);
    }
    return ProfileSnapshot<T>(
      status: status == ProfileResourceStatus.stale
          ? ProfileResourceStatus.stale
          : ProfileResourceStatus.ready,
      data: data,
      issue: issue,
      isRefreshing: true,
    );
  }

  ProfileSnapshot<T> resolve(T nextData) {
    return ProfileSnapshot<T>(
      status: ProfileResourceStatus.ready,
      data: nextData,
    );
  }

  ProfileSnapshot<T> reject(ProfileIssue nextIssue) {
    if (data == null) {
      return ProfileSnapshot<T>(
        status: ProfileResourceStatus.unavailable,
        issue: nextIssue,
      );
    }
    return ProfileSnapshot<T>(
      status: ProfileResourceStatus.stale,
      data: data,
      issue: nextIssue,
    );
  }
}

class ProfilePagedSnapshot<T> {
  const ProfilePagedSnapshot({
    required this.status,
    this.items = const [],
    this.page = 1,
    this.pageSize = 3,
    this.total = 0,
    this.issue,
    this.loadMoreIssue,
    this.isRefreshing = false,
    this.isLoadingMore = false,
  });

  const ProfilePagedSnapshot.idle() : this(status: ProfileResourceStatus.idle);

  final ProfileResourceStatus status;
  final List<T> items;
  final int page;
  final int pageSize;
  final int total;
  final ProfileIssue? issue;
  final ProfileIssue? loadMoreIssue;
  final bool isRefreshing;
  final bool isLoadingMore;

  bool get hasData =>
      status == ProfileResourceStatus.ready ||
      status == ProfileResourceStatus.stale ||
      items.isNotEmpty;
  bool get isLoading => status == ProfileResourceStatus.loading;
  bool get isUnavailable => status == ProfileResourceStatus.unavailable;
  bool get isStale => status == ProfileResourceStatus.stale;
  bool get hasMore => pageSize > 0 && total > items.length;

  ProfilePagedSnapshot<T> startLoad() {
    if (!hasData) {
      return ProfilePagedSnapshot<T>(status: ProfileResourceStatus.loading);
    }
    return ProfilePagedSnapshot<T>(
      status: status == ProfileResourceStatus.stale
          ? ProfileResourceStatus.stale
          : ProfileResourceStatus.ready,
      items: items,
      page: page,
      pageSize: pageSize,
      total: total,
      issue: issue,
      isRefreshing: true,
    );
  }

  ProfilePagedSnapshot<T> resolve({
    required List<T> nextItems,
    required int nextPage,
    required int nextPageSize,
    required int nextTotal,
  }) {
    return ProfilePagedSnapshot<T>(
      status: ProfileResourceStatus.ready,
      items: List<T>.unmodifiable(nextItems),
      page: nextPage,
      pageSize: nextPageSize,
      total: nextTotal,
    );
  }

  ProfilePagedSnapshot<T> reject(ProfileIssue nextIssue) {
    if (!hasData) {
      return ProfilePagedSnapshot<T>(
        status: ProfileResourceStatus.unavailable,
        issue: nextIssue,
      );
    }
    return ProfilePagedSnapshot<T>(
      status: ProfileResourceStatus.stale,
      items: items,
      page: page,
      pageSize: pageSize,
      total: total,
      issue: nextIssue,
    );
  }

  ProfilePagedSnapshot<T> startLoadMore() {
    return ProfilePagedSnapshot<T>(
      status: status,
      items: items,
      page: page,
      pageSize: pageSize,
      total: total,
      issue: issue,
      isRefreshing: isRefreshing,
      isLoadingMore: true,
    );
  }

  ProfilePagedSnapshot<T> resolveLoadMore({
    required List<T> nextItems,
    required int nextPage,
    required int nextPageSize,
    required int nextTotal,
  }) {
    return ProfilePagedSnapshot<T>(
      status: status,
      items: List<T>.unmodifiable(nextItems),
      page: nextPage,
      pageSize: nextPageSize,
      total: nextTotal,
      issue: issue,
    );
  }

  ProfilePagedSnapshot<T> rejectLoadMore(ProfileIssue nextIssue) {
    return ProfilePagedSnapshot<T>(
      status: status,
      items: items,
      page: page,
      pageSize: pageSize,
      total: total,
      issue: issue,
      loadMoreIssue: nextIssue,
    );
  }
}

class ProfileState {
  const ProfileState({
    this.userId,
    this.includesMyQuickReplies = false,
    this.identity = const ProfileSnapshot<PublicProfileSummary>.idle(),
    this.stats = const ProfileSnapshot<PublicProfileStats>.idle(),
    this.posts = const ProfilePagedSnapshot<PublicProfilePostSummary>.idle(),
    this.comments =
        const ProfilePagedSnapshot<PublicProfileCommentSummary>.idle(),
    this.myQuickReplies =
        const ProfilePagedSnapshot<UserQuickReplySummary>.idle(),
  });

  final String? userId;
  final bool includesMyQuickReplies;
  final ProfileSnapshot<PublicProfileSummary> identity;
  final ProfileSnapshot<PublicProfileStats> stats;
  final ProfilePagedSnapshot<PublicProfilePostSummary> posts;
  final ProfilePagedSnapshot<PublicProfileCommentSummary> comments;
  final ProfilePagedSnapshot<UserQuickReplySummary> myQuickReplies;

  bool get isIdle => userId == null;
  bool get isBusy => identity.isLoading || isRefreshing;
  bool get isRefreshing =>
      identity.isRefreshing ||
      stats.isRefreshing ||
      posts.isRefreshing ||
      comments.isRefreshing ||
      myQuickReplies.isRefreshing;

  ProfileState copyWith({
    String? userId,
    bool clearUserId = false,
    bool? includesMyQuickReplies,
    ProfileSnapshot<PublicProfileSummary>? identity,
    ProfileSnapshot<PublicProfileStats>? stats,
    ProfilePagedSnapshot<PublicProfilePostSummary>? posts,
    ProfilePagedSnapshot<PublicProfileCommentSummary>? comments,
    ProfilePagedSnapshot<UserQuickReplySummary>? myQuickReplies,
  }) {
    return ProfileState(
      userId: clearUserId ? null : (userId ?? this.userId),
      includesMyQuickReplies:
          includesMyQuickReplies ?? this.includesMyQuickReplies,
      identity: identity ?? this.identity,
      stats: stats ?? this.stats,
      posts: posts ?? this.posts,
      comments: comments ?? this.comments,
      myQuickReplies: myQuickReplies ?? this.myQuickReplies,
    );
  }
}

class ProfileController extends ChangeNotifier {
  ProfileController({required ProfileRepository repository})
      : _repository = repository;

  final ProfileRepository _repository;
  ProfileState _state = const ProfileState();
  int _targetEpoch = 0;
  int _identityVersion = 0;
  int _statsVersion = 0;
  int _postsVersion = 0;
  int _commentsVersion = 0;
  int _quickRepliesVersion = 0;

  ProfileState get state => _state;

  Future<void> loadForUser(
    String? userId, {
    bool includeMyQuickReplies = false,
    String? accessToken,
  }) async {
    final normalizedUserId = _normalize(userId);
    if (normalizedUserId == null) {
      _clearTarget();
      return;
    }

    if (_state.userId == normalizedUserId &&
        _state.includesMyQuickReplies == includeMyQuickReplies &&
        (_state.identity.isLoading || _state.identity.hasData)) {
      return;
    }

    final epoch = ++_targetEpoch;
    _invalidateResourceRequests();
    _state = ProfileState(
      userId: normalizedUserId,
      includesMyQuickReplies: includeMyQuickReplies,
      identity: const ProfileSnapshot<PublicProfileSummary>(
        status: ProfileResourceStatus.loading,
      ),
      stats: const ProfileSnapshot<PublicProfileStats>(
        status: ProfileResourceStatus.loading,
      ),
      posts: const ProfilePagedSnapshot<PublicProfilePostSummary>(
        status: ProfileResourceStatus.loading,
      ),
      comments: const ProfilePagedSnapshot<PublicProfileCommentSummary>(
        status: ProfileResourceStatus.loading,
      ),
      myQuickReplies: includeMyQuickReplies
          ? const ProfilePagedSnapshot<UserQuickReplySummary>(
              status: ProfileResourceStatus.loading,
            )
          : const ProfilePagedSnapshot<UserQuickReplySummary>.idle(),
    );
    notifyListeners();

    final identityFuture = _loadIdentity(normalizedUserId, epoch: epoch);
    final statsFuture = _loadStats(normalizedUserId, epoch: epoch);
    final postsFuture = _loadPosts(normalizedUserId, epoch: epoch);
    final commentsFuture = _loadComments(normalizedUserId, epoch: epoch);
    final quickRepliesFuture = includeMyQuickReplies
        ? _loadQuickReplies(accessToken, epoch: epoch)
        : Future<void>.value();

    await identityFuture;
    await statsFuture;
    await postsFuture;
    await commentsFuture;
    await quickRepliesFuture;
  }

  Future<void> refresh({String? accessToken}) async {
    final userId = _state.userId;
    if (userId == null) {
      return;
    }

    final epoch = _targetEpoch;
    final identityFuture = _loadIdentity(userId, epoch: epoch);
    final statsFuture = _loadStats(userId, epoch: epoch);
    final postsFuture = _loadPosts(userId, epoch: epoch);
    final commentsFuture = _loadComments(userId, epoch: epoch);
    final quickRepliesFuture = _state.includesMyQuickReplies
        ? _loadQuickReplies(accessToken, epoch: epoch)
        : Future<void>.value();

    await identityFuture;
    await statsFuture;
    await postsFuture;
    await commentsFuture;
    await quickRepliesFuture;
  }

  Future<void> loadMorePosts() async {
    final userId = _state.userId;
    final current = _state.posts;
    if (userId == null ||
        !current.hasMore ||
        current.isLoadingMore ||
        current.isRefreshing) {
      return;
    }

    final epoch = _targetEpoch;
    final version = ++_postsVersion;
    _state = _state.copyWith(posts: current.startLoadMore());
    notifyListeners();

    try {
      final page = await _repository.getPublicPosts(
        userId: userId,
        pageIndex: current.page + 1,
        pageSize: current.pageSize <= 0 ? 3 : current.pageSize,
      );
      if (!_isCurrent(epoch, version, _postsVersion)) {
        return;
      }
      final existingIds = current.items.map((item) => item.id).toSet();
      final items = <PublicProfilePostSummary>[
        ...current.items,
        ...page.posts.where((item) => existingIds.add(item.id)),
      ];
      _state = _state.copyWith(
        posts: current.resolveLoadMore(
          nextItems: items,
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.dataCount,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectPostsLoadMore(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectPostsLoadMore(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开帖子'),
      );
    } catch (error) {
      _rejectPostsLoadMore(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开帖子'),
      );
    }
  }

  Future<void> loadMoreComments() async {
    final userId = _state.userId;
    final current = _state.comments;
    if (userId == null ||
        !current.hasMore ||
        current.isLoadingMore ||
        current.isRefreshing) {
      return;
    }

    final epoch = _targetEpoch;
    final version = ++_commentsVersion;
    _state = _state.copyWith(comments: current.startLoadMore());
    notifyListeners();

    try {
      final page = await _repository.getPublicComments(
        userId: userId,
        pageIndex: current.page + 1,
        pageSize: current.pageSize <= 0 ? 3 : current.pageSize,
      );
      if (!_isCurrent(epoch, version, _commentsVersion)) {
        return;
      }
      final existingIds = current.items.map((item) => item.id).toSet();
      final items = <PublicProfileCommentSummary>[
        ...current.items,
        ...page.comments.where((item) => existingIds.add(item.id)),
      ];
      _state = _state.copyWith(
        comments: current.resolveLoadMore(
          nextItems: items,
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.dataCount,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectCommentsLoadMore(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectCommentsLoadMore(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开评论'),
      );
    } catch (error) {
      _rejectCommentsLoadMore(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开评论'),
      );
    }
  }

  Future<void> loadMoreMyQuickReplies({required String accessToken}) async {
    final current = _state.myQuickReplies;
    final normalizedToken = _normalize(accessToken);
    if (!_state.includesMyQuickReplies ||
        normalizedToken == null ||
        !current.hasMore ||
        current.isLoadingMore ||
        current.isRefreshing) {
      return;
    }

    final epoch = _targetEpoch;
    final version = ++_quickRepliesVersion;
    _state = _state.copyWith(myQuickReplies: current.startLoadMore());
    notifyListeners();

    try {
      final page = await _repository.getMyQuickReplies(
        pageIndex: current.page + 1,
        pageSize: current.pageSize <= 0 ? 3 : current.pageSize,
        accessToken: normalizedToken,
      );
      if (!_isCurrent(epoch, version, _quickRepliesVersion)) {
        return;
      }
      final existingIds = current.items.map((item) => item.id).toSet();
      final items = <UserQuickReplySummary>[
        ...current.items,
        ...page.items.where((item) => existingIds.add(item.id)),
      ];
      _state = _state.copyWith(
        myQuickReplies: current.resolveLoadMore(
          nextItems: items,
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.total,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectQuickRepliesLoadMore(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectQuickRepliesLoadMore(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '我的轻回应'),
      );
    } catch (error) {
      _rejectQuickRepliesLoadMore(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '我的轻回应'),
      );
    }
  }

  Future<void> _loadIdentity(String userId, {required int epoch}) async {
    final version = ++_identityVersion;
    _state = _state.copyWith(identity: _state.identity.startLoad());
    notifyListeners();
    try {
      final profile = await _repository.getPublicProfile(userId: userId);
      if (!_isCurrent(epoch, version, _identityVersion)) {
        return;
      }
      _state = _state.copyWith(identity: _state.identity.resolve(profile));
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectIdentity(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectIdentity(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开资料'),
      );
    } catch (error) {
      _rejectIdentity(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开资料'),
      );
    }
  }

  Future<void> _loadStats(String userId, {required int epoch}) async {
    final version = ++_statsVersion;
    _state = _state.copyWith(stats: _state.stats.startLoad());
    notifyListeners();
    try {
      final stats = await _repository.getPublicStats(userId: userId);
      if (!_isCurrent(epoch, version, _statsVersion)) {
        return;
      }
      _state = _state.copyWith(stats: _state.stats.resolve(stats));
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectStats(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectStats(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开统计'),
      );
    } catch (error) {
      _rejectStats(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开统计'),
      );
    }
  }

  Future<void> _loadPosts(String userId, {required int epoch}) async {
    final version = ++_postsVersion;
    _state = _state.copyWith(posts: _state.posts.startLoad());
    notifyListeners();
    try {
      final page = await _repository.getPublicPosts(
        userId: userId,
        pageIndex: 1,
        pageSize: 3,
      );
      if (!_isCurrent(epoch, version, _postsVersion)) {
        return;
      }
      _state = _state.copyWith(
        posts: _state.posts.resolve(
          nextItems: _dedupeByStringId(
            page.posts,
            (item) => item.id,
          ),
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.dataCount,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectPosts(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectPosts(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开帖子'),
      );
    } catch (error) {
      _rejectPosts(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开帖子'),
      );
    }
  }

  Future<void> _loadComments(String userId, {required int epoch}) async {
    final version = ++_commentsVersion;
    _state = _state.copyWith(comments: _state.comments.startLoad());
    notifyListeners();
    try {
      final page = await _repository.getPublicComments(
        userId: userId,
        pageIndex: 1,
        pageSize: 3,
      );
      if (!_isCurrent(epoch, version, _commentsVersion)) {
        return;
      }
      _state = _state.copyWith(
        comments: _state.comments.resolve(
          nextItems: _dedupeByStringId(
            page.comments,
            (item) => item.id,
          ),
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.dataCount,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectComments(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectComments(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '公开评论'),
      );
    } catch (error) {
      _rejectComments(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '公开评论'),
      );
    }
  }

  Future<void> _loadQuickReplies(
    String? accessToken, {
    required int epoch,
  }) async {
    final normalizedToken = _normalize(accessToken);
    final version = ++_quickRepliesVersion;
    _state = _state.copyWith(
      myQuickReplies: _state.myQuickReplies.startLoad(),
    );
    notifyListeners();

    if (normalizedToken == null) {
      _rejectQuickReplies(
        epoch,
        version,
        const ProfileIssue(
          kind: ProfileIssueKind.request,
          message: '登录会话不可用，无法读取我的轻回应。',
          code: 'Profile.SessionUnavailable',
        ),
      );
      return;
    }

    try {
      final page = await _repository.getMyQuickReplies(
        pageIndex: 1,
        pageSize: 3,
        accessToken: normalizedToken,
      );
      if (!_isCurrent(epoch, version, _quickRepliesVersion)) {
        return;
      }
      _state = _state.copyWith(
        myQuickReplies: _state.myQuickReplies.resolve(
          nextItems: _dedupeByStringId(
            page.items,
            (item) => item.id,
          ),
          nextPage: page.page,
          nextPageSize: page.pageSize,
          nextTotal: page.total,
        ),
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectQuickReplies(epoch, version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectQuickReplies(
        epoch,
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '我的轻回应'),
      );
    } catch (error) {
      _rejectQuickReplies(
        epoch,
        version,
        _unexpectedProfileIssue(error, resourceLabel: '我的轻回应'),
      );
    }
  }

  void _rejectIdentity(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _identityVersion)) return;
    _state = _state.copyWith(identity: _state.identity.reject(issue));
    notifyListeners();
  }

  void _rejectStats(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _statsVersion)) return;
    _state = _state.copyWith(stats: _state.stats.reject(issue));
    notifyListeners();
  }

  void _rejectPosts(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _postsVersion)) return;
    _state = _state.copyWith(posts: _state.posts.reject(issue));
    notifyListeners();
  }

  void _rejectComments(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _commentsVersion)) return;
    _state = _state.copyWith(comments: _state.comments.reject(issue));
    notifyListeners();
  }

  void _rejectQuickReplies(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _quickRepliesVersion)) return;
    _state = _state.copyWith(
      myQuickReplies: _state.myQuickReplies.reject(issue),
    );
    notifyListeners();
  }

  void _rejectPostsLoadMore(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _postsVersion)) return;
    _state = _state.copyWith(posts: _state.posts.rejectLoadMore(issue));
    notifyListeners();
  }

  void _rejectCommentsLoadMore(int epoch, int version, ProfileIssue issue) {
    if (!_isCurrent(epoch, version, _commentsVersion)) return;
    _state = _state.copyWith(comments: _state.comments.rejectLoadMore(issue));
    notifyListeners();
  }

  void _rejectQuickRepliesLoadMore(
    int epoch,
    int version,
    ProfileIssue issue,
  ) {
    if (!_isCurrent(epoch, version, _quickRepliesVersion)) return;
    _state = _state.copyWith(
      myQuickReplies: _state.myQuickReplies.rejectLoadMore(issue),
    );
    notifyListeners();
  }

  bool _isCurrent(int epoch, int version, int currentVersion) {
    return epoch == _targetEpoch && version == currentVersion;
  }

  void _clearTarget() {
    if (_state.isIdle) return;
    _targetEpoch++;
    _invalidateResourceRequests();
    _state = const ProfileState();
    notifyListeners();
  }

  void _invalidateResourceRequests() {
    _identityVersion++;
    _statsVersion++;
    _postsVersion++;
    _commentsVersion++;
    _quickRepliesVersion++;
  }

  @override
  void dispose() {
    _targetEpoch++;
    _invalidateResourceRequests();
    super.dispose();
  }
}

String? _normalize(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}

List<T> _dedupeByStringId<T>(
  Iterable<T> items,
  String Function(T item) idOf,
) {
  final seen = <String>{};
  return List<T>.unmodifiable(
    items.where((item) => seen.add(idOf(item))),
  );
}

ProfileIssue _unexpectedProfileIssue(
  Object error, {
  required String resourceLabel,
}) {
  final message = error.toString().trim();
  return ProfileIssue(
    kind: ProfileIssueKind.request,
    message: message.isEmpty ? '$resourceLabel请求失败。' : message,
    code: 'Profile.Unexpected',
  );
}
