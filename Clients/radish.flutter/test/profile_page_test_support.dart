part of 'profile_page_test.dart';

NativeAuthController _buildAuthController(
  SessionController sessionController, {
  InMemoryNativeAuthGateway? gateway,
  AuthSession? nextSession,
  String? exchangeFailureMessage,
}) {
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: gateway ?? InMemoryNativeAuthGateway(),
    exchangeService: _FakeAuthorizationCodeExchangeService(
      nextSession: nextSession,
      failureMessage: exchangeFailureMessage,
    ),
  );
}

const _longUserId =
    'user-2042219067430928384-extra-long-public-profile-identifier-for-narrow-screen';
const _longUserName =
    'radish_native_profile_reader_with_a_very_long_public_handle_for_narrow_screen';
const _longDisplayName =
    'Radish Native Profile Reader With A Very Long Display Name For Narrow Screens';
const _longDocsSlug =
    'flutter-native-profile-long-document-slug-that-should-not-break-the-narrow-profile-card-layout';
const _longDocsTitle =
    'Flutter native profile long document title that should stay constrained';
const _longPostId =
    'post-2042219067430928384-extra-long-public-id-for-profile-preview';
const _longCommentId =
    'comment-2042219067430928384-extra-long-public-id-for-profile-preview';
const _longRecentBrowseTitle =
    'A very long recent forum reading title that should stay inside the profile card';
const _longPostTitle =
    'A very long public post title that should stay readable without pushing the profile page wider';
const _longPostSummary =
    'This public post summary is intentionally long so the native profile card can prove it keeps text constrained on narrow screens.';
const _longCategoryName =
    'extremely-long-category-name-for-profile-preview-chip';
const _longCommentSnapshot =
    'A very long quoted comment snapshot that should be constrained inside the profile comment chip on narrow screens';

class _SuccessProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'luobo',
      displayName: 'Radish Author',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 12,
      commentCount: 28,
      totalLikeCount: 96,
      postLikeCount: 54,
      commentLikeCount: 42,
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
          id: 'post-1',
          publicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
          title: 'Native profile follow-up',
          summary: 'Expand the public profile beyond a single info card.',
          content: 'Expand the public profile beyond a single info card.',
          categoryName: 'Engineering',
          viewCount: 128,
          likeCount: 16,
          commentCount: 6,
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
          id: 'comment-1',
          postId: 'post-1',
          postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
          content: 'Recent public comments should stay readable in the shell.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName: 'radish',
          replyToCommentSnapshot: 'public profile preview',
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
          id: 'quick-1',
          postId: 'post-1',
          postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
          postTitle: 'Native profile follow-up',
          content: '这个原生回看入口不错',
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
          id: 'history-post-1',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '1001',
          title: 'Native profile follow-up',
          summary: 'Expand the public profile beyond a single info card.',
          routePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f801',
          viewCount: 2,
          lastViewTime: '2026-04-20T09:30:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-docs-1',
          targetType: 'Wiki',
          targetTypeDisplay: '文档',
          targetId: '1002',
          targetSlug: 'flutter-docs-scope',
          title: 'Radish Flutter docs scope',
          routePath: '/docs/flutter-docs-scope',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:20:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-product-1',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '1003',
          title: 'Early Access Badge',
          routePath: '/shop/products/1003',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) async {
    return const MyProfileInfo(
      userId: '2042219067430928384',
      userName: 'luobo',
      userEmail: 'luobo@example.com',
      sex: 0,
      age: 24,
      address: 'Radish base',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw UnimplementedError();
  }
}

class _EditableProfileRepository extends _SuccessProfileRepository {
  MyProfileInfo _profile = const MyProfileInfo(
    userId: '2042219067430928384',
    userName: 'luobo',
    userEmail: 'luobo@example.com',
    sex: 0,
    age: 24,
    address: 'Radish base',
    createTime: '2026-04-20T08:00:00Z',
  );

  UpdateMyProfileRequest? lastRequest;
  int updateCount = 0;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: _profile.userName,
      displayName: _profile.displayName,
      createTime: _profile.createTime,
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) async {
    return _profile;
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) async {
    updateCount += 1;
    lastRequest = request;
    _profile = MyProfileInfo(
      userId: _profile.userId,
      userName: request.userName,
      userEmail: request.userEmail,
      sex: request.sex ?? _profile.sex,
      age: request.age ?? 0,
      birth: request.birth ?? _profile.birth,
      address: request.address ?? '',
      createTime: _profile.createTime,
      avatarAttachmentId: _profile.avatarAttachmentId,
      avatarUrl: _profile.avatarUrl,
      avatarThumbnailUrl: _profile.avatarThumbnailUrl,
    );
  }
}

class _ProfileUpdateFailingRepository extends _EditableProfileRepository {
  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) async {
    updateCount += 1;
    lastRequest = request;
    throw const RadishApiClientException('用户名已被占用');
  }
}

class _LongTextProfileRepository extends _SuccessProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return const PublicProfileSummary(
      userId: _longUserId,
      userName: _longUserName,
      displayName: _longDisplayName,
      createTime: '2026-04-20T08:00:00Z',
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
          id: _longPostId,
          title: _longPostTitle,
          summary: _longPostSummary,
          content: _longPostSummary,
          categoryName: _longCategoryName,
          viewCount: 128,
          likeCount: 16,
          commentCount: 6,
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
          id: _longCommentId,
          postId: _longPostId,
          content:
              'This long public comment should stay inside the profile preview tile on narrow screens.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName:
              'reply_target_with_a_very_long_public_name_for_profile_preview',
          replyToCommentSnapshot: _longCommentSnapshot,
        ),
      ],
    );
  }
}

class _FailingProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('公开资料服务暂时不可用');
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw const RadishApiClientException('浏览记录服务暂时不可用');
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    throw const RadishApiClientException('个人资料服务暂时不可用');
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw const RadishApiClientException('个人资料服务暂时不可用');
  }
}

class _QuickReplyFailingProfileRepository extends _SuccessProfileRepository {
  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }
}

class _EmptyPublicActivityProfileRepository extends _SuccessProfileRepository {
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
      pageCount: 0,
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
      pageCount: 0,
      comments: [],
    );
  }
}

class _PendingRefreshProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;
  Completer<PublicProfileSummary>? refreshCompleter;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _RefreshFailingProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    throw const RadishApiClientException('公开资料刷新服务暂时不可用');
  }
}

class _FailThenRecoverProfileRepository extends _SuccessProfileRepository {
  int _profileCalls = 0;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    _profileCalls += 1;
    if (_profileCalls == 1) {
      return super.getPublicProfile(userId: userId);
    }

    if (_profileCalls == 2) {
      throw const RadishApiClientException('公开资料刷新服务暂时不可用');
    }

    return Future.value(_updatedProfileSummary(userId));
  }
}

PublicProfileSummary _updatedProfileSummary(String userId) {
  return PublicProfileSummary(
    userId: userId,
    userName: 'luobo',
    displayName: 'Updated Radish Author',
    createTime: '2026-04-20T08:00:00Z',
  );
}

class _PagedPostProfileRepository extends _SuccessProfileRepository {
  final List<int> postPages = <int>[];

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    postPages.add(pageIndex);

    if (pageIndex == 1) {
      return const PublicProfilePostPage(
        page: 1,
        pageSize: 3,
        dataCount: 4,
        pageCount: 2,
        posts: [
          PublicProfilePostSummary(
            id: 'post-page-1',
            title: '第一页公开帖子 1',
            summary: '第一页帖子摘要 1',
            content: '第一页帖子正文 1',
            categoryName: 'Engineering',
            viewCount: 11,
            likeCount: 1,
            commentCount: 2,
            createTime: '2026-04-20T08:00:00Z',
          ),
          PublicProfilePostSummary(
            id: 'post-page-2',
            title: '第一页公开帖子 2',
            summary: '第一页帖子摘要 2',
            content: '第一页帖子正文 2',
            categoryName: 'Community',
            viewCount: 12,
            likeCount: 2,
            commentCount: 3,
            createTime: '2026-04-20T08:02:00Z',
          ),
          PublicProfilePostSummary(
            id: 'post-page-3',
            title: '第一页公开帖子 3',
            summary: '第一页帖子摘要 3',
            content: '第一页帖子正文 3',
            categoryName: 'Design',
            viewCount: 13,
            likeCount: 3,
            commentCount: 4,
            createTime: '2026-04-20T08:04:00Z',
          ),
        ],
      );
    }

    return const PublicProfilePostPage(
      page: 2,
      pageSize: 3,
      dataCount: 4,
      pageCount: 2,
      posts: [
        PublicProfilePostSummary(
          id: 'post-page-4',
          publicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f804',
          title: '第四篇公开帖子',
          summary: '第四篇公开帖子摘要',
          content: '第四篇公开帖子正文',
          categoryName: 'Flutter',
          viewCount: 14,
          likeCount: 4,
          commentCount: 5,
          createTime: '2026-04-20T08:06:00Z',
        ),
      ],
    );
  }
}

class _PagedPostLoadMoreFailingProfileRepository
    extends _PagedPostProfileRepository {
  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex == 1) {
      return super.getPublicPosts(
        userId: userId,
        pageIndex: pageIndex,
        pageSize: pageSize,
      );
    }

    throw const RadishApiClientException('加载更多公开帖子失败');
  }
}

class _PagedCommentProfileRepository extends _SuccessProfileRepository {
  final List<int> commentPages = <int>[];

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    commentPages.add(pageIndex);

    if (pageIndex == 1) {
      return const PublicProfileCommentPage(
        page: 1,
        pageSize: 3,
        dataCount: 4,
        pageCount: 2,
        comments: [
          PublicProfileCommentSummary(
            id: 'comment-page-1',
            postId: 'post-1',
            content: '第一页公开评论 1',
            likeCount: 1,
            createTime: '2026-04-20T09:00:00Z',
          ),
          PublicProfileCommentSummary(
            id: 'comment-page-2',
            postId: 'post-2',
            content: '第一页公开评论 2',
            likeCount: 2,
            createTime: '2026-04-20T09:02:00Z',
          ),
          PublicProfileCommentSummary(
            id: 'comment-page-3',
            postId: 'post-3',
            content: '第一页公开评论 3',
            likeCount: 3,
            createTime: '2026-04-20T09:04:00Z',
          ),
        ],
      );
    }

    return const PublicProfileCommentPage(
      page: 2,
      pageSize: 3,
      dataCount: 4,
      pageCount: 2,
      comments: [
        PublicProfileCommentSummary(
          id: 'comment-page-4',
          postId: 'post-4',
          postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f814',
          content: '第四条公开评论上下文',
          likeCount: 4,
          createTime: '2026-04-20T09:06:00Z',
        ),
      ],
    );
  }
}

class _PagedCommentLoadMoreFailingProfileRepository
    extends _PagedCommentProfileRepository {
  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    if (pageIndex == 1) {
      return super.getPublicComments(
        userId: userId,
        pageIndex: pageIndex,
        pageSize: pageSize,
      );
    }

    throw const RadishApiClientException('加载更多公开评论失败');
  }
}

class _PagedQuickReplyProfileRepository extends _SuccessProfileRepository {
  final List<int> quickReplyPages = <int>[];

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    quickReplyPages.add(pageIndex);

    if (pageIndex == 1) {
      return const UserQuickReplyPage(
        page: 1,
        pageSize: 3,
        total: 4,
        items: [
          UserQuickReplySummary(
            id: 'quick-page-1',
            postId: 'post-1',
            postTitle: 'Native profile follow-up',
            content: '第一页轻回应 1',
            createTime: '2026-04-20T09:10:00Z',
          ),
          UserQuickReplySummary(
            id: 'quick-page-2',
            postId: 'post-2',
            postTitle: 'Second native follow-up',
            content: '第一页轻回应 2',
            createTime: '2026-04-20T09:12:00Z',
          ),
          UserQuickReplySummary(
            id: 'quick-page-3',
            postId: 'post-3',
            postTitle: 'Third native follow-up',
            content: '第一页轻回应 3',
            createTime: '2026-04-20T09:14:00Z',
          ),
        ],
      );
    }

    return const UserQuickReplyPage(
      page: 2,
      pageSize: 3,
      total: 4,
      items: [
        UserQuickReplySummary(
          id: 'quick-page-4',
          postId: 'post-4',
          postTitle: 'Fourth native follow-up',
          content: '第四条回看上下文',
          createTime: '2026-04-20T09:16:00Z',
        ),
      ],
    );
  }
}

class _PagedQuickReplyLoadMoreFailingProfileRepository
    extends _PagedQuickReplyProfileRepository {
  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    if (pageIndex == 1) {
      return super.getMyQuickReplies(
        pageIndex: pageIndex,
        pageSize: pageSize,
        accessToken: accessToken,
      );
    }

    throw const RadishApiClientException('加载更多轻回应失败');
  }
}

class _NoopSessionRefreshService extends SessionRefreshService {
  _NoopSessionRefreshService()
      : super(environment: const AppEnvironment.development());

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    return session;
  }
}

class _ClipboardRecorder {
  String? text;

  void install() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'Clipboard.setData') {
        final arguments = Map<Object?, Object?>.from(call.arguments as Map);
        text = arguments['text'] as String?;
      }

      return null;
    });
  }

  void reset() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, null);
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
