import 'dart:async';

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
          accountId: 'account-42',
          onOpenForumDetailTarget: openedForumTargets.add,
          onOpenDocsDetailTarget: openedDocsTargets.add,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('账号浏览历史'), findsWidgets);
    expect(find.text('已加载 3 / 3 条记录'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsOneWidget);
    expect(find.text('Radish Flutter docs scope'), findsOneWidget);
    expect(find.text('Early Access Badge'), findsOneWidget);
    expect(
      find.text('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f'),
      findsNothing,
    );
    expect(find.text('/forum/post/2042219067430928384'), findsNothing);

    await tester.tap(find.widgetWithText(FilledButton, '打开帖子'));
    await tester.pumpAndSettle();

    expect(openedForumTargets, hasLength(1));
    expect(
      openedForumTargets.single.postId,
      'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    );
    expect(
      openedForumTargets.single.source,
      ForumDetailHandoffSource.profileRecentBrowse,
    );

    await tester.tap(find.widgetWithText(FilledButton, '打开文档'));
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
          accountId: 'account-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Native profile follow-up'), findsOneWidget);

    await tester.tap(find.text('刷新账号历史'));
    await tester.pumpAndSettle();

    expect(find.text('账号浏览历史刷新失败'), findsOneWidget);
    expect(find.text('浏览记录服务暂时不可用'), findsOneWidget);
    expect(find.text('Native profile follow-up'), findsOneWidget);
  });

  testWidgets('repository rebuild rejects the previous owner response', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final previous = Completer<UserBrowseHistoryPage>();
    final previousRepository = _DeferredBrowseHistoryRepository(previous);

    await tester.pumpWidget(
      MaterialApp(
        home: BrowseHistoryPage(
          environment: const AppEnvironment.development(),
          repository: previousRepository,
          shopRepository: const EmptyShopRepository(),
          walletRepository: const EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'account-42',
        ),
      ),
    );
    await tester.pump();

    await tester.pumpWidget(
      const MaterialApp(
        home: BrowseHistoryPage(
          environment: AppEnvironment.development(),
          repository: _ReplacementBrowseHistoryRepository(),
          shopRepository: EmptyShopRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'account-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Repository B'), findsOneWidget);
    previous.complete(_singleHistoryPage(title: 'Repository A'));
    await tester.pumpAndSettle();

    expect(find.text('Repository B'), findsOneWidget);
    expect(find.text('Repository A'), findsNothing);
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
          id: '9001',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '2042219067430928384',
          title: 'Native profile follow-up',
          summary: 'Expand the public profile beyond a single info card.',
          routePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
          viewCount: 2,
          lastViewTime: '2026-04-20T09:30:00Z',
        ),
        UserBrowseHistoryItem(
          id: '9002',
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
          id: '9003',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '1003',
          title: 'Early Access Badge',
          routePath: '/shop/product/1003',
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

class _DeferredBrowseHistoryRepository extends _BrowseHistoryRepository {
  _DeferredBrowseHistoryRepository(this.pending);

  final Completer<UserBrowseHistoryPage> pending;

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    return pending.future;
  }
}

class _ReplacementBrowseHistoryRepository extends _BrowseHistoryRepository {
  const _ReplacementBrowseHistoryRepository();

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return _singleHistoryPage(title: 'Repository B');
  }
}

UserBrowseHistoryPage _singleHistoryPage({required String title}) {
  return UserBrowseHistoryPage(
    page: 1,
    pageSize: 20,
    total: 1,
    items: [
      UserBrowseHistoryItem(
        id: '9100',
        targetType: 'Product',
        targetTypeDisplay: '商品',
        targetId: '1001',
        title: title,
        viewCount: 1,
        lastViewTime: '2026-08-27T08:00:00Z',
      ),
    ],
  );
}
