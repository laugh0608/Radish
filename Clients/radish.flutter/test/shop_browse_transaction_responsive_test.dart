import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_detail_page.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_list_page.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  testWidgets('catalog resolves compact medium and expanded grids',
      (tester) async {
    tester.view.devicePixelRatio = 1;
    tester.view.physicalSize = const Size(599, 2200);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductListPage(
          environment: AppEnvironment.development(),
          repository: _ResponsiveShopRepository(),
          walletRepository: _ResponsiveWalletRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-catalog-grid-compact')), findsOne);

    tester.view.physicalSize = const Size(600, 2200);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-catalog-grid-medium')), findsOne);

    tester.view.physicalSize = const Size(1024, 2200);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-catalog-grid-expanded')), findsOne);
  });

  testWidgets('detail preserves purchase priority across window classes',
      (tester) async {
    tester.view.devicePixelRatio = 1;
    tester.view.physicalSize = const Size(599, 2200);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _ResponsiveShopRepository(),
          walletRepository: _ResponsiveWalletRepository(),
          productId: '4001',
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-detail-layout-compact')), findsOne);
    expect(
      tester.getTopLeft(find.text('单商品购买')).dy,
      lessThan(tester.getTopLeft(find.text('详情说明')).dy),
    );

    tester.view.physicalSize = const Size(600, 2200);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-detail-layout-medium')), findsOne);
    expect(find.byKey(const ValueKey('shop-purchase-rail-medium')), findsOne);

    tester.view.physicalSize = const Size(1024, 2200);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-detail-layout-expanded')), findsOne);
    expect(find.byKey(const ValueKey('shop-purchase-rail-360')), findsOne);

    tester.view.physicalSize = const Size(1280, 2200);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('shop-detail-main-axis-820')), findsOne);
  });

  testWidgets('payment draft requires explicit discard before returning',
      (tester) async {
    tester.view.devicePixelRatio = 1;
    tester.view.physicalSize = const Size(1200, 2200);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductListPage(
          environment: AppEnvironment.development(),
          repository: _ResponsiveShopRepository(),
          walletRepository: _ResponsiveWalletRepository(),
          accessToken: 'access-token',
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('查看详情').first);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '123456');

    await tester.pageBack();
    await tester.pumpAndSettle();
    expect(find.text('丢弃支付口令？'), findsOneWidget);
    await tester.tap(find.text('继续购买'));
    await tester.pumpAndSettle();
    expect(find.text('商品详情'), findsWidgets);
    expect(find.byType(TextField), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.tap(find.text('丢弃并返回'));
    await tester.pumpAndSettle();
    expect(find.text('商品列表'), findsOneWidget);
    expect(find.text('商品详情'), findsNothing);
  });
}

class _ResponsiveShopRepository implements ShopRepository {
  const _ResponsiveShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopProductPage(
      page: 1,
      pageSize: 20,
      dataCount: 3,
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
        ),
        ShopProductSummary(
          id: '4002',
          name: 'Guofeng Theme',
          productType: '主题',
          price: 300,
          soldCount: 8,
          durationDisplay: '永久',
        ),
        ShopProductSummary(
          id: '4003',
          name: 'Experience Badge',
          productType: '徽章',
          price: 80,
          soldCount: 12,
          durationDisplay: '30 天',
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
      description: '用于验证公开商品详情和单商品购买结构。',
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
    required String idempotencyKey,
    int quantity = 1,
  }) async {
    return const ShopPurchaseResult(
      success: true,
      orderId: '9001',
      orderNo: 'ORDER-9001',
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
  }) {
    throw UnimplementedError();
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({required String accessToken}) {
    return Future.value(const <ShopUserBenefit>[]);
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    return Future.value(const <ShopInventoryItem>[]);
  }
}

class _ResponsiveWalletRepository implements WalletRepository {
  const _ResponsiveWalletRepository();

  @override
  Future<CoinBalance> getBalance({required String accessToken}) async {
    return const CoinBalance(
      userId: 'responsive-user',
      balance: 880,
      balanceDisplay: '880',
      frozenBalance: 0,
      frozenBalanceDisplay: '0',
      totalEarned: 1000,
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
  }) {
    throw UnimplementedError();
  }
}
