part of 'profile_page_test.dart';

void registerProfileControllerTests() {
  test('keeps secondary first-load failures inside their own snapshots',
      () async {
    final repository = _SelectiveProfileFailureRepository()
      ..failStats = true
      ..failPosts = true
      ..failComments = true
      ..failQuickReplies = true;
    final controller = ProfileController(repository: repository);
    addTearDown(controller.dispose);

    await controller.loadForUser(
      'user-a',
      includeMyQuickReplies: true,
      accessToken: 'token-a',
    );

    expect(controller.state.identity.status, ProfileResourceStatus.ready);
    expect(controller.state.identity.data?.userId, 'user-a');
    expect(controller.state.stats.status, ProfileResourceStatus.unavailable);
    expect(controller.state.posts.status, ProfileResourceStatus.unavailable);
    expect(controller.state.comments.status, ProfileResourceStatus.unavailable);
    expect(
      controller.state.myQuickReplies.status,
      ProfileResourceStatus.unavailable,
    );
    expect(controller.state.stats.issue?.code, 'Profile.StatsUnavailable');
    expect(controller.state.stats.issue?.statusCode, 503);
  });

  test('marks only failed refresh resources stale', () async {
    final repository = _SelectiveProfileFailureRepository();
    final controller = ProfileController(repository: repository);
    addTearDown(controller.dispose);
    await controller.loadForUser(
      'user-a',
      includeMyQuickReplies: true,
      accessToken: 'token-a',
    );

    repository
      ..failStats = true
      ..failPosts = true
      ..failComments = true
      ..failQuickReplies = true;
    await controller.refresh(accessToken: 'token-a');

    expect(controller.state.identity.status, ProfileResourceStatus.ready);
    expect(controller.state.stats.status, ProfileResourceStatus.stale);
    expect(controller.state.stats.data?.postCount, 12);
    expect(controller.state.posts.status, ProfileResourceStatus.stale);
    expect(controller.state.posts.items, isNotEmpty);
    expect(controller.state.comments.status, ProfileResourceStatus.stale);
    expect(controller.state.comments.items, isNotEmpty);
    expect(
      controller.state.myQuickReplies.status,
      ProfileResourceStatus.stale,
    );
    expect(controller.state.myQuickReplies.items, isNotEmpty);
  });

  test('drops a late identity response after the target changes', () async {
    final repository = _TargetSwitchProfileRepository();
    final controller = ProfileController(repository: repository);
    addTearDown(controller.dispose);

    final firstLoad = controller.loadForUser('user-a');
    await Future<void>.delayed(Duration.zero);
    await controller.loadForUser('user-b');
    repository.userAIdentity.complete(
      const PublicProfileSummary(
        userId: 'user-a',
        userName: 'late-user-a',
        createTime: '2026-08-23T08:00:00Z',
      ),
    );
    await firstLoad;

    expect(controller.state.userId, 'user-b');
    expect(controller.state.identity.data?.userId, 'user-b');
    expect(controller.state.identity.data?.userName, 'user-b-name');
  });

  test('drops pending responses after the controller is disposed', () async {
    final repository = _TargetSwitchProfileRepository();
    final controller = ProfileController(repository: repository);

    final load = controller.loadForUser('user-a');
    await Future<void>.delayed(Duration.zero);
    controller.dispose();
    repository.userAIdentity.complete(
      const PublicProfileSummary(
        userId: 'user-a',
        userName: 'disposed-user-a',
        createTime: '2026-08-23T08:00:00Z',
      ),
    );

    await expectLater(load, completes);
  });

  test('clears private quick replies when switching to a public target',
      () async {
    final controller = ProfileController(
      repository: _SuccessProfileRepository(),
    );
    addTearDown(controller.dispose);

    await controller.loadForUser(
      'my-user',
      includeMyQuickReplies: true,
      accessToken: 'my-token',
    );
    expect(controller.state.myQuickReplies.items, isNotEmpty);

    await controller.loadForUser('public-user');

    expect(controller.state.includesMyQuickReplies, isFalse);
    expect(controller.state.myQuickReplies.status, ProfileResourceStatus.idle);
    expect(controller.state.myQuickReplies.items, isEmpty);
  });

  test('keeps three append generations independent and deduplicated', () async {
    final repository = _IndependentPagedProfileRepository();
    final controller = ProfileController(repository: repository);
    addTearDown(controller.dispose);
    await controller.loadForUser(
      'my-user',
      includeMyQuickReplies: true,
      accessToken: 'my-token',
    );

    final postsLoad = controller.loadMorePosts();
    final commentsLoad = controller.loadMoreComments();
    final repliesLoad = controller.loadMoreMyQuickReplies(
      accessToken: 'my-token',
    );
    expect(controller.state.posts.isLoadingMore, isTrue);
    expect(controller.state.comments.isLoadingMore, isTrue);
    expect(controller.state.myQuickReplies.isLoadingMore, isTrue);

    repository.commentsPageTwo.complete(_duplicateCommentPageTwo);
    await Future<void>.delayed(Duration.zero);
    expect(controller.state.comments.items.map((item) => item.id), [
      'comment-1',
      'comment-2',
    ]);
    expect(controller.state.posts.isLoadingMore, isTrue);
    expect(controller.state.myQuickReplies.isLoadingMore, isTrue);

    repository.quickRepliesPageTwo.complete(_duplicateQuickReplyPageTwo);
    repository.postsPageTwo.complete(_duplicatePostPageTwo);
    await Future.wait([postsLoad, commentsLoad, repliesLoad]);

    expect(controller.state.posts.items.map((item) => item.id), [
      'post-1',
      'post-2',
    ]);
    expect(controller.state.comments.items.map((item) => item.id), [
      'comment-1',
      'comment-2',
    ]);
    expect(controller.state.myQuickReplies.items.map((item) => item.id), [
      'quick-1',
      'quick-2',
    ]);
  });

  test('deduplicates repeated ids in authoritative first pages', () async {
    final controller = ProfileController(
      repository: _DuplicateFirstPageProfileRepository(),
    );
    addTearDown(controller.dispose);

    await controller.loadForUser(
      'my-user',
      includeMyQuickReplies: true,
      accessToken: 'my-token',
    );

    expect(controller.state.posts.items, hasLength(1));
    expect(controller.state.comments.items, hasLength(1));
    expect(controller.state.myQuickReplies.items, hasLength(1));
  });
}

class _SelectiveProfileFailureRepository extends _SuccessProfileRepository {
  bool failStats = false;
  bool failPosts = false;
  bool failComments = false;
  bool failQuickReplies = false;

  @override
  Future<PublicProfileStats> getPublicStats({required String userId}) {
    if (failStats) {
      throw const RadishApiClientException(
        '公开统计暂时不可用',
        statusCode: 503,
        code: 'Profile.StatsUnavailable',
      );
    }
    return super.getPublicStats(userId: userId);
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (failPosts) {
      throw const RadishApiClientException('公开帖子暂时不可用');
    }
    return super.getPublicPosts(
      userId: userId,
      pageIndex: pageIndex,
      pageSize: pageSize,
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (failComments) {
      throw const RadishApiClientException('公开评论暂时不可用');
    }
    return super.getPublicComments(
      userId: userId,
      pageIndex: pageIndex,
      pageSize: pageSize,
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    if (failQuickReplies) {
      throw const RadishApiClientException('我的轻回应暂时不可用');
    }
    return super.getMyQuickReplies(
      pageIndex: pageIndex,
      pageSize: pageSize,
      accessToken: accessToken,
    );
  }
}

class _TargetSwitchProfileRepository extends _SuccessProfileRepository {
  final userAIdentity = Completer<PublicProfileSummary>();

  @override
  Future<PublicProfileSummary> getPublicProfile({required String userId}) {
    if (userId == 'user-a') return userAIdentity.future;
    return Future.value(
      PublicProfileSummary(
        userId: userId,
        userName: '$userId-name',
        createTime: '2026-08-23T08:00:00Z',
      ),
    );
  }
}

class _IndependentPagedProfileRepository extends _SuccessProfileRepository {
  final postsPageTwo = Completer<PublicProfilePostPage>();
  final commentsPageTwo = Completer<PublicProfileCommentPage>();
  final quickRepliesPageTwo = Completer<UserQuickReplyPage>();

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex > 1) return postsPageTwo.future;
    return Future.value(_postPageOneWithMore);
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex > 1) return commentsPageTwo.future;
    return Future.value(_commentPageOneWithMore);
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    if (pageIndex > 1) return quickRepliesPageTwo.future;
    return Future.value(_quickReplyPageOneWithMore);
  }
}

class _DuplicateFirstPageProfileRepository
    extends _IndependentPagedProfileRepository {
  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      posts: [_postOne, _postOne],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfileCommentPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      comments: [_commentOne, _commentOne],
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserQuickReplyPage(
      page: 1,
      pageSize: 3,
      total: 1,
      items: [_quickReplyOne, _quickReplyOne],
    );
  }
}

const _postOne = PublicProfilePostSummary(
  id: 'post-1',
  title: 'Post one',
  content: 'Post one',
  viewCount: 1,
  likeCount: 1,
  commentCount: 1,
  createTime: '2026-08-23T08:00:00Z',
);
const _postTwo = PublicProfilePostSummary(
  id: 'post-2',
  title: 'Post two',
  content: 'Post two',
  viewCount: 2,
  likeCount: 2,
  commentCount: 2,
  createTime: '2026-08-23T09:00:00Z',
);
const _commentOne = PublicProfileCommentSummary(
  id: 'comment-1',
  postId: 'post-1',
  content: 'Comment one',
  likeCount: 1,
  createTime: '2026-08-23T08:00:00Z',
);
const _commentTwo = PublicProfileCommentSummary(
  id: 'comment-2',
  postId: 'post-2',
  content: 'Comment two',
  likeCount: 2,
  createTime: '2026-08-23T09:00:00Z',
);
const _quickReplyOne = UserQuickReplySummary(
  id: 'quick-1',
  postId: 'post-1',
  postTitle: 'Post one',
  content: 'Quick reply one',
  createTime: '2026-08-23T08:00:00Z',
);
const _quickReplyTwo = UserQuickReplySummary(
  id: 'quick-2',
  postId: 'post-2',
  postTitle: 'Post two',
  content: 'Quick reply two',
  createTime: '2026-08-23T09:00:00Z',
);

const _postPageOneWithMore = PublicProfilePostPage(
  page: 1,
  pageSize: 1,
  dataCount: 2,
  pageCount: 2,
  posts: [_postOne],
);
const _duplicatePostPageTwo = PublicProfilePostPage(
  page: 2,
  pageSize: 1,
  dataCount: 2,
  pageCount: 2,
  posts: [_postOne, _postTwo],
);
const _commentPageOneWithMore = PublicProfileCommentPage(
  page: 1,
  pageSize: 1,
  dataCount: 2,
  pageCount: 2,
  comments: [_commentOne],
);
const _duplicateCommentPageTwo = PublicProfileCommentPage(
  page: 2,
  pageSize: 1,
  dataCount: 2,
  pageCount: 2,
  comments: [_commentOne, _commentTwo],
);
const _quickReplyPageOneWithMore = UserQuickReplyPage(
  page: 1,
  pageSize: 1,
  total: 2,
  items: [_quickReplyOne],
);
const _duplicateQuickReplyPageTwo = UserQuickReplyPage(
  page: 2,
  pageSize: 1,
  total: 2,
  items: [_quickReplyOne, _quickReplyTwo],
);
