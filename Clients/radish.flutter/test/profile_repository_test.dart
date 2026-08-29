import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';

void main() {
  test('public profile resources use identifier-aware endpoints', () async {
    const publicId = 'usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f';
    final apiClient = _RecordingProfileApiClient();
    final repository = HttpProfileRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final profile = await repository.getPublicProfile(userId: publicId);
    final stats = await repository.getPublicStats(userId: publicId);
    final posts = await repository.getPublicPosts(
      userId: publicId,
      pageIndex: 2,
      pageSize: 3,
    );
    final comments = await repository.getPublicComments(
      userId: publicId,
      pageIndex: 4,
      pageSize: 5,
    );

    expect(profile.userId, '2042219067430928384');
    expect(stats.postCount, 2);
    expect(posts.page, 2);
    expect(comments.page, 4);
    expect(
      apiClient.requestedUris.map((uri) => uri.path),
      [
        '/api/v1/User/GetPublicProfile',
        '/api/v1/User/GetPublicUserStats',
        '/api/v1/Post/GetPublicUserPosts',
        '/api/v1/Comment/GetPublicUserComments',
      ],
    );
    for (final uri in apiClient.requestedUris) {
      expect(uri.queryParameters['identifier'], publicId);
      expect(uri.queryParameters.containsKey('userId'), isFalse);
    }
    expect(apiClient.requestedUris[2].queryParameters['pageIndex'], '2');
    expect(apiClient.requestedUris[2].queryParameters['pageSize'], '3');
    expect(apiClient.requestedUris[3].queryParameters['pageIndex'], '4');
    expect(apiClient.requestedUris[3].queryParameters['pageSize'], '5');
    expect(apiClient.bearerTokens, everyElement(isNull));
  });

  test('profile update preserves explicit empty address for clearing',
      () async {
    final apiClient = _RecordingProfileApiClient();
    final repository = HttpProfileRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    await repository.updateMyProfile(
      request: const UpdateMyProfileRequest(
        userName: 'luobo',
        userEmail: 'luobo@example.test',
        address: '',
      ),
      accessToken: 'access-token',
    );

    expect(apiClient.lastPostUri?.path, '/api/v1/User/UpdateMyProfile');
    expect(apiClient.lastPostBearerToken, 'access-token');
    expect(apiClient.lastPostBody, {
      'userName': 'luobo',
      'userEmail': 'luobo@example.test',
      'sex': null,
      'age': null,
      'birth': null,
      'address': '',
    });
  });
}

class _RecordingProfileApiClient implements RadishApiClient {
  final List<Uri> requestedUris = [];
  final List<String?> bearerTokens = [];
  Uri? lastPostUri;
  Object? lastPostBody;
  String? lastPostBearerToken;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    requestedUris.add(uri);
    bearerTokens.add(bearerToken);

    final payload = switch (uri.path) {
      '/api/v1/User/GetPublicProfile' => {
          'voUserId': '2042219067430928384',
          'voUserName': 'luobo',
          'voCreateTime': '2026-08-29T00:00:00Z',
        },
      '/api/v1/User/GetPublicUserStats' => {
          'voPostCount': 2,
          'voCommentCount': 1,
          'voTotalLikeCount': 3,
          'voPostLikeCount': 2,
          'voCommentLikeCount': 1,
        },
      '/api/v1/Post/GetPublicUserPosts' => {
          'page': 2,
          'pageSize': 3,
          'dataCount': 0,
          'pageCount': 0,
          'data': <Object?>[],
        },
      '/api/v1/Comment/GetPublicUserComments' => {
          'page': 4,
          'pageSize': 5,
          'dataCount': 0,
          'pageCount': 0,
          'data': <Object?>[],
        },
      _ => throw StateError('Unexpected profile request: $uri'),
    };

    return decode(payload);
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastPostUri = uri;
    lastPostBody = body;
    lastPostBearerToken = bearerToken;
    return decode(null);
  }

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }
}
