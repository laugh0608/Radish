part of 'forum_page_test.dart';

class _SuccessForumRepository implements ForumRepository {
  final List<_CreatePostRequest> createPostRequests = <_CreatePostRequest>[];

  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    return const [
      ForumCategorySummary(
        id: '9',
        name: 'Engineering',
        slug: 'engineering',
      ),
    ];
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return ForumPostPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: 1,
      pageCount: 1,
      posts: const [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'How to wire Radish Flutter forum reading',
          summary:
              'Use the public read-only feed contract first, then expand into detail.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'Luobo',
          viewCount: 256,
          likeCount: 18,
          commentCount: 42,
          answerCount: 3,
          isTop: true,
          isQuestion: true,
          createTime: '2026-04-18T10:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: 'How to wire Radish Flutter forum reading',
      summary:
          'Use the public read-only feed contract first, then expand into detail.',
      content: '# Detail\n\nForum detail body.',
      contentType: 'Markdown',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: '1024',
      authorName: 'Luobo',
      commentCount: 42,
      answerCount: 3,
      isTop: true,
      isQuestion: true,
      createTime: '2026-04-18T10:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      comments: [
        ForumCommentSummary(
          id: 'comment-2048',
          postId: '2042219067430928384',
          content: 'Pinned target comment for handoff verification.',
          authorId: '1025',
          authorName: 'Reader',
          createTime: '2026-04-18T12:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 1,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: '2042219067430928384',
          authorId: '1025',
          authorName: 'Reader',
          content: '学到了',
          createTime: '2026-04-18T12:10:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'current-user',
      authorName: 'current',
      content: content,
      createTime: '2026-04-18T12:15:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }

  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    return ForumQuestionDetail(
      postId: postId,
      isSolved: false,
      answerCount: 1,
      answers: [
        ForumAnswerSummary(
          id: 'answer-created',
          postId: postId,
          authorId: 'current-user',
          authorName: 'current',
          content: content,
          createTime: '2026-04-18T12:16:00Z',
        ),
      ],
    );
  }

  @override
  Future<String> createPost({
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    createPostRequests.add(
      _CreatePostRequest(
        title: title,
        content: content,
        categoryId: categoryId,
        tagNames: tagNames,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
    return 'post-created-1';
  }

  @override
  Future<void> updatePost({
    required String postId,
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {}

  @override
  Future<void> updateComment({
    required String commentId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {}
}

class _CreatedPostPublicRouteForumRepository extends _SuccessForumRepository {
  String? lastDetailPostId;

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    lastDetailPostId = postId;
    return const ForumPostDetail(
      id: 'post-created-1',
      publicId: 'pst_created_flutter',
      title: 'Flutter 新帖公开链路',
      content: '发布后应打开带 PublicId 的公开详情。',
      contentType: 'text',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: 'user-42',
      authorName: '我',
      tagNames: ['flutter', '链路'],
      createTime: '2026-06-04T08:00:00Z',
    );
  }
}

class _CreatePostFailingForumRepository extends _SuccessForumRepository {
  @override
  Future<String> createPost({
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    createPostRequests.add(
      _CreatePostRequest(
        title: title,
        content: content,
        categoryId: categoryId,
        tagNames: tagNames,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
    throw const RadishApiClientException('发帖服务暂时不可用');
  }
}

class _FailingForumRepository implements ForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    throw const RadishApiClientException('分类服务暂时不可用');
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    throw const RadishApiClientException('论坛服务暂时不可用');
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    throw const RadishApiClientException('帖子详情服务暂时不可用');
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    throw const RadishApiClientException('评论服务暂时不可用');
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    throw const RadishApiClientException('回复服务暂时不可用');
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    throw const RadishApiClientException('评论定位服务暂时不可用');
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    throw const RadishApiClientException('轻回应发布服务暂时不可用');
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    throw const RadishApiClientException('评论发布服务暂时不可用');
  }

  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    throw const RadishApiClientException('回答发布服务暂时不可用');
  }

  @override
  Future<String> createPost({
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    throw const RadishApiClientException('发帖服务暂时不可用');
  }

  @override
  Future<void> updatePost({
    required String postId,
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    throw const RadishApiClientException('帖子编辑服务暂时不可用');
  }

  @override
  Future<void> updateComment({
    required String commentId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    throw const RadishApiClientException('评论编辑服务暂时不可用');
  }
}

class _CreatePostRequest {
  const _CreatePostRequest({
    required this.title,
    required this.content,
    required this.categoryId,
    required this.tagNames,
    required this.accessToken,
    required this.clientSubmissionId,
  });

  final String title;
  final String content;
  final String categoryId;
  final List<String> tagNames;
  final String accessToken;
  final String clientSubmissionId;
}

class _RecordingForumApiClient implements RadishApiClient {
  Uri? lastUri;
  Object? lastBody;
  String? lastBearerToken;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBearerToken = bearerToken;

    if (uri.path == '/api/v1/Post/GetList') {
      return decode({
        'page': 2,
        'pageSize': 20,
        'dataCount': 1,
        'pageCount': 2,
        'data': const [
          {
            'voId': '9223372036854775806',
            'voTitle': 'LongId mapping',
            'voCategoryId': '9223372036854775805',
            'voAuthorId': '9223372036854775804',
          },
        ],
      });
    }

    throw UnimplementedError('Unexpected GET target: ${uri.path}');
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBody = body;
    lastBearerToken = bearerToken;

    if (uri.path == '/api/v1/Question/Answer') {
      return decode({
        'voPostId': 'post-42',
        'voIsSolved': false,
        'voAnswerCount': 1,
        'voAnswers': const [
          {
            'voAnswerId': 'answer-1',
            'voPostId': 'post-42',
            'voAuthorId': 'user-1',
            'voAuthorName': 'radish',
            'voContent': '回答内容',
            'voIsAccepted': false,
            'voCreateTime': '2026-04-20T08:14:00Z',
          },
        ],
      });
    }

    return decode('created-id');
  }

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBody = body;
    lastBearerToken = bearerToken;

    return decode(null);
  }
}

class _NoopSessionRefreshService extends SessionRefreshService {
  const _NoopSessionRefreshService()
      : super(environment: const AppEnvironment.development());

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    return session;
  }
}

class _PendingRefreshForumRepository extends _SuccessForumRepository {
  int _calls = 0;
  Completer<ForumPostPage>? refreshCompleter;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    final completer = refreshCompleter;
    if (completer == null) {
      throw StateError('Missing refresh completer.');
    }

    return completer.future;
  }
}

class _RefreshFailingForumRepository extends _SuccessForumRepository {
  int _calls = 0;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    throw const RadishApiClientException('论坛刷新服务暂时不可用');
  }
}

class _FailThenRecoverForumRepository extends _SuccessForumRepository {
  int _calls = 0;

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    _calls += 1;
    if (_calls == 1) {
      return super.getPostPage(
        pageIndex: pageIndex,
        pageSize: pageSize,
        sort: sort,
      );
    }

    if (_calls == 2) {
      throw const RadishApiClientException('论坛刷新服务暂时不可用');
    }

    return Future.value(_updatedForumPostPage());
  }
}

class _PendingInitialForumRepository extends _SuccessForumRepository {
  final Completer<ForumPostPage> pageCompleter = Completer<ForumPostPage>();

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    return pageCompleter.future;
  }
}

class _EmptyForumRepository extends _SuccessForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return ForumPostPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: 0,
      pageCount: 0,
      posts: const [],
    );
  }
}

class _CategoryFailingForumRepository extends _SuccessForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() {
    throw const RadishApiClientException(
      '论坛分类服务暂时不可用',
      statusCode: 503,
      code: 'Forum.CategoryUnavailable',
    );
  }
}

class _LongForumRepository extends _SuccessForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return ForumPostPage(
      page: 1,
      pageSize: pageSize,
      dataCount: 1,
      pageCount: 1,
      posts: const [
        ForumPostSummary(
          id: 'long-post-id',
          title: '这是一个用于验证紧凑断点标题边界的非常长非常长非常长非常长非常长的公开论坛帖子标题',
          summary: '这是一段用于验证连续信息流摘要边界的很长内容。它不应撑破主轴，也不应把原生页面变成卡片网格。'
              '这是一段用于验证连续信息流摘要边界的很长内容。它不应撑破主轴，也不应把原生页面变成卡片网格。',
          categoryId: '9223372036854775806',
          categoryName: '一个同样很长但必须保持单行省略的论坛分类名称',
          authorId: '9223372036854775805',
          authorName: '一个同样很长但必须保持可读边界的作者显示名称',
          viewCount: 9223372036854775807,
          likeCount: 9223372036854775807,
          commentCount: 9223372036854775807,
          createTime: '2026-08-23T08:00:00Z',
        ),
      ],
    );
  }
}

ForumPostPage _updatedForumPostPage() {
  return const ForumPostPage(
    page: 1,
    pageSize: 20,
    dataCount: 1,
    pageCount: 1,
    posts: [
      ForumPostSummary(
        id: '2042219067430928385',
        title: 'Updated forum refresh summary',
        summary: 'Updated public forum summary after refresh.',
        categoryId: '9',
        categoryName: 'Engineering',
        authorId: '1024',
        authorName: 'Luobo',
        viewCount: 512,
        likeCount: 20,
        commentCount: 48,
        createTime: '2026-04-18T11:00:00Z',
      ),
    ],
  );
}
