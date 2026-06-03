import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_list_page.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  testWidgets('renders public shop product list and opens detail',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductListPage(
          environment: AppEnvironment.development(),
          repository: _SeededShopRepository(),
          walletRepository: EmptyWalletRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);
    expect(find.text('只读购买边界'), findsNothing);

    await tester.tap(find.text('查看详情'));
    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('来源：公开商品列表'), findsOneWidget);
    expect(find.text('返回商城'), findsOneWidget);
    expect(find.text('单商品购买'), findsOneWidget);
    expect(find.text('登录后购买'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(find.text('公开商城'), findsWidgets);
    expect(find.text('商品列表'), findsOneWidget);
  });

  testWidgets('renders public shop list error state', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductListPage(
          environment: AppEnvironment.development(),
          repository: _FailingShopRepository(),
          walletRepository: EmptyWalletRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载公开商品'), findsOneWidget);
    expect(find.text('商品列表不可用'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });
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
