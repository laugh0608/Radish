import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'leaderboard_models.dart';

abstract class LeaderboardRepository {
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  });
}

class EmptyLeaderboardRepository implements LeaderboardRepository {
  const EmptyLeaderboardRepository();

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) async {
    return LeaderboardPageResult.empty(
      page: pageIndex,
      pageSize: pageSize,
    );
  }
}

class HttpLeaderboardRepository implements LeaderboardRepository {
  const HttpLeaderboardRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Leaderboard/GetLeaderboard',
      queryParameters: {
        'type': '1',
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: LeaderboardPageResult.fromJson,
    );
  }
}
