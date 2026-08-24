part of 'smoke_test.dart';

class _FakeProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 0,
      commentCount: 0,
      totalLikeCount: 0,
      postLikeCount: 0,
      commentLikeCount: 0,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 0,
      pageCount: 1,
      posts: [],
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
      dataCount: 0,
      pageCount: 1,
      comments: [],
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
      total: 0,
      items: [],
    );
  }

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserBrowseHistoryPage(
      page: 1,
      pageSize: 20,
      total: 0,
      items: [],
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw UnimplementedError();
  }
}

class _SeededLeaderboardRepository implements LeaderboardRepository {
  const _SeededLeaderboardRepository();

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const LeaderboardPageResult(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      items: [
        LeaderboardItem(
          rank: 1,
          userId: '9',
          userName: 'luobo',
          currentLevel: 8,
          currentLevelName: '探索者',
          primaryValue: '18888',
          primaryLabel: '总经验值',
        ),
      ],
    );
  }
}

class _FakeForumNotificationRepository implements NotificationRepository {
  const _FakeForumNotificationRepository();

  @override
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const NotificationPage(
      notifications: [
        NotificationListItem(
          id: 'notification-system',
          notificationId: 'system-1',
          notification: NotificationPayload(
            title: '系统维护',
            content: '今晚 23:00 进行维护。',
            type: 'System',
            businessType: 'System',
            createdAt: null,
            extData: {'app': 'system'},
          ),
          isRead: false,
          createdAt: '2026-05-31T08:30:00',
        ),
        NotificationListItem(
          id: 'notification-forum-comment',
          notificationId: 'forum-1',
          notification: NotificationPayload(
            title: '帖子被评论',
            content: '有人评论了你的帖子。',
            type: 'CommentReplied',
            businessType: 'Comment',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
              'commentId': 'comment-big-1',
            },
          ),
          isRead: false,
          createdAt: '2026-05-31T08:31:00',
        ),
        NotificationListItem(
          id: 'notification-forum-like',
          notificationId: 'forum-2',
          notification: NotificationPayload(
            title: '帖子收到轻回应',
            content: '你的帖子收到新的轻回应。',
            type: 'PostLiked',
            businessType: 'Post',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
            },
          ),
          isRead: true,
          createdAt: '2026-05-31T08:32:00',
        ),
      ],
    );
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const [
      ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: '帖子被评论',
        commentId: 'comment-big-1',
      ),
      ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: '帖子收到轻回应',
      ),
    ];
  }

  @override
  Future<int> markAsRead({
    required String accessToken,
    required String notificationId,
  }) async {
    return 1;
  }
}

class _MutableForumNotificationRepository implements NotificationRepository {
  ForumDetailHandoffTarget? target;
  List<NotificationListItem>? notifications;
  Object? error;
  Object? markAsReadError;
  int callCount = 0;
  final List<String> markedReadNotificationIds = <String>[];

  @override
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final notifications = this.notifications;
    if (notifications != null) {
      callCount += 1;
      final error = this.error;
      if (error != null) {
        throw error;
      }

      return NotificationPage(notifications: notifications);
    }

    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return NotificationPage.fromForumTargets(targets);
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.isEmpty ? null : targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    callCount += 1;
    final error = this.error;
    if (error != null) {
      throw error;
    }

    final target = this.target;
    return target == null ? const <ForumDetailHandoffTarget>[] : [target];
  }

  @override
  Future<int> markAsRead({
    required String accessToken,
    required String notificationId,
  }) async {
    final error = markAsReadError;
    if (error != null) {
      throw error;
    }

    markedReadNotificationIds.add(notificationId);
    return 1;
  }
}

class _RecordingAppLifecycleGateway implements AppLifecycleGateway {
  int moveTaskToBackCallCount = 0;

  @override
  Future<void> moveTaskToBack() async {
    moveTaskToBackCallCount += 1;
  }
}

class _SeededProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 1,
      commentCount: 1,
      totalLikeCount: 3,
      postLikeCount: 1,
      commentLikeCount: 2,
    );
  }

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
      posts: [
        PublicProfilePostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          content: 'Tap through to the public native detail page.',
          categoryName: 'General',
          viewCount: 128,
          likeCount: 16,
          commentCount: 3,
          createTime: '2026-04-20T08:00:00Z',
        ),
      ],
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
      comments: [
        PublicProfileCommentSummary(
          id: 'reply-1',
          postId: 'post-42',
          content: 'Recent public comments should stay readable in the shell.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName: 'luobo',
          replyToCommentSnapshot: 'First public root comment',
        ),
      ],
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
      items: [
        UserQuickReplySummary(
          id: 'quick-42',
          postId: 'post-42',
          postTitle: '论坛详情回流',
          content: '这个回流很好用',
          createTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserBrowseHistoryPage(
      page: 1,
      pageSize: 20,
      total: 3,
      items: [
        UserBrowseHistoryItem(
          id: 'history-post-42',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '42',
          title: '论坛详情回流',
          routePath: '/forum/post/post-42',
          viewCount: 3,
          lastViewTime: '2026-04-20T09:20:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-docs-42',
          targetType: 'Wiki',
          targetTypeDisplay: '文档',
          targetId: '43',
          targetSlug: 'native-docs',
          title: 'Native docs',
          routePath: '/docs/native-docs',
          viewCount: 2,
          lastViewTime: '2026-04-20T09:10:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-product-42',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '1001',
          title: 'Early Access Badge',
          routePath: '/shop/products/1001',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw UnimplementedError();
  }
}

class _FakeSessionRefreshService extends SessionRefreshService {
  _FakeSessionRefreshService.missing()
      : _nextSession = null,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.success(AuthSession nextSession)
      : _nextSession = nextSession,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.failure(String failureMessage)
      : _nextSession = null,
        _failureMessage = failureMessage,
        super(environment: const AppEnvironment.development());

  final AuthSession? _nextSession;
  final String? _failureMessage;

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    final failureMessage = _failureMessage;
    if (failureMessage != null) {
      throw SessionRefreshException(failureMessage);
    }

    final nextSession = _nextSession;
    if (nextSession != null) {
      return nextSession;
    }

    return session;
  }
}

class _FakeAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  const _FakeAuthorizationCodeExchangeService({
    this.nextSession,
    this.failureMessage,
  });

  final AuthSession? nextSession;
  final String? failureMessage;

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
    required String codeVerifier,
  }) async {
    final failureMessage = this.failureMessage;
    if (failureMessage != null) {
      throw AuthorizationCodeExchangeException(failureMessage);
    }

    final nextSession = this.nextSession;
    if (nextSession == null) {
      throw const AuthorizationCodeExchangeException(
        'No fake authorization-code session was configured.',
      );
    }

    return nextSession;
  }
}

String _buildJwt({
  required String userId,
  required DateTime expiresAt,
}) {
  final header = base64Url.encode(utf8.encode('{"alg":"none","typ":"JWT"}'));
  final payload = base64Url.encode(
    utf8.encode(
      '{"sub":"$userId","exp":${expiresAt.toUtc().millisecondsSinceEpoch ~/ 1000}}',
    ),
  );
  return '$header.$payload.signature';
}
