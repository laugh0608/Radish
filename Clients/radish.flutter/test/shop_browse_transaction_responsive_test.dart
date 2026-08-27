import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
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

  for (final themeId in RadishThemeId.values) {
    testWidgets(
      'keeps commerce browse transaction structure in ${themeId.value}',
      (tester) async {
        _configureViewport(tester, const Size(600, 2200));

        await tester.pumpWidget(_detailApp(themeId: themeId));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const ValueKey('shop-detail-layout-medium')),
          findsOne,
        );
        expect(
          find.byKey(const ValueKey('shop-purchase-rail-medium')),
          findsOne,
        );
        expect(find.text('公开商品详情'), findsOne);
        expect(find.text('单商品购买'), findsOne);
        expect(tester.takeException(), isNull, reason: themeId.value);
      },
    );
  }

  testWidgets('compact long product content stays inside the viewport',
      (tester) async {
    _configureViewport(tester, const Size(599, 3600));

    await tester.pumpWidget(
      _detailApp(
        repository: const _ResponsiveShopRepository(useLongContent: true),
        productId: '9223372036854775807',
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('shop-detail-layout-compact')), findsOne);
    expect(find.text(_longProductName), findsOne);
    expect(find.text(_longProductDescription), findsOne);
    expect(find.text(_longProductCategory), findsOne);
    expect(find.text(_longProductBenefit), findsOne);
    expect(
      find.text('/shop/product/9223372036854775807'),
      findsOne,
    );
    expect(
      tester.getTopLeft(find.text('单商品购买')).dy,
      lessThan(tester.getTopLeft(find.text('详情说明')).dy),
    );
    expect(tester.takeException(), isNull);
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

const _longProductName = '超长商品名称用于验证紧凑窗口中的公开商品信息、购买任务和长文本仍能保持自然换行';
const _longProductDescription =
    '这是一段需要在 compact 商品详情中自然换行的超长描述，用来确认公开信息不会挤压单商品购买任务，也不会产生横向溢出。';
const _longProductCategory = '超长商品分类名称用于验证紧凑标签仍然受控显示';
const _longProductBenefit =
    'benefit:9223372036854775807:long-value-that-must-wrap-inside-the-compact-product-detail-surface';

void _configureViewport(WidgetTester tester, Size size) {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = size;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _detailApp({
  ShopRepository repository = const _ResponsiveShopRepository(),
  RadishThemeId themeId = RadishThemeId.defaultTheme,
  String productId = '4001',
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: ShopProductDetailPage(
      environment: const AppEnvironment.development(),
      repository: repository,
      walletRepository: const _ResponsiveWalletRepository(),
      productId: productId,
    ),
  );
}

class _ResponsiveShopRepository implements ShopRepository {
  const _ResponsiveShopRepository({this.useLongContent = false});

  final bool useLongContent;

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
    if (useLongContent) {
      return const ShopProductDetail(
        id: '9223372036854775807',
        name: _longProductName,
        description: _longProductDescription,
        categoryName: _longProductCategory,
        productType: '超长权益与道具组合类型用于验证元数据自然换行',
        benefitValue: _longProductBenefit,
        price: 9223372036854775000,
        originalPrice: 9223372036854775807,
        hasDiscount: true,
        stockType: 'Unlimited',
        stock: 0,
        soldCount: 9223372036854775000,
        limitPerUser: 1,
        inStock: true,
        durationDisplay: '永久有效且保留完整服务端语义',
        isOnSale: true,
        isEnabled: true,
      );
    }
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
