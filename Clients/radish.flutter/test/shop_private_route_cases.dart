part of 'shop_product_detail_page_test.dart';

void registerShopPrivateRouteCases() {
  testWidgets('order detail refresh failure keeps current order context',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final repository = _RefreshFailingOrderRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: ShopOrderDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          walletRepository: const EmptyWalletRepository(),
          accessToken: 'access-token',
          orderId: '9001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('订单 RO202605310001'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);

    await tester.tap(find.text('刷新订单'));
    await tester.pumpAndSettle();

    expect(find.text('订单详情刷新失败'), findsOneWidget);
    expect(find.text('订单 RO202605310001'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('正在查看订单 RO202605310001'), findsOneWidget);
  });

  testWidgets('order detail source actions require canonical LongId strings',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopOrderDetailPage(
          environment: AppEnvironment.development(),
          repository: _NonCanonicalOrderReferenceRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          orderId: '9001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('订单 RO202605310001'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, '查看商品'), findsNothing);
    expect(find.widgetWithText(FilledButton, '查看扣款流水'), findsNothing);
    expect(find.widgetWithText(FilledButton, '查看背包发放'), findsOneWidget);
  });

  testWidgets('order detail inventory entry keeps return on load failure',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopOrderDetailPage(
          environment: AppEnvironment.development(),
          repository: _OrderInventoryFailingRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          orderId: '9001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('订单 RO202605310001'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '查看背包发放'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '查看背包发放'));
    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载背包'), findsOneWidget);
    expect(find.text('背包发放暂时不可用'), findsOneWidget);
    expect(find.text('来源：订单详情'), findsOneWidget);
    expect(find.text('返回订单详情'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('订单详情'), findsWidgets);
    expect(find.text('订单 RO202605310001'), findsOneWidget);
  });

  testWidgets('inventory source failures show explicit state and return',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopInventoryPage(
          environment: AppEnvironment.development(),
          repository: _BrokenInventorySourceRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
    expect(find.text('查看来源订单'), findsOneWidget);
    expect(find.text('查看来源商品'), findsWidgets);

    await tester.tap(find.text('查看来源订单'));
    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载订单详情'), findsOneWidget);
    expect(find.text('来源订单不存在'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);

    await tester.tap(find.text('查看来源商品').first);
    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载商品详情'), findsOneWidget);
    expect(find.text('来源商品不存在'), findsOneWidget);
    expect(find.text('返回背包'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
  });

  testWidgets('inventory source actions require canonical LongId strings',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopInventoryPage(
          environment: AppEnvironment.development(),
          repository: _NonCanonicalInventorySourceRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
    expect(find.text('非规范来源权益'), findsOneWidget);
    expect(find.text('非规范来源道具'), findsOneWidget);
    expect(find.text('查看来源订单'), findsNothing);
    expect(find.text('查看来源商品'), findsNothing);
  });

  testWidgets('wallet order filter empty and refresh failure keep context',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final repository = _EmptyThenRefreshFailingWalletRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: WalletPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          accessToken: 'access-token',
          title: '订单扣款流水',
          returnLabel: '返回订单详情',
          transactionType: 'CONSUME',
          businessType: 'Order',
          businessId: '9001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('订单扣款流水'), findsWidgets);
    expect(find.text('返回订单详情'), findsOneWidget);
    expect(find.text('当前筛选：Order #9001'), findsOneWidget);
    expect(find.text('当前订单暂无匹配的胡萝卜流水。'), findsOneWidget);
    expect(find.text('已加载 0 / 0 条流水'), findsOneWidget);

    await tester.tap(find.text('刷新资产'));
    await tester.pumpAndSettle();

    expect(find.text('刷新失败：资产刷新失败'), findsOneWidget);
    expect(find.text('当前筛选：Order #9001'), findsOneWidget);
    expect(find.text('当前订单暂无匹配的胡萝卜流水。'), findsOneWidget);
    expect(find.text('880 胡萝卜'), findsOneWidget);
  });
}
