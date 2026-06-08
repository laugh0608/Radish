import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/profile/presentation/browse_history_page.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  testWidgets('renders browse history and opens native handoff targets', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final openedForumTargets = <ForumDetailHandoffTarget>[];
    final openedDocsTargets = <DocsDetailHandoffTarget>[];

    await tester.pumpWidget(
      MaterialApp(
        home: BrowseHistoryPage(
          environment: const AppEnvironment.development(),
          repository: const _BrowseHistoryRepository(),
          shopRepository: const EmptyShopRepository(),
          walletRepository: const EmptyWalletRepository(),
          accessToken: 'access-token',
          onOpenForumDetailTarget: openedForumTargets.add,
          onOpenDocsDetailTarget: openedDocsTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('最近访问'), findsWidgets);
    expect(find.text('已加载 3 / 3 条记录'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('Early Access Badge'), findsOneWidget);
    expect(
      find.text('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f801'),
      findsOneWidget,
    );
    expect(find.text('/forum/post/2042219067430928384'), findsNothing);

    await tester.tap(find.widgetWithText(FilledButton, '打开详情').first);
    await tester.pumpAndSettle();

    expect(openedForumTargets, hasLength(1));
    expect(
      openedForumTargets.single.postId,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
    );
    expect(
      openedForumTargets.single.source,
      ForumDetailHandoffSource.profileRecentBrowse,
    );

    await tester.tap(find.widgetWithText(FilledButton, '打开详情').at(1));
    await tester.pumpAndSettle();

    expect(openedDocsTargets, hasLength(1));
    expect(openedDocsTargets.single.slug, 'flutter-docs-scope');
    expect(
      openedDocsTargets.single.source,
      DocsDetailHandoffSource.profileRecentDocument,
    );
  });

  testWidgets('keeps current data visible when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _FailingRefreshBrowseHistoryRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: BrowseHistoryPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          shopRepository: const EmptyShopRepository(),
          walletRepository: const EmptyWalletRepository(),
          accessToken: 'access-token',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Native profile follow-up'), findsOneWidget);

    await tester.tap(find.text('刷新最近访问'));
    await tester.pumpAndSettle();

    expect(find.text('刷新最近访问失败'), findsOneWidget);
    expect(find.text('浏览记录服务暂时不可用'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsOneWidget);
  });
}

class _BrowseHistoryRepository implements ProfileRepository {
  const _BrowseHistoryRepository();

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserBrowseHistoryPage(
      page: 1,
      pageSize: 20,
      total: 3,
      items: [
        UserBrowseHistoryItem(
          id: 'history-post-1',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '2042219067430928384',
          targetSlug: 'pst_018f6b6f7c7d70008f8f8f8f8f8f801',
          title: 'Native profile follow-up',
          summary: 'Expand the public profile beyond a single info card.',
          routePath: '/forum/post/2042219067430928384',
          viewCount: 2,
          lastViewTime: '2026-04-20T09:30:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-docs-1',
          targetType: 'Wiki',
          targetTypeDisplay: '文档',
          targetId: '1002',
          targetSlug: 'flutter-docs-scope',
          title: 'Radish Flutter docs scope',
          routePath: '/docs/flutter-docs-scope',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:20:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-product-1',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '1003',
          title: 'Early Access Badge',
          routePath: '/shop/products/1003',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<MyProfileInfo> getMyProfile({
    required String accessToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    throw UnimplementedError();
  }
}

class _FailingRefreshBrowseHistoryRepository extends _BrowseHistoryRepository {
  var _requestCount = 0;

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    _requestCount += 1;
    if (_requestCount > 1) {
      throw const RadishApiClientException('浏览记录服务暂时不可用');
    }

    return super.getMyBrowseHistory(
      pageIndex: pageIndex,
      pageSize: pageSize,
      accessToken: accessToken,
    );
  }
}
