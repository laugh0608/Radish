part of 'forum_page_test.dart';

void registerForumRepositoryTests() {
  test('http forum repository sends post client submission id', () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final postId = await repository.createPost(
      title: 'Flutter 发帖',
      content: '正文',
      categoryId: '9',
      tagNames: const ['flutter', '发布'],
      accessToken: 'access-token',
      clientSubmissionId: 'forum-post:test-key',
    );

    expect(postId, 'created-id');
    expect(apiClient.lastUri?.path, '/api/v1/Post/Publish');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'title': 'Flutter 发帖',
      'content': '正文',
      'clientSubmissionId': 'forum-post:test-key',
      'contentType': 'text',
      'categoryId': '9',
      'tagNames': ['flutter', '发布'],
      'isQuestion': false,
    });
  });
  test('http forum repository sends comment client submission id', () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final commentId = await repository.createComment(
      postId: 'post-42',
      content: '评论内容',
      accessToken: 'access-token',
      clientSubmissionId: 'forum-comment:test-key',
      parentId: 'comment-1',
      replyToCommentId: 'reply-1',
      replyToCommentSnapshot: '被回复内容',
      replyToUserName: 'radish',
    );

    expect(commentId, 'created-id');
    expect(apiClient.lastUri?.path, '/api/v1/Comment/Create');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'postId': 'post-42',
      'content': '评论内容',
      'clientSubmissionId': 'forum-comment:test-key',
      'parentId': 'comment-1',
      'replyToCommentId': 'reply-1',
      'replyToCommentSnapshot': '被回复内容',
      'replyToUserName': 'radish',
    });
  });
  test('http forum repository sends answer client submission id', () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final question = await repository.answerQuestion(
      postId: 'post-42',
      content: '回答内容',
      accessToken: 'access-token',
      clientSubmissionId: 'forum-answer:test-key',
    );

    expect(question.postId, 'post-42');
    expect(question.answerCount, 1);
    expect(apiClient.lastUri?.path, '/api/v1/Question/Answer');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'postId': 'post-42',
      'content': '回答内容',
      'clientSubmissionId': 'forum-answer:test-key',
    });
  });
  test('http forum repository sends post edit client submission id', () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    await repository.updatePost(
      postId: 'post-42',
      title: '原帖子标题',
      content: '编辑后的正文',
      categoryId: '9',
      tagNames: const ['flutter', '编辑'],
      accessToken: 'access-token',
      clientSubmissionId: 'forum-post-edit:test-key',
    );

    expect(apiClient.lastUri?.path, '/api/v1/Post/Update');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'postId': 'post-42',
      'title': '原帖子标题',
      'content': '编辑后的正文',
      'categoryId': '9',
      'tagNames': ['flutter', '编辑'],
      'clientSubmissionId': 'forum-post-edit:test-key',
    });
  });
  test('http forum repository sends comment edit client submission id',
      () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    await repository.updateComment(
      commentId: 'comment-42',
      content: '编辑后的评论',
      accessToken: 'access-token',
      clientSubmissionId: 'forum-comment-edit:test-key',
    );

    expect(apiClient.lastUri?.path, '/api/v1/Comment/Update');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'commentId': 'comment-42',
      'content': '编辑后的评论',
      'clientSubmissionId': 'forum-comment-edit:test-key',
    });
  });
  test('http forum repository rejects blank client submission id', () async {
    final repository = HttpForumRepository(
      apiClient: _RecordingForumApiClient(),
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    expect(
      () => repository.createPost(
        title: 'Flutter 发帖',
        content: '正文',
        categoryId: '9',
        tagNames: const ['flutter'],
        accessToken: 'access-token',
        clientSubmissionId: ' ',
      ),
      throwsA(
        isA<RadishApiClientException>().having(
          (error) => error.message,
          'message',
          '发帖请求缺少提交意图 ID',
        ),
      ),
    );

    expect(
      () => repository.createComment(
        postId: 'post-42',
        content: '评论内容',
        accessToken: 'access-token',
        clientSubmissionId: ' ',
      ),
      throwsA(
        isA<RadishApiClientException>().having(
          (error) => error.message,
          'message',
          '评论请求缺少提交意图 ID',
        ),
      ),
    );

    expect(
      () => repository.answerQuestion(
        postId: 'post-42',
        content: '回答内容',
        accessToken: 'access-token',
        clientSubmissionId: ' ',
      ),
      throwsA(
        isA<RadishApiClientException>().having(
          (error) => error.message,
          'message',
          '回答请求缺少提交意图 ID',
        ),
      ),
    );

    expect(
      () => repository.updatePost(
        postId: 'post-42',
        title: 'Flutter 发帖',
        content: '正文',
        categoryId: '9',
        tagNames: const ['flutter'],
        accessToken: 'access-token',
        clientSubmissionId: ' ',
      ),
      throwsA(
        isA<RadishApiClientException>().having(
          (error) => error.message,
          'message',
          '帖子编辑请求缺少提交意图 ID',
        ),
      ),
    );

    expect(
      () => repository.updateComment(
        commentId: 'comment-42',
        content: '评论内容',
        accessToken: 'access-token',
        clientSubmissionId: ' ',
      ),
      throwsA(
        isA<RadishApiClientException>().having(
          (error) => error.message,
          'message',
          '评论编辑请求缺少提交意图 ID',
        ),
      ),
    );
  });

  test('http forum repository keeps Post/GetList target mapping and long ids',
      () async {
    final apiClient = _RecordingForumApiClient();
    final repository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getPostPage(
      pageIndex: 2,
      pageSize: 20,
      sort: ForumFeedSort.hottest,
    );

    expect(apiClient.lastUri?.path, '/api/v1/Post/GetList');
    expect(apiClient.lastUri?.queryParameters['pageIndex'], '2');
    expect(apiClient.lastUri?.queryParameters['pageSize'], '20');
    expect(apiClient.lastUri?.queryParameters['sortBy'], 'hottest');
    expect(apiClient.lastUri?.queryParameters['postType'], 'all');
    expect(page.posts.single.id, '9223372036854775806');
    expect(page.posts.single.categoryId, '9223372036854775805');
    expect(page.posts.single.authorId, '9223372036854775804');
  });
}
