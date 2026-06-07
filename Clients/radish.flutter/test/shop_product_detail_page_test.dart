import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_inventory_page.dart';
import 'package:radish_flutter/features/shop/presentation/shop_order_detail_page.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_detail_page.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';
import 'package:radish_flutter/features/wallet/presentation/wallet_page.dart';

void main() {
  testWidgets('renders public shop product detail with login purchase boundary',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final clipboard = _ClipboardRecorder()..install();
    addTearDown(clipboard.reset);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _SuccessShopRepository(),
          walletRepository: EmptyWalletRepository(),
          productId: '4001',
          initialTitle: 'Profile Rename Card',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('公开商品详情'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);
    expect(
      find.text('https://localhost:5000/shop/product/4001'),
      findsOneWidget,
    );
    expect(find.text('Profile Rename Card'), findsWidgets);
    expect(find.text('120 胡萝卜'), findsOneWidget);
    expect(find.text('单商品购买'), findsOneWidget);
    expect(find.text('登录后购买'), findsOneWidget);
    expect(
      find.text('本批只支持当前商品直接购买 1 件；购物车、退款、权益使用和完整移动商城不在本次范围。'),
      findsOneWidget,
    );

    await tester.tap(find.text('复制公开链接'));
    await tester.pump();

    expect(clipboard.text, 'https://localhost:5000/shop/product/4001');
    expect(find.text('公开链接已复制'), findsOneWidget);
  });

  testWidgets('renders shop detail error state', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _FailingShopRepository(),
          walletRepository: EmptyWalletRepository(),
          productId: '4001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载商品详情'), findsOneWidget);
    expect(find.text('商品不存在'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });

  testWidgets('authenticated user purchases product and opens order result',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _PurchaseInventoryShopRepository(),
          walletRepository: _OrderTransactionWalletRepository(),
          productId: '4001',
          accessToken: 'access-token',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('当前可购买'), findsOneWidget);

    await tester.enterText(find.byType(TextField), '123456');
    await tester.tap(find.text('确认购买 1 件'));
    await tester.pumpAndSettle();

    expect(find.text('订单详情'), findsWidgets);
    expect(find.text('订单 RO202605310001'), findsOneWidget);
    expect(find.text('返回商品详情'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '查看背包发放'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, '查看扣款流水'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '查看背包发放'));
    await tester.pumpAndSettle();

    expect(find.text('我的背包'), findsWidgets);
    expect(find.text('来源：订单详情'), findsOneWidget);
    expect(find.text('返回订单详情'), findsOneWidget);
    expect(find.text('订单发放徽章'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('查看来源订单'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('订单详情'), findsWidgets);
    expect(find.text('订单 RO202605310001'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, '查看扣款流水'));
    await tester.pumpAndSettle();

    expect(find.text('订单扣款流水'), findsWidgets);
    expect(find.text('返回订单详情'), findsOneWidget);
    expect(find.text('当前筛选：Order #9001'), findsOneWidget);
    expect(find.text('商城消费'), findsOneWidget);
  });

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

  testWidgets('login request returns to product purchase panel',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.restore();
    var loginRequested = false;

    await tester.pumpWidget(
      MaterialApp(
        home: ShopProductDetailPage(
          environment: const AppEnvironment.development(),
          repository: const _SuccessShopRepository(),
          walletRepository: const EmptyWalletRepository(),
          productId: '4001',
          sessionController: sessionController,
          onRequestSignIn: () async {
            loginRequested = true;
            await sessionController.setSession(
              AuthSession(
                accessToken: 'access-token',
                refreshToken: 'refresh-token',
                userId: 'user-42',
                expiresAt: DateTime.now().toUtc().add(
                      const Duration(hours: 1),
                    ),
              ),
            );
          },
        ),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('登录后购买'));
    await tester.pumpAndSettle();

    expect(loginRequested, isTrue);
    expect(find.text('已回到商品详情，可以继续确认购买。'), findsOneWidget);
    expect(find.text('当前可购买'), findsOneWidget);
  });

  testWidgets('purchase failure stays on detail with explicit error',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _PurchaseFailingShopRepository(),
          walletRepository: EmptyWalletRepository(),
          productId: '4001',
          accessToken: 'access-token',
        ),
      ),
    );

    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '123456');
    await tester.tap(find.text('确认购买 1 件'));
    await tester.pumpAndSettle();

    expect(find.text('支付口令错误'), findsOneWidget);
    expect(find.text('商品详情'), findsWidgets);
  });

  test('http shop repository uses public product detail endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final product = await repository.getProductDetail(productId: '4001');

    expect(apiClient.lastUri?.path, '/api/v1/Shop/GetProduct/4001');
    expect(product.id, '4001');
    expect(product.productType, '消耗品');
    expect(product.durationDisplay, '永久');
  });

  test('http shop repository uses public product list endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getProductPage(pageIndex: 2, pageSize: 10);

    expect(apiClient.lastUri?.path, '/api/v1/Shop/GetProducts');
    expect(apiClient.lastUri?.queryParameters['pageIndex'], '2');
    expect(apiClient.lastUri?.queryParameters['pageSize'], '10');
    expect(page.products.single.id, '4001');
    expect(page.products.single.productType, '消耗品');
  });

  test('http shop repository uses private order detail endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final order = await repository.getOrderDetail(
      accessToken: 'access-token',
      orderId: '9001',
    );

    expect(apiClient.lastUri?.path, '/api/v1/Shop/GetOrder/9001');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(order.id, '9001');
    expect(order.orderNo, 'RO202605310001');
    expect(order.productId, '4001');
  });

  test('http shop repository uses private buy check endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final result = await repository.checkCanBuy(
      accessToken: 'access-token',
      productId: '4001',
    );

    expect(apiClient.lastUri?.path, '/api/v1/Shop/CheckCanBuy/4001');
    expect(apiClient.lastUri?.queryParameters['quantity'], '1');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(result.canBuy, isTrue);
  });

  test('http shop repository posts purchase body to private endpoint',
      () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final result = await repository.purchaseProduct(
      accessToken: 'access-token',
      productId: '4001',
      paymentPassword: '123456',
    );

    expect(apiClient.lastUri?.path, '/api/v1/Shop/Purchase');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'productId': '4001',
      'quantity': 1,
      'paymentPassword': '123456',
    });
    expect(result.success, isTrue);
    expect(result.orderId, '9001');
  });
}

class _SuccessShopRepository implements ShopRepository {
  const _SuccessShopRepository();

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
          id: '4001',
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
      id: '4001',
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
  Future<ShopProductBuyCheckResult> checkCanBuy({
    required String accessToken,
    required String productId,
    int quantity = 1,
  }) async {
    return const ShopProductBuyCheckResult(canBuy: true);
  }

  @override
  Future<ShopPurchaseResult> purchaseProduct({
    required String accessToken,
    required String productId,
    required String paymentPassword,
    int quantity = 1,
  }) async {
    return const ShopPurchaseResult(
      success: true,
      orderId: '9001',
      orderNo: 'RO202605310001',
      deductedCoins: 120,
      remainingBalance: 880,
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
      dataCount: 0,
      pageCount: 1,
      orders: [],
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
      createTime: '2026-05-31T08:00:00Z',
      completedTime: '2026-05-31T08:01:00Z',
    );
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const <ShopUserBenefit>[];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return const <ShopInventoryItem>[];
  }
}

class _PurchaseFailingShopRepository extends _SuccessShopRepository {
  const _PurchaseFailingShopRepository();

  @override
  Future<ShopPurchaseResult> purchaseProduct({
    required String accessToken,
    required String productId,
    required String paymentPassword,
    int quantity = 1,
  }) async {
    return const ShopPurchaseResult(
      success: false,
      errorMessage: '支付口令错误',
    );
  }
}

class _PurchaseInventoryShopRepository extends _SuccessShopRepository {
  const _PurchaseInventoryShopRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const [
      ShopUserBenefit(
        id: 'benefit-order-1',
        benefitType: 'Badge',
        benefitTypeDisplay: '徽章',
        benefitName: '订单发放徽章',
        sourceType: 'Order',
        sourceTypeDisplay: '商城订单',
        sourceOrderId: '9001',
        sourceProductId: '4001',
        isActive: true,
        isExpired: false,
      ),
    ];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return const [
      ShopInventoryItem(
        id: 'inventory-order-1',
        consumableType: 'ProfileRename',
        consumableTypeDisplay: '改名卡',
        itemName: 'Profile Rename Card',
        quantity: 1,
        sourceProductId: '4001',
      ),
    ];
  }
}

class _OrderInventoryFailingRepository extends _SuccessShopRepository {
  const _OrderInventoryFailingRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    throw const RadishApiClientException('背包发放暂时不可用');
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    throw const RadishApiClientException('背包发放暂时不可用');
  }
}

class _RefreshFailingOrderRepository extends _SuccessShopRepository {
  int _orderDetailRequests = 0;

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    _orderDetailRequests += 1;
    if (_orderDetailRequests > 1) {
      throw const RadishApiClientException('订单详情刷新失败');
    }

    return super.getOrderDetail(
      accessToken: accessToken,
      orderId: orderId,
    );
  }
}

class _NonCanonicalOrderReferenceRepository extends _SuccessShopRepository {
  const _NonCanonicalOrderReferenceRepository();

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    return const ShopOrderDetail(
      id: '09001',
      orderNo: 'RO202605310001',
      productId: 'product-4001',
      productName: 'Profile Rename Card',
      productType: 'Consumable',
      productTypeDisplay: '消耗品',
      quantity: 1,
      unitPrice: 120,
      totalPrice: 120,
      status: 'Completed',
      statusDisplay: '已完成',
      createTime: '2026-05-31T08:00:00Z',
      completedTime: '2026-05-31T08:01:00Z',
    );
  }
}

class _BrokenInventorySourceRepository extends _SuccessShopRepository {
  const _BrokenInventorySourceRepository();

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) {
    throw const RadishApiClientException('来源商品不存在');
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    throw const RadishApiClientException('来源订单不存在');
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
        sourceType: 'Order',
        sourceTypeDisplay: '商城订单',
        sourceOrderId: '9001',
        sourceProductId: '4001',
        isActive: true,
        isExpired: false,
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
        consumableType: 'ProfileRename',
        consumableTypeDisplay: '改名卡',
        itemName: 'Profile Rename Card',
        quantity: 1,
        sourceProductId: '4001',
      ),
    ];
  }
}

class _NonCanonicalInventorySourceRepository extends _SuccessShopRepository {
  const _NonCanonicalInventorySourceRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const [
      ShopUserBenefit(
        id: 'benefit-invalid-source',
        benefitType: 'Badge',
        benefitTypeDisplay: '徽章',
        benefitName: '非规范来源权益',
        sourceType: 'Order',
        sourceTypeDisplay: '商城订单',
        sourceOrderId: '09001',
        sourceProductId: 'product-4001',
        isActive: true,
        isExpired: false,
      ),
    ];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return const [
      ShopInventoryItem(
        id: 'inventory-invalid-source',
        consumableType: 'ProfileRename',
        consumableTypeDisplay: '改名卡',
        itemName: '非规范来源道具',
        quantity: 1,
        sourceProductId: '0004001',
      ),
    ];
  }
}

class _EmptyThenRefreshFailingWalletRepository implements WalletRepository {
  int _balanceRequests = 0;

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) async {
    _balanceRequests += 1;
    if (_balanceRequests > 1) {
      throw const RadishApiClientException('资产刷新失败');
    }

    return const CoinBalance(
      userId: 'user-42',
      balance: 880,
      balanceDisplay: '880.000',
      frozenBalance: 0,
      frozenBalanceDisplay: '0.000',
      totalEarned: 1200,
      totalSpent: 120,
      totalTransferredIn: 0,
      totalTransferredOut: 0,
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) async {
    return CoinTransactionPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: 0,
      pageCount: 1,
      transactions: const [],
    );
  }
}

class _OrderTransactionWalletRepository implements WalletRepository {
  const _OrderTransactionWalletRepository();

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) async {
    return const CoinBalance(
      userId: 'user-42',
      balance: 880,
      balanceDisplay: '880.000',
      frozenBalance: 0,
      frozenBalanceDisplay: '0.000',
      totalEarned: 1200,
      totalSpent: 120,
      totalTransferredIn: 0,
      totalTransferredOut: 0,
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) async {
    return CoinTransactionPage(
      page: 1,
      pageSize: pageSize,
      dataCount: 1,
      pageCount: 1,
      transactions: [
        CoinTransaction(
          id: 'coin-2',
          transactionNo: 'TXN202605310001',
          fromUserId: 'user-42',
          fromUserName: '我',
          toUserId: 'shop',
          toUserName: '商城',
          amount: 120,
          amountDisplay: '120.000',
          fee: 0,
          feeDisplay: '0.000',
          transactionType: transactionType ?? 'CONSUME',
          transactionTypeDisplay: '商城消费',
          status: status ?? 'SUCCESS',
          statusDisplay: '成功',
          businessType: businessType,
          businessId: businessId,
          remark: '购买 Profile Rename Card',
          createTime: '2026-05-31T08:00:30Z',
        ),
      ],
    );
  }
}

class _FailingShopRepository implements ShopRepository {
  const _FailingShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('商品列表不可用');
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) {
    throw const RadishApiClientException('商品不存在');
  }

  @override
  Future<ShopProductBuyCheckResult> checkCanBuy({
    required String accessToken,
    required String productId,
    int quantity = 1,
  }) {
    throw const RadishApiClientException('购买检查不可用');
  }

  @override
  Future<ShopPurchaseResult> purchaseProduct({
    required String accessToken,
    required String productId,
    required String paymentPassword,
    int quantity = 1,
  }) {
    throw const RadishApiClientException('购买不可用');
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('订单列表不可用');
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    throw const RadishApiClientException('订单详情不可用');
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    throw const RadishApiClientException('权益列表不可用');
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    throw const RadishApiClientException('背包列表不可用');
  }
}

class _RecordingShopApiClient implements RadishApiClient {
  Uri? lastUri;
  String? lastBearerToken;
  Object? lastBody;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBearerToken = bearerToken;
    lastBody = null;
    if (uri.path == '/api/v1/Shop/GetProducts') {
      return decode({
        'page': 1,
        'pageSize': 10,
        'dataCount': 1,
        'pageCount': 1,
        'data': [
          {
            'voId': '4001',
            'voName': 'Profile Rename Card',
            'voProductType': 2,
            'voPrice': 120,
            'voOriginalPrice': 180,
            'voHasDiscount': true,
            'voSoldCount': 3,
            'voDurationDisplay': '永久',
            'voInStock': true,
          },
        ],
      });
    }

    if (uri.path == '/api/v1/Shop/GetOrder/9001') {
      return decode({
        'voId': '9001',
        'voOrderNo': 'RO202605310001',
        'voProductId': '4001',
        'voProductName': 'Profile Rename Card',
        'voProductType': 'Consumable',
        'voProductTypeDisplay': '消耗品',
        'voQuantity': 1,
        'voUnitPrice': 120,
        'voTotalPrice': 120,
        'voStatus': 'Completed',
        'voStatusDisplay': '已完成',
        'voCreateTime': '2026-05-31T08:00:00Z',
        'voCompletedTime': '2026-05-31T08:01:00Z',
      });
    }

    if (uri.path == '/api/v1/Shop/CheckCanBuy/4001') {
      return decode({
        'voCanBuy': true,
        'voReason': null,
      });
    }

    return decode({
      'voId': '4001',
      'voName': 'Profile Rename Card',
      'voDescription': 'Use this read-only detail.',
      'voCategoryName': 'Profile tools',
      'voProductType': 2,
      'voBenefitValue': 'rename-card',
      'voPrice': 120,
      'voOriginalPrice': 180,
      'voHasDiscount': true,
      'voStockType': 0,
      'voStock': 0,
      'voSoldCount': 3,
      'voLimitPerUser': 1,
      'voInStock': true,
      'voDurationDisplay': '永久',
      'voIsOnSale': true,
      'voIsEnabled': true,
    });
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBearerToken = bearerToken;
    lastBody = body;
    return decode({
      'success': true,
      'orderId': '9001',
      'orderNo': 'RO202605310001',
      'deductedCoins': 120,
      'remainingBalance': 880,
    });
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

class _ClipboardRecorder {
  String? text;

  void install() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'Clipboard.setData') {
        final arguments = Map<Object?, Object?>.from(call.arguments as Map);
        text = arguments['text'] as String?;
      }

      return null;
    });
  }

  void reset() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, null);
  }
}
