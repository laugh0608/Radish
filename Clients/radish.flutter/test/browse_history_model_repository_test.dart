import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';

void main() {
  test('http repository uses private browse history endpoint and bearer token',
      () async {
    final apiClient = _RecordingProfileApiClient();
    final repository = HttpProfileRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getMyBrowseHistory(
      pageIndex: 2,
      pageSize: 20,
      accessToken: 'account-token',
    );

    expect(apiClient.lastUri?.path, '/api/v1/User/GetMyBrowseHistory');
    expect(apiClient.lastUri?.queryParameters['pageIndex'], '2');
    expect(apiClient.lastUri?.queryParameters['pageSize'], '20');
    expect(apiClient.lastBearerToken, 'account-token');
    expect(page.items.single.id, '9223372036854775807');
    expect(page.items.single.targetId, '2042219067430928384');
  });

  test('post target prefers a legal internal public route', () {
    final item = _item(
      targetType: 'Post',
      targetId: '2042219067430928384',
      routePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    );

    expect(item.target.kind, UserBrowseHistoryTargetKind.post);
    expect(
      item.target.value,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    );
    expect(item.target.openLabel, '打开帖子');
  });

  test('post target accepts numeric fallback without using external route', () {
    final item = _item(
      targetType: 'Post',
      targetId: '2042219067430928384',
      routePath: 'https://example.test/forum/post/evil',
    );

    expect(item.target.value, '2042219067430928384');
    expect(item.target.value, isNot(contains('example.test')));
  });

  test('post target accepts legacy public slug and rejects invalid identity',
      () {
    final legacy = _item(
      targetType: 'Post',
      targetId: '2042219067430928384',
      targetSlug: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    );
    final invalid = _item(
      targetType: 'Post',
      targetId: '-1',
      targetSlug: 'pst_invalid',
      routePath: '/forum/post/not-valid',
    );

    expect(
      legacy.target.value,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    );
    expect(invalid.target.canOpen, isFalse);
    expect(invalid.target.unavailableReason, contains('帖子标识无效'));
  });

  test('wiki target prefers slug and accepts both internal route families', () {
    final explicitSlug = _item(
      targetType: 'Wiki',
      targetId: '1001',
      targetSlug: 'flutter-native',
      routePath: '/docs/ignored-route',
    );
    final docsRoute = _item(
      targetType: 'Wiki',
      targetId: '1002',
      routePath: '/docs/from-route',
    );
    final legacyRoute = _item(
      targetType: 'Wiki',
      targetId: '1003',
      routePath: '/wiki/doc/legacy-slug',
    );

    expect(explicitSlug.target.value, 'flutter-native');
    expect(docsRoute.target.value, 'from-route');
    expect(legacyRoute.target.value, 'legacy-slug');
  });

  test('wiki and unknown invalid targets remain visible but cannot open', () {
    final invalidWiki = _item(
      targetType: 'Wiki',
      targetId: '1001',
      routePath: 'https://example.test/docs/external',
    );
    final unknown = _item(
      targetType: 'Video',
      targetId: '1002',
      routePath: '/video/1002',
    );

    expect(invalidWiki.target.canOpen, isFalse);
    expect(invalidWiki.target.unavailableReason, contains('Slug 无效'));
    expect(unknown.target.kind, UserBrowseHistoryTargetKind.unknown);
    expect(unknown.target.canOpen, isFalse);
  });

  test('product target only trusts a positive target id', () {
    final valid = _item(
      targetType: 'Product',
      targetId: '2042219067430928384',
      routePath: '/shop/product/1',
    );
    final invalid = _item(
      targetType: 'Product',
      targetId: '0',
      routePath: '/shop/product/2042219067430928384',
    );

    expect(valid.target.value, '2042219067430928384');
    expect(invalid.target.canOpen, isFalse);
  });

  test('last view time is localized and invalid values use stable fallback',
      () {
    final valid = _item(
      targetType: 'Product',
      targetId: '1001',
      lastViewTime: '2026-08-27T08:09:00',
    );
    final invalid = _item(
      targetType: 'Product',
      targetId: '1001',
      lastViewTime: 'not-a-time',
    );

    expect(valid.lastViewTimeLabel, '2026-08-27 08:09');
    expect(invalid.lastViewTimeLabel, '时间未知');
  });

  test('json model rejects non-positive history identifiers', () {
    expect(
      () => UserBrowseHistoryItem.fromJson({
        'voId': '0',
        'voTargetType': 'Product',
        'voTargetId': '1001',
        'voTitle': 'Invalid history',
        'voViewCount': 1,
        'voLastViewTime': '2026-08-27T08:00:00Z',
      }),
      throwsFormatException,
    );
  });
}

class _RecordingProfileApiClient implements RadishApiClient {
  Uri? lastUri;
  String? lastBearerToken;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBearerToken = bearerToken;
    return decode({
      'voItems': [
        {
          'voId': '9223372036854775807',
          'voTargetType': 'Post',
          'voTargetTypeDisplay': '帖子',
          'voTargetId': '2042219067430928384',
          'voTitle': 'LongId stays a string',
          'voRoutePath': '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
          'voViewCount': 2,
          'voLastViewTime': '2026-08-27T08:00:00Z',
        },
      ],
      'voTotal': 21,
      'voPageIndex': 2,
      'voPageSize': 20,
    });
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) =>
      throw UnimplementedError();

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) =>
      throw UnimplementedError();
}

UserBrowseHistoryItem _item({
  required String targetType,
  required String targetId,
  String? targetSlug,
  String? routePath,
  String lastViewTime = '2026-08-27T08:00:00Z',
}) {
  return UserBrowseHistoryItem(
    id: '9001',
    targetType: targetType,
    targetTypeDisplay: targetType,
    targetId: targetId,
    targetSlug: targetSlug,
    title: 'History target',
    routePath: routePath,
    viewCount: 1,
    lastViewTime: lastViewTime,
  );
}
