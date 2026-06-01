import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/platform/app_lifecycle_gateway.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_follow_up_store.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/experience/data/experience_models.dart';
import 'package:radish_flutter/features/experience/data/experience_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_models.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_repository.dart';
import 'package:radish_flutter/features/notifications/data/notification_repository.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

Finder _forumCommentTextField({String hintText = '写下你的评论...'}) {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.hintText == hintText,
  );
}

void main() {
  testWidgets('restores into guest shell when no session exists',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    expect(find.text('正在恢复会话'), findsOneWidget);

    await tester.pump();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('游客'), findsOneWidget);
  });

  testWidgets('renders shell status strip on narrow screens without overflow',
      (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('DEVELOPMENT'), findsOneWidget);
    expect(find.text('游客'), findsOneWidget);
    expect(find.text('登录'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('root Android back moves the app task to background',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('Radish Flutter'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 1);
    expect(find.text('Radish Flutter'), findsOneWidget);
  });

  testWidgets('restores authenticated session into profile boundary',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('已登录用户 user-42'), findsOneWidget);
  });

  testWidgets('authenticated profile opens read-only private account routes',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _SeededProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        walletRepository: const _SeededWalletRepository(),
        experienceRepository: const _SeededExperienceRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('查看商城订单'), findsOneWidget);
    expect(find.text('查看背包'), findsOneWidget);
    expect(find.text('查看胡萝卜资产'), findsOneWidget);
    expect(find.text('查看经验记录'), findsOneWidget);
    expect(find.text('查看最近访问'), findsOneWidget);

    await tester.tap(find.text('查看商城订单'));
    await tester.pumpAndSettle();

    expect(find.text('我的订单'), findsWidgets);
    expect(find.textContaining('RO202605310001'), findsOneWidget);
    expect(find.text('已加载 1 / 1 个订单'), findsOneWidget);
    expect(find.text('查看订单详情'), findsOneWidget);

    await tester.tap(find.text('查看订单详情'));
    await tester.pumpAndSettle();

    expect(find.text('来源：订单列表'), findsOneWidget);
    expect(find.text('完成支付'), findsOneWidget);
    expect(find.text('订单完成'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的订单'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看商城订单'), findsOneWidget);

    await tester.tap(find.text('查看背包'));
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
    expect(find.text('已加载 1 个权益、1 个道具'), findsOneWidget);
    expect(find.text('早鸟徽章'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('查看来源订单'), findsOneWidget);
    expect(find.text('查看来源商品'), findsWidgets);

    await tester.tap(find.text('查看来源订单'));
    await tester.pumpAndSettle();

    expect(find.text('来源：背包来源'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);
    expect(find.text('完成支付'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);

    await tester.tap(find.text('查看来源商品').first);
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：背包来源'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看背包'), findsOneWidget);

    await tester.tap(find.text('查看胡萝卜资产'));
    await tester.pumpAndSettle();

    expect(find.text('胡萝卜资产'), findsWidgets);
    expect(find.text('已加载 2 / 2 条流水'), findsOneWidget);
    expect(find.text('余额概览'), findsOneWidget);
    expect(find.text('1200 胡萝卜'), findsOneWidget);
    expect(find.text('系统赠送'), findsOneWidget);
    expect(find.text('商城消费'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看胡萝卜资产'), findsOneWidget);

    await tester.tap(find.text('查看经验记录'));
    await tester.pumpAndSettle();

    expect(find.text('经验记录'), findsWidgets);
    expect(find.text('已加载 2 / 2 条经验流水'), findsOneWidget);
    expect(find.text('等级概览'), findsOneWidget);
    expect(find.text('Lv.3 练气'), findsOneWidget);
    expect(find.text('发帖奖励'), findsOneWidget);
    expect(find.text('评论奖励'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看经验记录'), findsOneWidget);

    await tester.tap(find.text('查看最近访问'));
    await tester.pumpAndSettle();

    expect(find.text('最近访问'), findsWidgets);
    expect(find.text('已加载 3 / 3 条记录'), findsOneWidget);
    expect(find.text('论坛详情回流'), findsOneWidget);
    expect(find.text('Native docs'), findsOneWidget);
    expect(find.text('Early Access Badge'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '打开详情').last);
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：浏览记录'), findsOneWidget);
    expect(find.text('返回最近访问'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('最近访问'), findsWidgets);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('查看最近访问'), findsOneWidget);
  });

  testWidgets('discover handoff opens guest profile target in shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final scrollable = find.byType(Scrollable).last;
    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开 @luobo'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('打开 @luobo'));
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('用户 user-9'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover forum card opens detail and returns to discover',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开帖子'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开帖子'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-1'), findsOneWidget);
    expect(find.text('打开来源：发现'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover forum shortcut returns to discover on Android back',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('进入论坛'));
    await tester.pumpAndSettle();

    expect(find.text('浏览公开帖子，支持最新和热门排序。当前阶段仅提供只读阅读。'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover docs shortcut returns to discover on Android back',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('进入文档'));
    await tester.pumpAndSettle();

    expect(find.text('浏览公开文档列表。当前不开放编辑、发布和治理操作。'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets(
      'discover leaderboard shortcut returns to discover on Android back',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开榜单').first,
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开榜单').first);
    await tester.pumpAndSettle();

    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('当前暂无可展示的经验榜排名。'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('Native discover'), findsOneWidget);
  });

  testWidgets('discover shop product opens read-only detail and returns',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('查看详情'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('公开商品详情'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsWidgets);
    expect(find.text('只读购买边界'), findsOneWidget);
    expect(find.text('/shop/product/product-4001'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('商城精选'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
  });

  testWidgets('discover shop shortcut opens read-only product list and returns',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        shopRepository: const _SeededShopRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('查看全部商品'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('查看全部商品'));
    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);
    expect(find.text('/shop/product/product-4001'), findsOneWidget);

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：公开商品列表'), findsOneWidget);
    expect(find.text('返回商城'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('继续阅读'), findsOneWidget);
    expect(find.text('商城精选'), findsOneWidget);
  });

  testWidgets('leaderboard user can open public profile and return to ranking',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();

    expect(find.text('luobo'), findsOneWidget);
    await tester.tap(find.text('打开公开主页'));
    await tester.pumpAndSettle();

    expect(find.text('公开主页'), findsOneWidget);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsOneWidget);
  });

  testWidgets(
      'leaderboard profile detail pop preserves return to ranking on Android back',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();

    await tester.tap(find.text('打开公开主页'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    final scrollable = find.byType(Scrollable).last;
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: scrollable,
    );
    await tester.ensureVisible(openPostButton);
    await tester.pumpAndSettle();
    await tester.drag(scrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openPostButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsOneWidget);
  });

  testWidgets(
      'leaderboard profile post and comment details handle Android back to source',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        leaderboardRepository: const _SeededLeaderboardRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('榜单').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('打开公开主页'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    final profileScrollable = find.byType(Scrollable).last;
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.tap(openPostButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);

    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('First public child comment'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开评论'), findsOneWidget);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('First public child comment'), findsNothing);
    expect(find.text('帖子详情'), findsNothing);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('榜单类型：经验榜'), findsOneWidget);
    expect(find.text('luobo'), findsOneWidget);
  });

  testWidgets('recent public profile target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(sessionController);

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _SeededDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _FakeForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开 @luobo'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开 @luobo'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('继续看公开主页'), findsOneWidget);

    await tester.tap(find.text('继续看公开主页'));
    await tester.pumpAndSettle();

    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
  });

  testWidgets('refreshes expired session before entering shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt:
                DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt:
              DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.success(
        AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 2)),
          ),
          refreshToken: 'refresh-token-next',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 2)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('游客'), findsNothing);
  });

  testWidgets('falls back to guest shell when refresh fails', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt:
                DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt:
              DateTime.now().toUtc().subtract(const Duration(minutes: 5)),
        ),
      ),
      refreshService:
          _FakeSessionRefreshService.failure('refresh token expired'),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();

    expect(find.text('游客'), findsOneWidget);
    expect(find.text('会话已失效'), findsOneWidget);
  });

  testWidgets('native OIDC callback redeems a session into the shell',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(
      sessionController,
      pendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'native-code-1',
      ),
      nextSession: AuthSession(
        accessToken: _buildJwt(
          userId: 'user-88',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
        refreshToken: 'refresh-token',
        userId: 'user-88',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('已登录'), findsOneWidget);
    expect(find.text('已登录用户 user-88'), findsOneWidget);
  });

  testWidgets('shows a visible shell notice when browser sign-in is canceled',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(sessionController);

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await authController.startLogin();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text(
        '浏览器返回应用前，登录已取消。',
      ),
      findsOneWidget,
    );
    expect(find.text('重试登录'), findsOneWidget);
  });

  testWidgets('maps access_denied callback into a friendly auth notice',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final authController = _buildAuthController(
      sessionController,
      pendingCallback: const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        error: 'access_denied',
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text('已在浏览器中取消登录。'),
      findsOneWidget,
    );
    await tester.tap(find.text('关闭'));
    await tester.pumpAndSettle();
    expect(find.text('登录需要处理'), findsNothing);
  });

  testWidgets('profile sign-in returns to the original profile tab',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-108',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-108',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    expect(find.text('游客模式'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, '登录'));
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-108'), findsOneWidget);
    expect(find.text('已登录'), findsOneWidget);
  });

  testWidgets('persisted profile sign-in target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-208',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-208',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore();

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _FakeDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _FakeForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, '登录'));
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'profile-login-rebuild-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('已登录用户 user-208'), findsOneWidget);
    expect(find.text('已登录'), findsOneWidget);
  });

  testWidgets('retry sign-in resumes forum detail target after cancellation',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-109',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-109',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(
      find.text('浏览器返回应用前，登录已取消。'),
      findsOneWidget,
    );

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-code',
      ),
    );
    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
    expect(find.text('已登录用户 user-109'), findsNothing);
  });

  testWidgets('persisted forum sign-in target survives shell rebuild',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-209',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-209',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore();

    Future<void> pumpApp() async {
      await tester.pumpWidget(
        RadishApp(
          environment: const AppEnvironment.development(),
          sessionController: sessionController,
          authController: authController,
          discoverRepository: _FakeDiscoverRepository(),
          docsRepository: _FakeDocsRepository(),
          forumRepository: _SeededBigIdForumRepository(),
          profileRepository: _FakeProfileRepository(),
          followUpStore: followUpStore,
        ),
      );
    }

    await pumpApp();

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.text('登录'));
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();

    await pumpApp();
    await tester.pump();
    await tester.pumpAndSettle();

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-login-rebuild-code',
      ),
    );
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
  });

  testWidgets('forum detail sign-in keeps current detail context',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-210',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-210',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('登录并保留当前位置'), findsOneWidget);

    await tester.tap(find.text('登录并保留当前位置'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('登录需要处理'), findsOneWidget);
    expect(find.text('重试登录'), findsOneWidget);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'forum-detail-login-code',
      ),
    );
    await tester.tap(find.text('重试登录'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已登录'), findsWidgets);
    expect(find.text('登录并保留当前位置'), findsNothing);
  });

  testWidgets(
      'notification detail sign-in keeps source and comment target after retry',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-211',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-211',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );
    final followUpStore = InMemoryForumFollowUpStore(
      initialPendingHandoff: const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: 'Native discover wiring plan',
        commentId: 'comment-big-1',
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _SeededBigIdDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('登录并保留当前位置'), findsOneWidget);

    await tester.tap(find.text('登录并保留当前位置'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('登录需要处理'), findsWidgets);
    expect(find.text('重试登录'), findsWidgets);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'notification-detail-login-code',
      ),
    );
    await tester.tap(find.text('重试登录').first);
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);
    expect(find.text('已登录'), findsWidgets);
    expect(find.text('登录并保留当前位置'), findsNothing);
  });

  testWidgets('quick reply sign-in returns to composer in current detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-212',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-212',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('登录后发布'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    expect(find.text('登录后可以发布轻回应'), findsOneWidget);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'quick-reply-login-code',
      ),
    );
    await tester.tap(find.text('登录后发布'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已回到轻回应区，可以继续发布。'), findsOneWidget);
    expect(find.text('发布轻回应'), findsOneWidget);
    expect(find.text('登录后发布'), findsNothing);
  });

  testWidgets('comment sign-in returns to composer in current detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final gateway = InMemoryNativeAuthGateway();
    final authController = NativeAuthController(
      environment: const AppEnvironment.development(),
      sessionController: sessionController,
      gateway: gateway,
      exchangeService: _FakeAuthorizationCodeExchangeService(
        nextSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-213',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-213',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: authController,
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('登录后评论'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    expect(find.text('登录后可以发表评论'), findsOneWidget);

    gateway.setPendingCallback(
      const NativeAuthCallbackPayload(
        type: NativeAuthCallbackType.login,
        code: 'comment-login-code',
      ),
    );
    await tester.tap(find.text('登录后评论'));
    await tester.pumpAndSettle();
    await authController.consumePendingCallback();
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('已回到评论区，可以继续发布。'), findsOneWidget);
    expect(find.text('发布评论'), findsOneWidget);
    expect(find.text('登录后评论'), findsNothing);
  });

  testWidgets('forum feed opens native public detail page', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsOneWidget);

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('正文'), findsOneWidget);
  });

  testWidgets('forum author handoff opens native public profile tab',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final lifecycleGateway = _RecordingAppLifecycleGateway();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('luobo').first);
    await tester.pumpAndSettle();

    expect(find.text('我的'), findsWidgets);
    expect(find.text('正在阅读公开主页 user-9'), findsOneWidget);
    expect(find.text('用户 user-9'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('论坛详情回流'), findsOneWidget);
    expect(find.text('浏览公开帖子，支持最新和热门排序。当前阶段仅提供只读阅读。'), findsOneWidget);
  });

  testWidgets('shell forum handoff opens native detail and targets comment',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        initialForumHandoffTarget: const ForumDetailHandoffTarget(
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          commentId: 'reply-1',
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('First public child comment'), findsOneWidget);
  });

  testWidgets(
      'profile comment handoff reuses shared native forum detail target',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: scrollable,
    );
    await tester.drag(scrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('/forum/post/post-42'), findsOneWidget);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);
  });

  testWidgets('profile comment reply returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final profileScrollable = find.byType(Scrollable).last;
    final openCommentButton = find.widgetWithText(FilledButton, '打开评论上下文');
    await tester.scrollUntilVisible(
      openCommentButton,
      200,
      scrollable: profileScrollable,
    );
    await tester.drag(profileScrollable, const Offset(0, -240));
    await tester.pumpAndSettle();
    await tester.tap(openCommentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('论坛详情回流'), findsWidgets);
    expect(find.text('个人主页评论'), findsWidgets);
    expect(find.text('First public child comment'), findsOneWidget);

    await tester.tap(find.widgetWithText(TextButton, '回复').first);
    await tester.pumpAndSettle();
    await tester.enterText(
      _forumCommentTextField(hintText: '写下你的回复...'),
      '从个人主页评论回复',
    );
    await tester.pump();
    final replyButton = find.widgetWithText(FilledButton, '发布回复');
    await tester.ensureVisible(replyButton);
    await tester.tap(replyButton);
    await tester.pumpAndSettle();

    expect(find.text('回复已发布，已更新当前评论区。'), findsOneWidget);
    expect(find.text('从个人主页评论回复'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('最近公开评论'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('profile post handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    final openPostButton = find.widgetWithText(FilledButton, '打开帖子');
    await tester.scrollUntilVisible(
      openPostButton,
      200,
      scrollable: scrollable,
    );
    await tester.tap(openPostButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('个人主页帖子'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近公开帖子'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('profile quick reply handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('回到原帖'),
      200,
      scrollable: scrollable,
    );
    expect(find.text('原帖回流'), findsWidgets);

    await tester.tap(find.text('回到原帖'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('我的轻回应'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('我的轻回应'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets(
      'profile recent browse handoff returns to profile after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(
          initialRecentBrowseHandoff: const ForumDetailHandoffTarget(
            postId: 'post-42',
            source: ForumDetailHandoffSource.browseHistory,
            initialTitle: '论坛详情回流',
          ),
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).last;
    await tester.scrollUntilVisible(
      find.text('最近阅读'),
      200,
      scrollable: scrollable,
    );
    await tester.tap(find.text('继续阅读帖子'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('我的最近阅读'), findsWidgets);

    Navigator.of(tester.element(find.text('帖子详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近阅读'), findsOneWidget);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('帖子详情'), findsNothing);
  });

  testWidgets('discover document handoff returns to discover after detail pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final docsFollowUpStore = InMemoryDocsFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDocumentDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: docsFollowUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);

    Navigator.of(tester.element(find.text('文档详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('发现'), findsWidgets);
    expect(find.text('文档精选'), findsOneWidget);
    expect(find.text('文档详情'), findsNothing);

    final recentTarget = await docsFollowUpStore.readRecentDocumentTarget();
    expect(recentTarget?.slug, 'flutter-docs-scope');
    expect(recentTarget?.source, DocsDetailHandoffSource.browseHistory);
  });

  testWidgets('docs detail link opens another native docs detail route',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededDocumentDiscoverRepository(),
        docsRepository: _LinkedDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pumpAndSettle();

    final linkedDocsAction = find.text('公开阅读边界').last;
    await tester.ensureVisible(linkedDocsAction);
    await tester.tap(linkedDocsAction);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：文档内链'), findsOneWidget);
    expect(find.text('Doc public-docs-reading-boundary'), findsWidgets);

    await tester.tap(find.text('返回来源').last);
    await tester.pumpAndSettle();

    expect(find.text('打开来源：发现'), findsOneWidget);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);
  });

  testWidgets('docs tab search opens native docs detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );
    final lifecycleGateway = _RecordingAppLifecycleGateway();

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _SearchableDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(),
        appLifecycleGateway: lifecycleGateway,
      ),
    );

    await tester.pump();
    await tester.tap(find.text('文档'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'boundary');
    await tester.tap(find.text('搜索文档'));
    await tester.pumpAndSettle();

    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('打开文档'),
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(find.text('打开文档'));
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('Doc public-docs-reading-boundary'), findsWidgets);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(lifecycleGateway.moveTaskToBackCallCount, 0);
    expect(find.text('“boundary” 共 1 篇文档'), findsOneWidget);
    expect(find.text('Public docs reading boundary'), findsOneWidget);
    expect(find.text('Doc public-docs-reading-boundary'), findsNothing);
  });

  testWidgets('profile recent document handoff returns to profile after pop',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _FakeForumRepository(),
        profileRepository: _SeededProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(
          initialRecentDocumentTarget: const DocsDetailHandoffTarget(
            slug: 'flutter-docs-scope',
            source: DocsDetailHandoffSource.browseHistory,
            initialTitle: 'Radish Flutter docs scope',
          ),
        ),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();
    await tester.tap(find.text('我的'));
    await tester.pumpAndSettle();

    final recentDocumentButton =
        find.widgetWithText(FilledButton, '继续阅读文档').last;
    await tester.scrollUntilVisible(
      recentDocumentButton,
      200,
      scrollable: find.byType(Scrollable).last,
    );
    await tester.tap(recentDocumentButton);
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('文档详情'), findsWidgets);
    expect(find.text('我的最近文档'), findsWidgets);
    expect(find.text('Doc flutter-docs-scope'), findsWidgets);

    Navigator.of(tester.element(find.text('文档详情').first)).pop();
    await tester.pumpAndSettle();

    expect(find.text('最近文档'), findsWidgets);
    expect(find.text('User user-42'), findsOneWidget);
    expect(find.text('文档详情'), findsNothing);
  });

  testWidgets('pending notification handoff opens shared native forum detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore(
      initialPendingHandoff: const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: 'Native discover wiring plan',
      ),
    );
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _SeededBigIdDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
  });

  testWidgets('recent forum notification list opens shared native detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: const _FakeForumNotificationRepository(),
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    await tester.tap(find.text('我的').last);
    await tester.pumpAndSettle();
    expect(find.text('已登录用户 user-42'), findsOneWidget);
    expect(find.text('通知 3 条'), findsOneWidget);

    await tester.tap(find.text('通知 3 条'));
    await tester.pumpAndSettle();

    expect(find.text('系统维护'), findsOneWidget);
    expect(find.text('只读'), findsOneWidget);
    expect(find.text('帖子被评论'), findsOneWidget);
    expect(find.text('帖子收到轻回应'), findsOneWidget);

    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
    expect(find.text('Big id root comment'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('已登录用户 user-42'), findsOneWidget);
    expect(find.text('通知 3 条'), findsOneWidget);
  });

  testWidgets('forum notification chip explains failure empty and refresh',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final notificationRepository = _MutableForumNotificationRepository()
      ..error = StateError('notification service unavailable');
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: 'user-42',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: 'user-42',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: InMemoryForumFollowUpStore(),
        notificationRepository: notificationRepository,
      ),
    );

    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('通知刷新失败'), findsOneWidget);
    expect(find.text('刷新通知'), findsOneWidget);
    expect(notificationRepository.callCount, 1);

    notificationRepository.error = null;
    await tester.tap(find.text('刷新通知'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('暂无通知'), findsOneWidget);
    expect(find.text('刷新通知'), findsOneWidget);
    expect(notificationRepository.callCount, 2);

    notificationRepository.target = const ForumDetailHandoffTarget(
      postId: '2042219067430928384',
      source: ForumDetailHandoffSource.notification,
      initialTitle: '帖子被评论',
      commentId: 'comment-big-1',
    );
    await tester.tap(find.text('刷新通知'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('通知 1 条'), findsOneWidget);
    expect(notificationRepository.callCount, 3);

    await tester.tap(find.text('通知 1 条'));
    await tester.pumpAndSettle();

    expect(find.text('帖子被评论'), findsOneWidget);

    await tester.tap(find.text('帖子被评论'));
    await tester.pumpAndSettle();

    expect(find.text('帖子详情'), findsWidgets);
    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('通知回流'), findsWidgets);
  });

  testWidgets('recent browse handoff resumes forum detail from shell chip',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final followUpStore = InMemoryForumFollowUpStore();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _FakeSessionRefreshService.missing(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        discoverRepository: _FakeDiscoverRepository(),
        docsRepository: _FakeDocsRepository(),
        forumRepository: _SeededBigIdForumRepository(),
        profileRepository: _FakeProfileRepository(),
        followUpStore: followUpStore,
      ),
    );

    await tester.pump();
    await tester.tap(find.text('论坛'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('查看详情'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('继续阅读论坛'), findsOneWidget);
    await tester.tap(find.text('继续阅读论坛'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('/forum/post/2042219067430928384'), findsOneWidget);
    expect(find.text('继续阅读'), findsWidgets);
  });

  test('recent browse store keeps newest deduplicated targets', () async {
    final followUpStore = InMemoryForumFollowUpStore();

    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-1',
        source: ForumDetailHandoffSource.shell,
        initialTitle: 'First post',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-2',
        source: ForumDetailHandoffSource.publicProfilePost,
        initialTitle: 'Second post',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-1',
        source: ForumDetailHandoffSource.publicProfileComment,
        initialTitle: 'First comment',
        commentId: 'comment-1',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'post-2',
        source: ForumDetailHandoffSource.discover,
        initialTitle: 'Second post refreshed',
      ),
    );

    final targets = await followUpStore.readRecentBrowseHandoffs();

    expect(targets.map((target) => target.postId), [
      'post-2',
      'post-1',
      'post-1',
    ]);
    expect(targets.first.initialTitle, 'Second post refreshed');
    expect(targets.first.source, ForumDetailHandoffSource.browseHistory);
    expect(targets[1].commentId, 'comment-1');
    expect(await followUpStore.readRecentBrowseHandoff(), targets.first);
  });

  test('recent document store keeps newest deduplicated targets', () async {
    final followUpStore = InMemoryDocsFollowUpStore();

    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'flutter-docs-scope',
        source: DocsDetailHandoffSource.discover,
        initialTitle: 'Flutter docs scope',
      ),
    );
    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'public-docs-reading-boundary',
        source: DocsDetailHandoffSource.docsList,
        initialTitle: 'Public docs reading boundary',
      ),
    );
    await followUpStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(
        slug: 'flutter-docs-scope',
        source: DocsDetailHandoffSource.docsLink,
        initialTitle: 'Flutter docs scope refreshed',
      ),
    );

    final targets = await followUpStore.readRecentDocumentTargets();

    expect(targets.map((target) => target.slug), [
      'flutter-docs-scope',
      'public-docs-reading-boundary',
    ]);
    expect(targets.first.initialTitle, 'Flutter docs scope refreshed');
    expect(targets.first.source, DocsDetailHandoffSource.browseHistory);
    expect(await followUpStore.readRecentDocumentTarget(), targets.first);
  });
}

NativeAuthController _buildAuthController(
  SessionController sessionController, {
  NativeAuthCallbackPayload? pendingCallback,
  AuthSession? nextSession,
  String? exchangeFailureMessage,
}) {
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: InMemoryNativeAuthGateway(
      initialPendingCallback: pendingCallback,
    ),
    exchangeService: _FakeAuthorizationCodeExchangeService(
      nextSession: nextSession,
      failureMessage: exchangeFailureMessage,
    ),
  );
}

class _FakeDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [],
      documents: [],
      products: [],
    );
  }
}

class _SeededDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: 'post-1',
          title: 'Native discover',
          summary: 'Use discover to jump into real tabs.',
          categoryId: 'forum-cat-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
        ),
      ],
      documents: [],
      products: [
        DiscoverProductSummary(
          id: 'product-4001',
          name: 'Profile Rename Card',
          productType: 'Consumable',
          price: 120,
          soldCount: 3,
          durationDisplay: '永久',
        ),
      ],
    );
  }
}

class _SeededShopRepository implements ShopRepository {
  const _SeededShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopProductPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      products: [
        ShopProductSummary(
          id: 'product-4001',
          name: 'Profile Rename Card',
          productType: '消耗品',
          price: 120,
          originalPrice: 180,
          hasDiscount: true,
          soldCount: 3,
          durationDisplay: '永久',
          inStock: true,
        ),
      ],
    );
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) async {
    return const ShopProductDetail(
      id: 'product-4001',
      name: 'Profile Rename Card',
      description: 'Use this read-only detail to confirm the item scope.',
      categoryName: 'Profile tools',
      productType: '消耗品',
      benefitValue: 'rename-card',
      price: 120,
      originalPrice: 180,
      hasDiscount: true,
      stockType: 'Unlimited',
      stock: 0,
      soldCount: 3,
      limitPerUser: 1,
      inStock: true,
      durationDisplay: '永久',
      isOnSale: true,
      isEnabled: true,
    );
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopOrderPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      orders: [
        ShopOrderSummary(
          id: '9001',
          orderNo: 'RO202605310001',
          productName: 'Profile Rename Card',
          quantity: 1,
          totalPrice: 120,
          status: 'Completed',
          statusDisplay: '已完成',
          createTime: '2026-05-31T08:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    return const ShopOrderDetail(
      id: '9001',
      orderNo: 'RO202605310001',
      productId: '4001',
      productName: 'Profile Rename Card',
      productType: 'Consumable',
      productTypeDisplay: '消耗品',
      quantity: 1,
      unitPrice: 120,
      totalPrice: 120,
      status: 'Completed',
      statusDisplay: '已完成',
      durationDisplay: '永久',
      createTime: '2026-05-31T08:00:00Z',
      paidTime: '2026-05-31T08:00:30Z',
      completedTime: '2026-05-31T08:01:00Z',
    );
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const [
      ShopUserBenefit(
        id: 'benefit-1',
        benefitType: 'Badge',
        benefitTypeDisplay: '徽章',
        benefitName: '早鸟徽章',
        sourceType: 'Purchase',
        sourceTypeDisplay: '购买',
        sourceOrderId: '9001',
        sourceProductId: '4001',
        durationDisplay: '永久',
        isActive: true,
        isExpired: false,
        createTime: '2026-05-31T08:05:00Z',
      ),
    ];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return const [
      ShopInventoryItem(
        id: 'inventory-1',
        consumableType: 'RenameCard',
        consumableTypeDisplay: '改名卡',
        itemName: 'Profile Rename Card',
        itemValue: 'rename-card',
        sourceProductId: '4001',
        quantity: 1,
        createTime: '2026-05-31T08:06:00Z',
      ),
    ];
  }
}

class _SeededWalletRepository implements WalletRepository {
  const _SeededWalletRepository();

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) async {
    return const CoinBalance(
      userId: 'user-42',
      balance: 1200,
      balanceDisplay: '1.200',
      frozenBalance: 100,
      frozenBalanceDisplay: '0.100',
      totalEarned: 1800,
      totalSpent: 600,
      totalTransferredIn: 0,
      totalTransferredOut: 0,
      createTime: '2026-05-30T08:00:00Z',
      modifyTime: '2026-05-31T09:00:00Z',
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const CoinTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      transactions: [
        CoinTransaction(
          id: 'coin-1',
          transactionNo: 'CT202605310001',
          fromUserId: null,
          fromUserName: null,
          toUserId: 'user-42',
          toUserName: 'user-42',
          amount: 1800,
          amountDisplay: '1.800',
          fee: 0,
          feeDisplay: '0.000',
          transactionType: 'SYSTEM_GRANT',
          transactionTypeDisplay: '系统赠送',
          status: 'SUCCESS',
          statusDisplay: '成功',
          remark: '新账号奖励',
          createTime: '2026-05-31T08:00:00Z',
        ),
        CoinTransaction(
          id: 'coin-2',
          transactionNo: 'CT202605310002',
          fromUserId: 'user-42',
          fromUserName: 'user-42',
          toUserId: null,
          toUserName: null,
          amount: 600,
          amountDisplay: '0.600',
          fee: 0,
          feeDisplay: '0.000',
          transactionType: 'CONSUME',
          transactionTypeDisplay: '商城消费',
          status: 'SUCCESS',
          statusDisplay: '成功',
          businessType: 'Order',
          businessId: '9001',
          remark: '购买 Profile Rename Card',
          createTime: '2026-05-31T08:30:00Z',
        ),
      ],
    );
  }
}

class _SeededExperienceRepository implements ExperienceRepository {
  const _SeededExperienceRepository();

  @override
  Future<UserExperience> getMyExperience({
    required String accessToken,
  }) async {
    return const UserExperience(
      userId: 'user-42',
      userName: 'user-42',
      currentLevel: 3,
      currentLevelName: '练气',
      currentExp: 240,
      totalExp: 1240,
      expToNextLevel: 260,
      nextLevel: 4,
      nextLevelName: '筑基',
      levelProgress: 0.48,
      expFrozen: false,
      levelUpAt: '2026-05-30T08:00:00Z',
      rank: 12,
    );
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ExperienceTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      transactions: [
        ExperienceTransaction(
          id: 'exp-1',
          userId: 'user-42',
          userName: 'user-42',
          operatorId: '0',
          operatorName: 'system',
          expType: 'POST_CREATE',
          expTypeDisplay: '发帖奖励',
          expAmount: 20,
          businessType: 'Post',
          businessId: 'post-1',
          remark: '发布公开帖子',
          expBefore: 1220,
          expAfter: 1240,
          levelBefore: 3,
          levelAfter: 3,
          isLevelUp: false,
          createTime: '2026-05-31T08:00:00Z',
        ),
        ExperienceTransaction(
          id: 'exp-2',
          userId: 'user-42',
          userName: 'user-42',
          operatorId: '0',
          operatorName: 'system',
          expType: 'COMMENT_CREATE',
          expTypeDisplay: '评论奖励',
          expAmount: 10,
          businessType: 'Comment',
          businessId: 'comment-1',
          remark: '发布公开评论',
          expBefore: 1210,
          expAfter: 1220,
          levelBefore: 2,
          levelAfter: 3,
          isLevelUp: true,
          createTime: '2026-05-31T07:30:00Z',
        ),
      ],
    );
  }
}

class _SeededDocumentDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [],
      documents: [
        DocsDocumentSummary(
          id: 'doc-3001',
          title: 'Radish Flutter docs scope',
          slug: 'flutter-docs-scope',
          summary: 'Read-only docs route handoff.',
          modifyTime: '2026-04-20T08:00:00Z',
        ),
      ],
      products: [],
    );
  }
}

class _SeededBigIdDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Use discover to jump into real tabs.',
          categoryId: 'forum-cat-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
        ),
      ],
      documents: [],
      products: [],
    );
  }
}

class _FakeDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    return const DocsDocumentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      documents: [],
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    return DocsDocumentDetail(
      id: 'doc-$slug',
      title: 'Doc $slug',
      slug: slug,
      markdownContent: '# $slug',
      sourceType: 'Markdown',
      visibility: 1,
      status: 1,
      createTime: '2026-04-20T08:00:00Z',
    );
  }
}

class _SearchableDocsRepository extends _FakeDocsRepository {
  static const _documents = [
    DocsDocumentSummary(
      id: 'doc-flutter-docs-scope',
      title: 'Radish Flutter docs scope',
      slug: 'flutter-docs-scope',
      summary: 'Native Flutter docs wiring.',
    ),
    DocsDocumentSummary(
      id: 'doc-public-docs-reading-boundary',
      title: 'Public docs reading boundary',
      slug: 'public-docs-reading-boundary',
      summary: 'Keep editing outside the native docs search batch.',
    ),
  ];

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    final normalizedKeyword = keyword?.trim().toLowerCase();
    final documents = _documents.where((document) {
      if (normalizedKeyword == null || normalizedKeyword.isEmpty) {
        return true;
      }

      return document.title.toLowerCase().contains(normalizedKeyword) ||
          document.slug.toLowerCase().contains(normalizedKeyword) ||
          (document.summary?.toLowerCase().contains(normalizedKeyword) ??
              false);
    }).toList();

    return DocsDocumentPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: documents.length,
      pageCount: 1,
      documents: documents,
    );
  }
}

class _LinkedDocsRepository extends _FakeDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    if (slug == 'flutter-docs-scope') {
      return DocsDocumentDetail(
        id: 'doc-$slug',
        title: 'Doc $slug',
        slug: slug,
        markdownContent:
            '# $slug\n继续阅读 [公开阅读边界](/docs/public-docs-reading-boundary)',
        sourceType: 'Markdown',
        visibility: 1,
        status: 1,
        createTime: '2026-04-20T08:00:00Z',
      );
    }

    return super.getDocumentDetail(slug: slug);
  }
}

class _FakeForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: 'Post $postId',
      summary: 'Summary for $postId',
      content: '# Post $postId\n\nBody content',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-1',
      authorName: 'tester',
      tagNames: const ['flutter'],
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 0,
      items: [],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T08:13:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }
}

class _SeededForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          categoryId: 'category-1',
          categoryName: 'General',
          authorId: 'user-9',
          authorName: 'luobo',
          commentCount: 3,
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: '论坛详情回流',
      summary: 'Tap through to the public native detail page.',
      content:
          '# Native detail\n\n- author metadata\n- body content\n- back navigation',
      contentType: 'Markdown',
      categoryId: 'category-1',
      categoryName: 'General',
      authorId: 'user-9',
      authorName: 'luobo',
      tagNames: const ['android', 'flutter'],
      commentCount: 3,
      createTime: '2026-04-20T08:00:00Z',
      updateTime: '2026-04-20T10:30:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    if (pageIndex == 1) {
      return const ForumCommentPage(
        page: 1,
        pageSize: 20,
        dataCount: 2,
        pageCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'comment-1',
            postId: 'post-42',
            content: 'First public root comment',
            authorId: 'user-9',
            authorName: 'luobo',
            likeCount: 2,
            replyCount: 1,
            childrenTotal: 1,
            createTime: '2026-04-20T11:00:00Z',
          ),
          ForumCommentSummary(
            id: 'comment-2',
            postId: 'post-42',
            content: 'Second public root comment',
            authorId: 'user-10',
            authorName: 'reader',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:05:00Z',
          ),
        ],
      );
    }

    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      comments: [],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (parentId == 'comment-1') {
      return const ForumChildCommentPage(
        pageIndex: 1,
        pageSize: 5,
        totalCount: 1,
        comments: [
          ForumCommentSummary(
            id: 'reply-1',
            postId: 'post-42',
            content: 'First public child comment',
            authorId: 'user-11',
            authorName: 'guest-child',
            parentId: 'comment-1',
            rootId: 'comment-1',
            replyToUserName: 'luobo',
            replyToCommentSnapshot: 'First public root comment',
            createTime: '2026-04-20T11:10:00Z',
          ),
        ],
      );
    }

    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    if (commentId == 'reply-1') {
      return const ForumCommentNavigationLocation(
        commentId: 'reply-1',
        postId: 'post-42',
        rootCommentId: 'comment-1',
        parentCommentId: 'comment-1',
        isRootComment: false,
        rootPageIndex: 1,
        childPageIndex: 1,
      );
    }

    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: 'comment-1',
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 2,
      items: [
        ForumQuickReplySummary(
          id: 'quick-1',
          postId: 'post-42',
          authorId: 'user-9',
          authorName: 'luobo',
          content: '学到了',
          createTime: '2026-04-20T11:20:00Z',
        ),
        ForumQuickReplySummary(
          id: 'quick-2',
          postId: 'post-42',
          authorId: 'user-10',
          authorName: 'reader',
          content: '同感',
          createTime: '2026-04-20T11:25:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-created',
      postId: postId,
      authorId: 'user-current',
      authorName: 'current',
      content: content,
      createTime: '2026-04-20T11:30:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }
}

class _SeededBigIdForumRepository implements ForumRepository {
  @override
  Future<ForumPostPage> getPostPage({
    required int pageIndex,
    required int pageSize,
    required ForumFeedSort sort,
  }) async {
    return const ForumPostPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      posts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Connect real summaries without expanding into details.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          authorName: 'luobo',
          commentCount: 6,
          viewCount: 128,
          isEssence: true,
        ),
      ],
    );
  }

  @override
  Future<ForumPostDetail> getPostDetail({
    required String postId,
  }) async {
    return ForumPostDetail(
      id: postId,
      publicId: postId,
      title: 'Native discover wiring plan',
      summary: 'Connect real summaries without expanding into details.',
      content: '# Big id detail\n\nPreserve string ids through native handoff.',
      contentType: 'Markdown',
      categoryId: '9',
      categoryName: 'Engineering',
      authorId: '1024',
      authorName: 'luobo',
      commentCount: 6,
      viewCount: 128,
      isEssence: true,
      createTime: '2026-04-18T10:00:00Z',
    );
  }

  @override
  Future<ForumCommentPage> getRootCommentsPage({
    required String postId,
    required int pageIndex,
    required int pageSize,
    String sortBy = 'default',
  }) async {
    return const ForumCommentPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      comments: [
        ForumCommentSummary(
          id: 'comment-big-1',
          postId: '2042219067430928384',
          content: 'Big id root comment',
          authorId: '2048',
          authorName: 'reader',
          createTime: '2026-04-18T12:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumChildCommentPage> getChildCommentsPage({
    required String parentId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ForumChildCommentPage(
      pageIndex: 1,
      pageSize: 5,
      totalCount: 0,
      comments: [],
    );
  }

  @override
  Future<ForumCommentNavigationLocation> getCommentNavigation({
    required String postId,
    required String commentId,
    required int rootPageSize,
    required int childPageSize,
  }) async {
    return ForumCommentNavigationLocation(
      commentId: commentId,
      postId: postId,
      rootCommentId: commentId,
      isRootComment: true,
      rootPageIndex: 1,
    );
  }

  @override
  Future<ForumQuickReplyWall> getQuickReplyWall({
    required String postId,
    int take = 30,
  }) async {
    return const ForumQuickReplyWall(
      total: 1,
      items: [
        ForumQuickReplySummary(
          id: 'quick-big-1',
          postId: '2042219067430928384',
          authorId: '2048',
          authorName: 'reader',
          content: '已读',
          createTime: '2026-04-18T12:05:00Z',
        ),
      ],
    );
  }

  @override
  Future<ForumQuickReplySummary> createQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) async {
    return ForumQuickReplySummary(
      id: 'quick-big-created',
      postId: postId,
      authorId: 'current-user',
      authorName: 'current',
      content: content,
      createTime: '2026-04-18T12:20:00Z',
    );
  }

  @override
  Future<String> createComment({
    required String postId,
    required String content,
    required String accessToken,
    String? parentId,
    String? replyToCommentId,
    String? replyToCommentSnapshot,
    String? replyToUserName,
  }) async {
    return 'comment-created';
  }
}

class _FakeProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 0,
      commentCount: 0,
      totalLikeCount: 0,
      postLikeCount: 0,
      commentLikeCount: 0,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 0,
      pageCount: 1,
      posts: [],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfileCommentPage(
      page: 1,
      pageSize: 3,
      dataCount: 0,
      pageCount: 1,
      comments: [],
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserQuickReplyPage(
      page: 1,
      pageSize: 3,
      total: 0,
      items: [],
    );
  }

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserBrowseHistoryPage(
      page: 1,
      pageSize: 20,
      total: 0,
      items: [],
    );
  }
}

class _SeededLeaderboardRepository implements LeaderboardRepository {
  const _SeededLeaderboardRepository();

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
          userId: 'user-9',
          userName: 'luobo',
          currentLevel: 8,
          currentLevelName: '探索者',
          primaryValue: '18888',
          primaryLabel: '总经验值',
        ),
      ],
    );
  }
}

class _FakeForumNotificationRepository implements NotificationRepository {
  const _FakeForumNotificationRepository();

  @override
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const NotificationPage(
      notifications: [
        NotificationListItem(
          id: 'notification-system',
          notificationId: 'system-1',
          notification: NotificationPayload(
            title: '系统维护',
            content: '今晚 23:00 进行维护。',
            type: 'System',
            businessType: 'System',
            createdAt: null,
            extData: {'app': 'system'},
          ),
          isRead: false,
          createdAt: '2026-05-31T08:30:00',
        ),
        NotificationListItem(
          id: 'notification-forum-comment',
          notificationId: 'forum-1',
          notification: NotificationPayload(
            title: '帖子被评论',
            content: '有人评论了你的帖子。',
            type: 'CommentReplied',
            businessType: 'Comment',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
              'commentId': 'comment-big-1',
            },
          ),
          isRead: false,
          createdAt: '2026-05-31T08:31:00',
        ),
        NotificationListItem(
          id: 'notification-forum-like',
          notificationId: 'forum-2',
          notification: NotificationPayload(
            title: '帖子收到轻回应',
            content: '你的帖子收到新的轻回应。',
            type: 'PostLiked',
            businessType: 'Post',
            createdAt: null,
            extData: {
              'app': 'forum',
              'postId': '2042219067430928384',
            },
          ),
          isRead: true,
          createdAt: '2026-05-31T08:32:00',
        ),
      ],
    );
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const [
      ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: '帖子被评论',
        commentId: 'comment-big-1',
      ),
      ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.notification,
        initialTitle: '帖子收到轻回应',
      ),
    ];
  }
}

class _MutableForumNotificationRepository implements NotificationRepository {
  ForumDetailHandoffTarget? target;
  Object? error;
  int callCount = 0;

  @override
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return NotificationPage.fromForumTargets(targets);
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.isEmpty ? null : targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    callCount += 1;
    final error = this.error;
    if (error != null) {
      throw error;
    }

    final target = this.target;
    return target == null ? const <ForumDetailHandoffTarget>[] : [target];
  }
}

class _RecordingAppLifecycleGateway implements AppLifecycleGateway {
  int moveTaskToBackCallCount = 0;

  @override
  Future<void> moveTaskToBack() async {
    moveTaskToBackCallCount += 1;
  }
}

class _SeededProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'user-$userId',
      displayName: 'User $userId',
      createTime: '2026-04-20T08:00:00Z',
    );
  }

  @override
  Future<PublicProfileStats> getPublicStats({
    required String userId,
  }) async {
    return const PublicProfileStats(
      postCount: 1,
      commentCount: 1,
      totalLikeCount: 3,
      postLikeCount: 1,
      commentLikeCount: 2,
    );
  }

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfilePostPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      posts: [
        PublicProfilePostSummary(
          id: 'post-42',
          title: '论坛详情回流',
          summary: 'Tap through to the public native detail page.',
          content: 'Tap through to the public native detail page.',
          categoryName: 'General',
          viewCount: 128,
          likeCount: 16,
          commentCount: 3,
          createTime: '2026-04-20T08:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const PublicProfileCommentPage(
      page: 1,
      pageSize: 3,
      dataCount: 1,
      pageCount: 1,
      comments: [
        PublicProfileCommentSummary(
          id: 'reply-1',
          postId: 'post-42',
          content: 'Recent public comments should stay readable in the shell.',
          likeCount: 5,
          createTime: '2026-04-20T09:00:00Z',
          replyToUserName: 'luobo',
          replyToCommentSnapshot: 'First public root comment',
        ),
      ],
    );
  }

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) async {
    return const UserQuickReplyPage(
      page: 1,
      pageSize: 3,
      total: 1,
      items: [
        UserQuickReplySummary(
          id: 'quick-42',
          postId: 'post-42',
          postTitle: '论坛详情回流',
          content: '这个回流很好用',
          createTime: '2026-04-20T09:10:00Z',
        ),
      ],
    );
  }

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
          id: 'history-post-42',
          targetType: 'Post',
          targetTypeDisplay: '帖子',
          targetId: '42',
          title: '论坛详情回流',
          routePath: '/forum/post/post-42',
          viewCount: 3,
          lastViewTime: '2026-04-20T09:20:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-docs-42',
          targetType: 'Wiki',
          targetTypeDisplay: '文档',
          targetId: '43',
          targetSlug: 'native-docs',
          title: 'Native docs',
          routePath: '/docs/native-docs',
          viewCount: 2,
          lastViewTime: '2026-04-20T09:10:00Z',
        ),
        UserBrowseHistoryItem(
          id: 'history-product-42',
          targetType: 'Product',
          targetTypeDisplay: '商品',
          targetId: '1001',
          title: 'Early Access Badge',
          routePath: '/shop/products/1001',
          viewCount: 1,
          lastViewTime: '2026-04-20T09:00:00Z',
        ),
      ],
    );
  }
}

class _FakeSessionRefreshService extends SessionRefreshService {
  _FakeSessionRefreshService.missing()
      : _nextSession = null,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.success(AuthSession nextSession)
      : _nextSession = nextSession,
        _failureMessage = null,
        super(environment: const AppEnvironment.development());

  _FakeSessionRefreshService.failure(String failureMessage)
      : _nextSession = null,
        _failureMessage = failureMessage,
        super(environment: const AppEnvironment.development());

  final AuthSession? _nextSession;
  final String? _failureMessage;

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    final failureMessage = _failureMessage;
    if (failureMessage != null) {
      throw SessionRefreshException(failureMessage);
    }

    final nextSession = _nextSession;
    if (nextSession != null) {
      return nextSession;
    }

    return session;
  }
}

class _FakeAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  const _FakeAuthorizationCodeExchangeService({
    this.nextSession,
    this.failureMessage,
  });

  final AuthSession? nextSession;
  final String? failureMessage;

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
  }) async {
    final failureMessage = this.failureMessage;
    if (failureMessage != null) {
      throw AuthorizationCodeExchangeException(failureMessage);
    }

    final nextSession = this.nextSession;
    if (nextSession == null) {
      throw const AuthorizationCodeExchangeException(
        'No fake authorization-code session was configured.',
      );
    }

    return nextSession;
  }
}

String _buildJwt({
  required String userId,
  required DateTime expiresAt,
}) {
  final header = base64Url.encode(utf8.encode('{"alg":"none","typ":"JWT"}'));
  final payload = base64Url.encode(
    utf8.encode(
      '{"sub":"$userId","exp":${expiresAt.toUtc().millisecondsSinceEpoch ~/ 1000}}',
    ),
  );
  return '$header.$payload.signature';
}
