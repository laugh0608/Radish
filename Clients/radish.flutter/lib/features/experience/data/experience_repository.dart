import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'experience_models.dart';

abstract class ExperienceRepository {
  Future<UserExperience> getMyExperience({
    required String accessToken,
  });

  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  });
}

class EmptyExperienceRepository implements ExperienceRepository {
  const EmptyExperienceRepository();

  @override
  Future<UserExperience> getMyExperience({
    required String accessToken,
  }) {
    throw const RadishApiClientException('经验记录暂时不可用');
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('经验流水暂时不可用');
  }
}

class HttpExperienceRepository implements ExperienceRepository {
  const HttpExperienceRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<UserExperience> getMyExperience({
    required String accessToken,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看经验记录');
    }

    final uri = endpoints.resolveApi('/api/v1/Experience/GetMyExperience');

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: UserExperience.fromJson,
    );
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看经验流水');
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Experience/GetTransactions',
      queryParameters: {
        'pageIndex': pageIndex.clamp(1, 9999).toString(),
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: ExperienceTransactionPage.fromJson,
    );
  }
}
