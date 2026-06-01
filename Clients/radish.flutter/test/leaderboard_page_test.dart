import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_models.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_repository.dart';
import 'package:radish_flutter/features/leaderboard/presentation/leaderboard_page.dart';

void main() {
  testWidgets('renders public experience leaderboard entries', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: LeaderboardPage(
          repository: _SuccessLeaderboardRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('榜单'), findsOneWidget);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('#1'), findsOneWidget);
    expect(find.text('luobo'), findsOneWidget);
    expect(find.text('Lv.8 · 探索者'), findsOneWidget);
    expect(find.text('总经验值: 18888'), findsOneWidget);
    expect(find.text('当前账号'), findsOneWidget);
    expect(find.text('打开公开主页'), findsNothing);
  });

  testWidgets(
      'opens public profile from leaderboard entry when callback exists',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    String? openedUserId;
    await tester.pumpWidget(
      MaterialApp(
        home: LeaderboardPage(
          repository: const _SuccessLeaderboardRepository(),
          onOpenProfileUser: (userId) {
            openedUserId = userId;
          },
        ),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('打开公开主页'));

    expect(openedUserId, '2042219067430928384');
  });

  testWidgets('renders leaderboard error state when repository fails', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: LeaderboardPage(
          repository: _FailingLeaderboardRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载榜单'), findsOneWidget);
    expect(find.text('榜单服务暂时不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });

  test('http leaderboard repository uses experience leaderboard endpoint',
      () async {
    final apiClient = _RecordingLeaderboardApiClient();
    final repository = HttpLeaderboardRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getExperienceLeaderboard(
      pageIndex: 1,
      pageSize: 20,
    );

    expect(apiClient.lastUri?.path, '/api/v1/Leaderboard/GetLeaderboard');
    expect(apiClient.lastUri?.queryParameters['type'], '1');
    expect(apiClient.lastUri?.queryParameters['pageIndex'], '1');
    expect(apiClient.lastUri?.queryParameters['pageSize'], '20');
    expect(page.items.single.userId, '2042219067430928384');
    expect(page.items.single.primaryValue, '18888');
  });
}

class _SuccessLeaderboardRepository implements LeaderboardRepository {
  const _SuccessLeaderboardRepository();

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const LeaderboardPageResult(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      items: [
        LeaderboardItem(
          rank: 1,
          userId: '2042219067430928384',
          userName: 'luobo',
          currentLevel: 8,
          currentLevelName: '探索者',
          themeColor: '#5B8DEF',
          isCurrentUser: true,
          primaryValue: '18888',
          primaryLabel: '总经验值',
        ),
      ],
    );
  }
}

class _FailingLeaderboardRepository implements LeaderboardRepository {
  const _FailingLeaderboardRepository();

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('榜单服务暂时不可用');
  }
}

class _RecordingLeaderboardApiClient implements RadishApiClient {
  Uri? lastUri;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    return decode({
      'page': 1,
      'pageSize': 20,
      'dataCount': 1,
      'pageCount': 1,
      'data': [
        {
          'voRank': 1,
          'voUserId': '2042219067430928384',
          'voUserName': 'luobo',
          'voCurrentLevel': 8,
          'voCurrentLevelName': '探索者',
          'voIsCurrentUser': false,
          'voPrimaryValue': 18888,
          'voPrimaryLabel': '总经验值',
        },
      ],
    });
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
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
