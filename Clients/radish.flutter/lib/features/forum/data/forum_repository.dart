import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'forum_models.dart';

abstract class ForumRepository {
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
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
}
