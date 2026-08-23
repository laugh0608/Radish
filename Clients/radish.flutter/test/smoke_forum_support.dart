part of 'smoke_test.dart';

class _FakeForumRepository implements ForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    return const [
      ForumCategorySummary(
        id: 'category-1',
        name: 'General',
      ),
    ];
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: 'Post $postId',
      summary: 'Summary for $postId',
      content: '# Post $postId\n\nBody content',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-1',
      authorName: 'tester',
      tagNames: const ['flutter'],
      createTime: '2026-04-20T08:00:00Z',
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
      dataCount: 0,
      pageCount: 0,
      comments: [],
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
      total: 0,
      items: [],
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
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T08:13:00Z',
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
          authorId: 'user-current',
          authorName: 'current',
          content: content,
          createTime: '2026-04-20T08:14:00Z',
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
    return 'post-created';
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

class _SeededForumRepository implements ForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    return const [
      ForumCategorySummary(
        id: 'category-1',
        name: 'General',
      ),
    ];
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          categoryId: 'category-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
          commentCount: 3,
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
      title: '论坛详情回流',
      summary: 'Tap through to the public native detail page.',
      content:
          '# Native detail\n\n- author metadata\n- body content\n- back navigation',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      tagNames: const ['android', 'flutter'],
      commentCount: 3,
      createTime: '2026-04-20T08:00:00Z',
      updateTime: '2026-04-20T10:30:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    if (pageIndex == 1) {
      return const ForumCommentPage(
        page: 1,
        pageSize: 20,
        dataCount: 2,
        pageCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'comment-1',
            postId: 'post-42',
            content: 'First public root comment',
            authorId: 'user-9',
            authorName: 'luobo',
            likeCount: 2,
            replyCount: 1,
            childrenTotal: 1,
            createTime: '2026-04-20T11:00:00Z',
          ),
          ForumCommentSummary(
            id: 'comment-2',
            postId: 'post-42',
            content: 'Second public root comment',
            authorId: 'user-10',
            authorName: 'reader',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:05:00Z',
          ),
        ],
      );
    }

    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      comments: [],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (parentId == 'comment-1') {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'reply-1',
            postId: 'post-42',
            content: 'First public child comment',
            authorId: 'user-11',
            authorName: 'guest-child',
            parentId: 'comment-1',
            rootId: 'comment-1',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:10:00Z',
          ),
        ],
      );
    }

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
    if (commentId == 'reply-1') {
      return const ForumCommentNavigationLocation(
        commentId: 'reply-1',
        postId: 'post-42',
        rootCommentId: 'comment-1',
        parentCommentId: 'comment-1',
        isRootComment: false,
        rootPageIndex: 1,
        childPageIndex: 1,
      );
    }

    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: 'comment-1',
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
      total: 2,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: 'post-42',
          authorId: 'user-9',
          authorName: 'luobo',
          content: '学到了',
          createTime: '2026-04-20T11:20:00Z',
        ),
        ForumQuickReplySummary(
          id: 'quick-2',
          postId: 'post-42',
          authorId: 'user-10',
          authorName: 'reader',
          content: '同感',
          createTime: '2026-04-20T11:25:00Z',
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
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T11:30:00Z',
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
          authorId: 'user-current',
          authorName: 'current',
          content: content,
          createTime: '2026-04-20T11:31:00Z',
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
    return 'post-created';
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

class _RecordingPostForumRepository extends _SeededForumRepository {
  final List<_ForumCreatePostRequest> createPostRequests =
      <_ForumCreatePostRequest>[];

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
      _ForumCreatePostRequest(
        title: title,
        content: content,
        categoryId: categoryId,
        tagNames: tagNames,
        accessToken: accessToken,
        clientSubmissionId: clientSubmissionId,
      ),
    );
    return 'post-created';
  }
}

class _ForumCreatePostRequest {
  const _ForumCreatePostRequest({
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

class _SeededBigIdForumRepository implements ForumRepository {
  @override
  Future<List<ForumCategorySummary>> getTopCategories() async {
    return const [
      ForumCategorySummary(
        id: '9',
        name: 'Engineering',
      ),
    ];
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Connect real summaries without expanding into details.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'luobo',
          commentCount: 6,
          viewCount: 128,
          isEssence: true,
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
      title: 'Native discover wiring plan',
      summary: 'Connect real summaries without expanding into details.',
      content: '# Big id detail\n\nPreserve string ids through native handoff.',
      contentType: 'Markdown',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: '1024',
      authorName: 'luobo',
      commentCount: 6,
      viewCount: 128,
      isEssence: true,
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
          id: 'comment-big-1',
          postId: '2042219067430928384',
          content: 'Big id root comment',
          authorId: '2048',
          authorName: 'reader',
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
          id: 'quick-big-1',
          postId: '2042219067430928384',
          authorId: '2048',
          authorName: 'reader',
          content: '已读',
          createTime: '2026-04-18T12:05:00Z',
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
      id: 'quick-big-created',
      postId: postId,
      authorId: 'current-user',
      authorName: 'current',
      content: content,
      createTime: '2026-04-18T12:20:00Z',
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
          id: 'answer-big-created',
          postId: postId,
          authorId: 'current-user',
          authorName: 'current',
          content: content,
          createTime: '2026-04-18T12:21:00Z',
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
    return '2042219067430928399';
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
