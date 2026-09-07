import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/profile/presentation/browse_history_page.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  const breakpointCases = <(double, String)>[
    (599, 'compact'),
    (600, 'medium'),
    (1024, 'expanded'),
    (1280, 'expanded'),
  ];

  for (final (width, layout) in breakpointCases) {
    testWidgets('uses $layout browse history layout at $width', (tester) async {
      await _setViewport(tester, Size(width, 3600));

      await tester.pumpWidget(_app());
      await tester.pumpAndSettle();

      expect(find.byKey(Key('browse-history-layout-$layout')), findsOneWidget);
      if (layout == 'expanded') {
        expect(find.text('数据来源说明'), findsOneWidget);
        expect(find.text('设备快捷记录'), findsOneWidget);
      } else {
        expect(find.text('数据来源说明'), findsNothing);
      }
      expect(tester.takeException(), isNull);
    });
  }

  for (final themeId in RadishThemeId.values) {
    testWidgets('keeps browse history semantic surface in ${themeId.value}', (
      tester,
    ) async {
      await _setViewport(tester, const Size(800, 3600));

      await tester.pumpWidget(_app(themeId: themeId));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('browse-history-layout-medium')),
        findsOneWidget,
      );
      expect(find.text('账号服务端历史'), findsWidgets);
      expect(find.text('打开帖子'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('long content and invalid targets stay inside compact viewport', (
    tester,
  ) async {
    await _setViewport(tester, const Size(390, 4200));

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(find.textContaining('超长账号历史标题'), findsOneWidget);
    expect(find.textContaining('超长摘要'), findsOneWidget);
    expect(find.text('时间未知'), findsOneWidget);
    expect(find.text('暂不可打开'), findsOneWidget);
    expect(find.textContaining('该记录类型暂不支持'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('surface does not expose internal route or raw ISO time', (
    tester,
  ) async {
    await _setViewport(tester, const Size(800, 3600));

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(
      find.text('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f'),
      findsNothing,
    );
    expect(find.text('2026-08-27T08:00:00Z'), findsNothing);
    expect(find.textContaining('浏览 9223372036854775807 次'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}

Future<void> _setViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _app({RadishThemeId themeId = RadishThemeId.defaultTheme}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: const BrowseHistoryPage(
      environment: AppEnvironment.development(),
      repository: _ResponsiveBrowseHistoryRepository(),
      shopRepository: EmptyShopRepository(),
      walletRepository: EmptyWalletRepository(),
      accessToken: 'access-token',
      accountId: 'account-42',
    ),
  );
}

class _ResponsiveBrowseHistoryRepository implements ProfileRepository {
  const _ResponsiveBrowseHistoryRepository();

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserBrowseHistoryPage(
      page: 1,
      pageSize: 20,
      total: 4,
      items: [
        UserBrowseHistoryItem(
          id: '9001',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '2042219067430928384',
          title: '超长账号历史标题需要在紧凑窗口中自然换行而不能挤出移动端视口',
          summary: '超长摘要用于验证服务端账号历史在 compact 连续流中保持可读、可换行和无横向溢出。',
          routePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
          viewCount: 9223372036854775807,
          lastViewTime: '2026-08-27T08:00:00Z',
        ),
        UserBrowseHistoryItem(
          id: '9002',
          targetType: 'Wiki',
          targetTypeDisplay: '文档',
          targetId: '2042219067430928385',
          targetSlug: 'flutter-native-product-ui',
          title: 'Flutter Native 产品化',
          viewCount: 2,
          lastViewTime: 'not-a-time',
        ),
        UserBrowseHistoryItem(
          id: '9003',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '2042219067430928386',
          title: 'Early Access Badge',
          viewCount: 1,
          lastViewTime: '2026-08-27T07:00:00Z',
        ),
        UserBrowseHistoryItem(
          id: '9004',
          targetType: 'Video',
          targetTypeDisplay: '视频',
          targetId: '2042219067430928387',
          title: '旧类型记录仍然保持可见',
          viewCount: 1,
          lastViewTime: '2026-08-27T06:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<MyProfileInfo> getMyProfile({required String accessToken}) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) =>
      throw UnimplementedError();

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileSummary> getPublicProfile({required String userId}) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileStats> getPublicStats({required String userId}) =>
      throw UnimplementedError();

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) =>
      throw UnimplementedError();

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) =>
      throw UnimplementedError();
}
