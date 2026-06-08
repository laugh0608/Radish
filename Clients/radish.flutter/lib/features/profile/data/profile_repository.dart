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

  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  });

  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  });

  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    throw UnimplementedError('getMyProfile is not implemented.');
  }

  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw UnimplementedError('updateMyProfile is not implemented.');
  }
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

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/PostQuickReply/GetMine',
      queryParameters: {
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: accessToken,
      decode: UserQuickReplyPage.fromJson,
    );
  }

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/User/GetMyBrowseHistory',
      queryParameters: {
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: accessToken,
      decode: UserBrowseHistoryPage.fromJson,
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    final uri = endpoints.resolveApi('/api/v1/User/GetMyProfile');

    return apiClient.get(
      uri: uri,
      bearerToken: accessToken,
      decode: MyProfileInfo.fromJson,
    );
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    final uri = endpoints.resolveApi('/api/v1/User/UpdateMyProfile');

    return apiClient.post(
      uri: uri,
      body: request.toJson(),
      bearerToken: accessToken,
      decode: (_) {},
    );
  }
}
