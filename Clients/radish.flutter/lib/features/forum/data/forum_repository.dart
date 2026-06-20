import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'forum_models.dart';

abstract class ForumRepository {
  Future<List<ForumCategorySummary>> getTopCategories();

  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  });

  Future<ForumPostDetail> getPostDetail({
    required String postId,
  });

  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  });

  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  });

  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  });

  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  });

  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  });

  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  });

  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  });

  Future<String> createPost({
    required String title,
    required String content,
    required String categoryId,
    required List<String> tagNames,
    required String accessToken,
    required String clientSubmissionId,
  });
}

class HttpForumRepository implements ForumRepository {
  const HttpForumRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<List<ForumCategorySummary>> getTopCategories() {
    final uri = endpoints.resolveApi('/api/v1/Category/GetTopCategories');

    return apiClient.get(
      uri: uri,
      decode: _readForumCategories,
    );
  }

  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Post/GetList',
      queryParameters: {
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
        'sortBy': sort.apiValue,
        'postType': 'all',
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumPostPage.fromJson,
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) {
    final normalizedPostId = postId.trim();
    final uri = endpoints.resolveApi(
      '/api/v1/Post/GetById/${Uri.encodeComponent(normalizedPostId)}',
      queryParameters: const {
        'answerSort': 'default',
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumPostDetail.fromJson,
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) {
    final normalizedPostId = postId.trim();
    final normalizedSortBy = sortBy.trim().isEmpty ? 'default' : sortBy.trim();
    final uri = endpoints.resolveApi(
      '/api/v1/Comment/GetRootComments',
      queryParameters: {
        'postId': normalizedPostId,
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
        'sortBy': normalizedSortBy,
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumCommentPage.fromJson,
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) {
    final normalizedParentId = parentId.trim();
    final uri = endpoints.resolveApi(
      '/api/v1/Comment/GetChildComments',
      queryParameters: {
        'parentId': normalizedParentId,
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumChildCommentPage.fromJson,
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) {
    final normalizedPostId = postId.trim();
    final normalizedCommentId = commentId.trim();
    final uri = endpoints.resolveApi(
      '/api/v1/Comment/GetNavigation',
      queryParameters: {
        'postId': normalizedPostId,
        'commentId': normalizedCommentId,
        'rootPageSize': rootPageSize.toString(),
        'childPageSize': childPageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumCommentNavigationLocation.fromJson,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) {
    final normalizedPostId = postId.trim();
    final uri = endpoints.resolveApi(
      '/api/v1/PostQuickReply/GetRecentByPostId',
      queryParameters: {
        'postId': normalizedPostId,
        'take': take.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ForumQuickReplyWall.fromJson,
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    final normalizedPostId = postId.trim();
    final normalizedContent = content.trim();
    final uri = endpoints.resolveApi('/api/v1/PostQuickReply/Create');

    return apiClient.post(
      uri: uri,
      body: {
        'postId': normalizedPostId,
        'content': normalizedContent,
      },
      bearerToken: accessToken,
      decode: ForumQuickReplySummary.fromJson,
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
  }) {
    final normalizedPostId = postId.trim();
    final normalizedContent = content.trim();
    final normalizedClientSubmissionId = clientSubmissionId.trim();
    if (normalizedClientSubmissionId.isEmpty) {
      throw const RadishApiClientException('评论请求缺少提交意图 ID');
    }

    final uri = endpoints.resolveApi('/api/v1/Comment/Create');

    return apiClient.post(
      uri: uri,
      body: {
        'postId': normalizedPostId,
        'content': normalizedContent,
        'clientSubmissionId': normalizedClientSubmissionId,
        'parentId': _readNullableString(parentId),
        'replyToCommentId': _readNullableString(replyToCommentId),
        'replyToCommentSnapshot': _readNullableString(replyToCommentSnapshot),
        'replyToUserName': _readNullableString(replyToUserName),
      },
      bearerToken: accessToken,
      decode: _readCreatedCommentId,
    );
  }

  @override
  Future<ForumQuestionDetail> answerQuestion({
    required String postId,
    required String content,
    required String accessToken,
    required String clientSubmissionId,
  }) {
    final normalizedPostId = postId.trim();
    final normalizedContent = content.trim();
    final normalizedClientSubmissionId = clientSubmissionId.trim();
    if (normalizedClientSubmissionId.isEmpty) {
      throw const RadishApiClientException('回答请求缺少提交意图 ID');
    }

    final uri = endpoints.resolveApi('/api/v1/Question/Answer');

    return apiClient.post(
      uri: uri,
      body: {
        'postId': normalizedPostId,
        'content': normalizedContent,
        'clientSubmissionId': normalizedClientSubmissionId,
      },
      bearerToken: accessToken,
      decode: ForumQuestionDetail.fromJson,
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
  }) {
    final normalizedTitle = title.trim();
    final normalizedContent = content.trim();
    final normalizedCategoryId = categoryId.trim();
    final normalizedClientSubmissionId = clientSubmissionId.trim();
    if (normalizedClientSubmissionId.isEmpty) {
      throw const RadishApiClientException('发帖请求缺少提交意图 ID');
    }

    final normalizedTags = tagNames
        .map((tag) => tag.trim())
        .where((tag) => tag.isNotEmpty)
        .toSet()
        .toList();
    final uri = endpoints.resolveApi('/api/v1/Post/Publish');

    return apiClient.post(
      uri: uri,
      body: {
        'title': normalizedTitle,
        'content': normalizedContent,
        'clientSubmissionId': normalizedClientSubmissionId,
        'contentType': 'text',
        'categoryId': normalizedCategoryId,
        'tagNames': normalizedTags,
        'isQuestion': false,
      },
      bearerToken: accessToken,
      decode: _readCreatedPostId,
    );
  }
}

List<ForumCategorySummary> _readForumCategories(Object? json) {
  if (json is! List) {
    return const <ForumCategorySummary>[];
  }

  return json.map(ForumCategorySummary.fromJson).toList();
}

String? _readNullableString(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}

String _readCreatedCommentId(Object? json) {
  final value = json?.toString().trim();
  if (value == null || value.isEmpty) {
    throw const FormatException('Missing created comment id.');
  }

  return value;
}

String _readCreatedPostId(Object? json) {
  final value = json?.toString().trim();
  if (value == null || value.isEmpty) {
    throw const FormatException('Missing created post id.');
  }

  return value;
}
