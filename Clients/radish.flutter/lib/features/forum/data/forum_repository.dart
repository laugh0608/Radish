import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'forum_models.dart';

abstract class ForumRepository {
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
}

class HttpForumRepository implements ForumRepository {
  const HttpForumRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

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
}
