part of 'forum_detail_page_test.dart';

class _PagedForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    if (pageIndex == 1) {
      return ForumCommentPage.fromJson({
        'voPageIndex': 1,
        'voPageSize': 2,
        'voTotal': 3,
        'voItems': const [
          {
            'voId': 'comment-1',
            'voPostId': 'post-42',
            'voContent': 'Root comment one',
            'voAuthorId': 'user-1',
            'voAuthorName': 'radish',
            'voLikeCount': 3,
            'voReplyCount': 1,
            'voIsSofa': true,
            'voCreateTime': '2026-04-20T08:05:00Z',
            'voChildrenTotal': 2,
          },
          {
            'voId': 'comment-2',
            'voPostId': 'post-42',
            'voContent': 'Root comment two',
            'voAuthorId': 'user-2',
            'voAuthorName': 'guest',
            'voLikeCount': 1,
            'voReplyToUserName': 'luobo',
            'voReplyToCommentSnapshot': 'Original point from the author.',
            'voCreateTime': '2026-04-20T08:08:00Z',
          },
        ],
      });
    }

    return ForumCommentPage.fromJson({
      'voPageIndex': 2,
      'voPageSize': 2,
      'voTotal': 3,
      'voItems': const [
        {
          'voId': 'comment-3',
          'voPostId': 'post-42',
          'voContent': 'Root comment three',
          'voAuthorId': 'user-3',
          'voAuthorName': 'reader',
          'voLikeCount': 0,
          'voCreateTime': '2026-04-20T08:10:00Z',
        },
      ],
    });
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (parentId != 'comment-1') {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 0,
        comments: [],
      );
    }

    if (pageIndex == 1) {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 2,
        comments: [
          ForumCommentSummary(
            id: 'reply-1',
            postId: 'post-42',
            content: 'Child comment one',
            authorId: 'user-4',
            authorName: 'child-a',
            parentId: 'comment-1',
            rootId: 'comment-1',
            replyToUserName: 'radish',
            replyToCommentSnapshot: 'Root comment one',
            createTime: '2026-04-20T08:06:00Z',
          ),
        ],
      );
    }

    return const ForumChildCommentPage(
      pageIndex: 2,
      pageSize: 5,
      totalCount: 2,
      comments: [
        ForumCommentSummary(
          id: 'reply-2',
          postId: 'post-42',
          content: 'Child comment two',
          authorId: 'user-5',
          authorName: 'child-b',
          parentId: 'comment-1',
          rootId: 'comment-1',
          createTime: '2026-04-20T08:07:00Z',
        ),
      ],
    );
  }
}

class _LongContentForumRepository extends _PagedForumRepository {
  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    final detail = await super.getPostDetail(postId: postId);
    final paragraphs = List<String>.generate(
      80,
      (index) => '长正文段落 $index：用于验证 compact 连续阅读、自动换行与长页面滚动。',
    ).join('\n\n');
    return detail.copyWith(content: '# 长正文\n\n$paragraphs');
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return ForumCommentPage(
      page: 1,
      pageSize: 10,
      dataCount: 1,
      pageCount: 1,
      comments: [
        ForumCommentSummary(
          id: 'comment-long',
          postId: postId,
          content: '${List<String>.filled(24, '这是一段需要自然换行的长评论。').join()}长评论结尾',
          authorId: 'user-long',
          authorName: 'long-reader',
          createTime: '2026-08-23T08:00:00Z',
        ),
      ],
    );
  }
}

class _PublicIdForumRepository extends _PagedForumRepository {
  String? lastRootCommentsPostId;
  String? lastQuickReplyPostId;

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return const ForumPostDetail(
      id: '2042219761177198592',
      publicId: 'pst_01HZPUBLICROUTEID',
      title: '测试问答帖子',
      summary: '测试测试',
      content: '# 测试问答帖子\n\n测试测试',
      contentType: 'markdown',
      categoryId: 'category-1',
      categoryName: '生活随笔',
      authorId: 'user-9',
      authorName: 'test',
      isQuestion: true,
      isSolved: true,
      commentCount: 3,
      createTime: '2026-04-09T12:35:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    lastRootCommentsPostId = postId;
    return super.getRootCommentsPage(
      postId: postId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      sortBy: sortBy,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) {
    lastQuickReplyPostId = postId;
    return super.getQuickReplyWall(postId: postId, take: take);
  }
}

class _MissingMetadataForumRepository extends _PagedForumRepository {
  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: '缺省元数据帖子',
      content: '正文',
      contentType: 'Markdown',
      categoryId: '2042219067430928400',
      authorId: '2042219067430928399',
      commentCount: 0,
      createTime: '2026-04-20T08:00:00Z',
    );
  }
}

class _CountingQuickReplyForumRepository extends _PagedForumRepository {
  int detailRequests = 0;
  int rootCommentRequests = 0;
  int createQuickReplyRequests = 0;

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    detailRequests += 1;
    return super.getPostDetail(postId: postId);
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    rootCommentRequests += 1;
    return super.getRootCommentsPage(
      postId: postId,
      pageIndex: pageIndex,
      pageSize: pageSize,
      sortBy: sortBy,
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    createQuickReplyRequests += 1;
    return super.createQuickReply(
      postId: postId,
      content: content,
      accessToken: accessToken,
    );
  }
}

class _QuickReplySubmitFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    throw const RadishApiClientException('轻回应服务暂时不可用');
  }
}

class _QuestionAnswerForumRepository extends _PagedForumRepository {
  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: '问答详情',
      content: '# Question\n\nHow should Flutter answer a forum question?',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      answerCount: 2,
      isQuestion: true,
      isSolved: true,
      createTime: '2026-04-20T08:00:00Z',
      question: ForumQuestionDetail(
        postId: postId,
        isSolved: true,
        acceptedAnswerId: 'answer-1',
        answerCount: 2,
        answers: [
          ForumAnswerSummary(
            id: 'answer-1',
            postId: postId,
            authorId: 'user-1',
            authorName: 'radish',
            content: 'Accepted answer content',
            isAccepted: true,
            createTime: '2026-04-20T08:05:00Z',
          ),
          ForumAnswerSummary(
            id: 'answer-2',
            postId: postId,
            authorId: 'user-2',
            authorName: 'guest',
            content: 'Second answer content',
            createTime: '2026-04-20T08:08:00Z',
          ),
        ],
      ),
    );
  }
}

class _RecordingAnswerForumRepository extends _QuestionAnswerForumRepository {
  final List<_CreateAnswerRequest> answerRequests = <_CreateAnswerRequest>[];

  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    answerRequests.add(
      _CreateAnswerRequest(
        postId: postId,
        content: content,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );

    return ForumQuestionDetail(
      postId: postId,
      isSolved: true,
      acceptedAnswerId: 'answer-1',
      answerCount: 3,
      answers: [
        ForumAnswerSummary(
          id: 'answer-1',
          postId: postId,
          authorId: 'user-1',
          authorName: 'radish',
          content: 'Accepted answer content',
          isAccepted: true,
          createTime: '2026-04-20T08:05:00Z',
        ),
        ForumAnswerSummary(
          id: 'answer-2',
          postId: postId,
          authorId: 'user-2',
          authorName: 'guest',
          content: 'Second answer content',
          createTime: '2026-04-20T08:08:00Z',
        ),
        ForumAnswerSummary(
          id: 'answer-created',
          postId: postId,
          authorId: 'user-current',
          authorName: 'current',
          content: content,
          createTime: '2026-04-20T08:14:00Z',
        ),
      ],
    );
  }
}

class _AnswerSubmitFailingForumRepository
    extends _RecordingAnswerForumRepository {
  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    answerRequests.add(
      _CreateAnswerRequest(
        postId: postId,
        content: content,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
    throw const RadishApiClientException('回答服务暂时不可用');
  }
}

class _RecordingCommentForumRepository extends _PagedForumRepository {
  final List<_CreateCommentRequest> createCommentRequests =
      <_CreateCommentRequest>[];

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
    createCommentRequests.add(
      _CreateCommentRequest(
        postId: postId,
        content: content,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
        parentId: parentId,
        replyToCommentId: replyToCommentId,
        replyToCommentSnapshot: replyToCommentSnapshot,
        replyToUserName: replyToUserName,
      ),
    );
    return parentId == null ? 'comment-created-root' : 'comment-created-reply';
  }
}

class _CommentSubmitFailingForumRepository
    extends _RecordingCommentForumRepository {
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
    createCommentRequests.add(
      _CreateCommentRequest(
        postId: postId,
        content: content,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
        parentId: parentId,
        replyToCommentId: replyToCommentId,
        replyToCommentSnapshot: replyToCommentSnapshot,
        replyToUserName: replyToUserName,
      ),
    );
    throw const RadishApiClientException('评论服务暂时不可用');
  }
}

class _RecordingForumEditRepository extends _PagedForumRepository {
  final List<_UpdatePostRequest> updatePostRequests = <_UpdatePostRequest>[];
  final List<_UpdateCommentRequest> updateCommentRequests =
      <_UpdateCommentRequest>[];

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      title: '论坛详情回流',
      summary: 'Tap through to the public native detail page.',
      content: '# Native detail\n\nForum detail body.',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      tagNames: const ['flutter'],
      commentCount: 3,
      createTime: '2026-04-20T08:00:00Z',
      updateTime: '2026-04-20T10:30:00Z',
    );
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
    updatePostRequests.add(
      _UpdatePostRequest(
        postId: postId,
        title: title,
        content: content,
        categoryId: categoryId,
        tagNames: tagNames,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
  }

  @override
  Future<void> updateComment({
    required String commentId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    updateCommentRequests.add(
      _UpdateCommentRequest(
        commentId: commentId,
        content: content,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
  }
}

class _PostEditFailingForumRepository extends _RecordingForumEditRepository {
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
    await super.updatePost(
      postId: postId,
      title: title,
      content: content,
      categoryId: categoryId,
      tagNames: tagNames,
      accessToken: accessToken,
      clientSubmissionId: clientSubmissionId,
    );
    throw const RadishApiClientException('帖子编辑服务暂时不可用');
  }
}

class _CommentEditFailingForumRepository extends _RecordingForumEditRepository {
  @override
  Future<void> updateComment({
    required String commentId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) async {
    await super.updateComment(
      commentId: commentId,
      content: content,
      accessToken: accessToken,
      clientSubmissionId: clientSubmissionId,
    );
    throw const RadishApiClientException('评论编辑服务暂时不可用');
  }
}

class _CreateAnswerRequest {
  const _CreateAnswerRequest({
    required this.postId,
    required this.content,
    required this.accessToken,
    required this.clientSubmissionId,
  });

  final String postId;
  final String content;
  final String accessToken;
  final String clientSubmissionId;
}

class _CreateCommentRequest {
  const _CreateCommentRequest({
    required this.postId,
    required this.content,
    required this.accessToken,
    required this.clientSubmissionId,
    required this.parentId,
    required this.replyToCommentId,
    required this.replyToCommentSnapshot,
    required this.replyToUserName,
  });

  final String postId;
  final String content;
  final String accessToken;
  final String clientSubmissionId;
  final String? parentId;
  final String? replyToCommentId;
  final String? replyToCommentSnapshot;
  final String? replyToUserName;
}

class _UpdatePostRequest {
  const _UpdatePostRequest({
    required this.postId,
    required this.title,
    required this.content,
    required this.categoryId,
    required this.tagNames,
    required this.accessToken,
    required this.clientSubmissionId,
  });

  final String postId;
  final String title;
  final String content;
  final String categoryId;
  final List<String> tagNames;
  final String accessToken;
  final String clientSubmissionId;
}

class _UpdateCommentRequest {
  const _UpdateCommentRequest({
    required this.commentId,
    required this.content,
    required this.accessToken,
    required this.clientSubmissionId,
  });

  final String commentId;
  final String content;
  final String accessToken;
  final String clientSubmissionId;
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

class _UnusedAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  const _UnusedAuthorizationCodeExchangeService();

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
    required String codeVerifier,
  }) {
    throw UnimplementedError();
  }
}

class _DetailFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    throw const RadishApiClientException('详情服务暂时不可用');
  }
}

class _CommentNavigationFailingForumRepository extends _PagedForumRepository {
  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) {
    throw const RadishApiClientException('评论定位服务暂时不可用');
  }
}

class _CommentFailingForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    throw const RadishApiClientException('评论服务暂时不可用');
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('回复服务暂时不可用');
  }
}

class _EmptyCommentForumRepository extends _BaseForumRepository {
  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return ForumCommentPage.fromJson({
      'voPageIndex': 1,
      'voPageSize': pageSize,
      'voTotal': 0,
      'voItems': const [],
    });
  }
}
