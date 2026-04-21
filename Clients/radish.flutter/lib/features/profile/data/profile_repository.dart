import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'profile_models.dart';

abstract class ProfileRepository {
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  });

  Future<PublicProfileStats> getPublicStats({
    required String userId,
  });

  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  });

  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  });
}

class HttpProfileRepository implements ProfileRepository {
  const HttpProfileRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/User/GetPublicProfile',
      queryParameters: {
        'userId': userId,
      },
    );

    return apiClient.get(
      uri: uri,
      decode: PublicProfileSummary.fromJson,
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/User/GetUserStats',
      queryParameters: {
        'userId': userId,
      },
    );

    return apiClient.get(
      uri: uri,
      decode: PublicProfileStats.fromJson,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Post/GetUserPosts',
      queryParameters: {
        'userId': userId,
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: PublicProfilePostPage.fromJson,
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Comment/GetUserComments',
      queryParameters: {
        'userId': userId,
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: PublicProfileCommentPage.fromJson,
    );
  }
}
