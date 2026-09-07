import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'discover_models.dart';

abstract class DiscoverRepository {
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  });
}

class HttpDiscoverRepository implements DiscoverRepository {
  const HttpDiscoverRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/PublicDiscover/GetFeed',
      queryParameters: {
        'pageSize': pageSize.toString(),
        'cursor': cursor?.trim(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: DiscoverFeedPage.fromJson,
    );
  }
}
