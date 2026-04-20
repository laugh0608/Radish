import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'profile_models.dart';

abstract class ProfileRepository {
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
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
}
