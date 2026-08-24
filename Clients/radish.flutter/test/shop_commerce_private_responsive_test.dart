import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_inventory_page.dart';
import 'package:radish_flutter/features/shop/presentation/shop_order_detail_page.dart';
import 'package:radish_flutter/features/shop/presentation/shop_order_list_page.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  testWidgets('order catalog resolves exact compact medium expanded bounds',
      (tester) async {
    _configureView(tester);
    await tester.pumpWidget(
      const MaterialApp(
        home: ShopOrderListPage(
          environment: AppEnvironment.development(),
          repository: _ResponsivePrivateShopRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'user-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await _expectWindowKey(
      tester,
      width: 599,
      key: 'shop-order-catalog-grid-compact',
    );
    await _expectWindowKey(
      tester,
      width: 600,
      key: 'shop-order-catalog-grid-medium',
    );
    await _expectWindowKey(
      tester,
      width: 1024,
      key: 'shop-order-catalog-grid-expanded',
    );
    await _expectWindowKey(
      tester,
      width: 1280,
      key: 'shop-order-catalog-grid-expanded',
    );

    expect(find.textContaining('ORDER-9001'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('order detail resolves rail layouts without long text overflow',
      (tester) async {
    _configureView(tester);
    await tester.pumpWidget(
      const MaterialApp(
        home: ShopOrderDetailPage(
          environment: AppEnvironment.development(),
          repository: _ResponsivePrivateShopRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'user-42',
          orderId: '9001',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await _expectWindowKey(
      tester,
      width: 599,
      key: 'shop-order-detail-compact',
    );
    await _expectWindowKey(
      tester,
      width: 600,
      key: 'shop-order-detail-medium',
    );
    await _expectWindowKey(
      tester,
      width: 1024,
      key: 'shop-order-detail-expanded',
    );
    await _expectWindowKey(
      tester,
      width: 1280,
      key: 'shop-order-detail-expanded',
    );

    expect(find.text('查看背包发放'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('inventory resolves sequential and independent lane layouts',
      (tester) async {
    _configureView(tester);
    await tester.pumpWidget(
      const MaterialApp(
        home: ShopInventoryPage(
          environment: AppEnvironment.development(),
          repository: _ResponsivePrivateShopRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'user-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await _expectWindowKey(
      tester,
      width: 599,
      key: 'shop-inventory-compact',
    );
    await _expectWindowKey(
      tester,
      width: 600,
      key: 'shop-inventory-medium',
    );
    await _expectWindowKey(
      tester,
      width: 1024,
      key: 'shop-inventory-expanded',
    );
    await _expectWindowKey(
      tester,
      width: 1280,
      key: 'shop-inventory-expanded',
    );

    expect(find.byKey(const ValueKey('shop-inventory-benefits')), findsOne);
    expect(find.byKey(const ValueKey('shop-inventory-items')), findsOne);
    expect(tester.takeException(), isNull);
  });

  testWidgets('inventory keeps item success visible beside benefit failure',
      (tester) async {
    _configureView(tester, width: 1024);
    await tester.pumpWidget(
      const MaterialApp(
        home: ShopInventoryPage(
          environment: AppEnvironment.development(),
          repository: _PartialResponsiveInventoryRepository(),
          walletRepository: EmptyWalletRepository(),
          accessToken: 'access-token',
          accountId: 'user-42',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('权益暂时不可用'), findsWidgets);
    expect(find.textContaining('长名称道具'), findsOneWidget);
    expect(find.text('暂时无法加载背包'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('commerce private structure stays isomorphic in four themes',
      (tester) async {
    _configureView(tester, width: 600);
    for (final themeId in RadishThemeId.values) {
      await tester.pumpWidget(
        MaterialApp(
          theme: buildRadishTheme(themeId),
          home: const ShopInventoryPage(
            environment: AppEnvironment.development(),
            repository: _ResponsivePrivateShopRepository(),
            walletRepository: EmptyWalletRepository(),
            accessToken: 'access-token',
            accountId: 'user-42',
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('shop-inventory-medium')), findsOne);
      expect(find.text('我的背包'), findsWidgets);
      expect(tester.takeException(), isNull, reason: themeId.name);
    }
  });
}

void _configureView(WidgetTester tester, {double width = 599}) {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = Size(width, 2800);
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Future<void> _expectWindowKey(
  WidgetTester tester, {
  required double width,
  required String key,
}) async {
  tester.view.physicalSize = Size(width, 2800);
  await tester.pumpAndSettle();
  expect(find.byKey(ValueKey(key)), findsOneWidget);
}

class _ResponsivePrivateShopRepository extends EmptyShopRepository {
  const _ResponsivePrivateShopRepository();

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return ShopOrderPage(
      page: 1,
      pageSize: 20,
      dataCount: 3,
      pageCount: 1,
      orders: [
        _summary('9001', '极长商品名称用于验证订单卡在紧凑和多列窗口中仍然保持可读并且不会横向溢出'),
        _summary('9002', 'Guofeng Theme'),
        _summary('9003', 'Experience Badge'),
      ],
    );
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    return ShopOrderDetail(
      id: orderId,
      orderNo: 'ORDER-$orderId-VERY-LONG-READABLE-IDENTIFIER',
      productId: '4001',
      productName: '极长商品名称用于验证订单详情主轴在六百像素双区布局中仍然自然换行而不遮挡只读上下文动作',
      productType: 'Consumable',
      productTypeDisplay: '消耗品',
      quantity: 1,
      unitPrice: 120,
      totalPrice: 120,
      status: 'Completed',
      statusDisplay: '已完成',
      durationDisplay: '永久',
      createTime: '2026-08-24 10:00',
      paidTime: '2026-08-24 10:01',
      completedTime: '2026-08-24 10:02',
      userRemark: '这是一段用于验证真实长备注能够在订单详情中自然换行的文本。',
      coinTransactionId: '8001',
    );
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return [
      _benefit(
        '6001',
        '极长权益名称用于验证权益资源卡在双 lane 和窄窗口中保持内容边界',
      ),
    ];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return [
      _item(
        '7001',
        '极长名称道具用于验证多端背包内容网格不会产生横向溢出',
      ),
    ];
  }
}

class _PartialResponsiveInventoryRepository
    extends _ResponsivePrivateShopRepository {
  const _PartialResponsiveInventoryRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    throw const RadishApiClientException('权益暂时不可用');
  }
}

ShopOrderSummary _summary(String id, String productName) {
  return ShopOrderSummary(
    id: id,
    orderNo: 'ORDER-$id',
    productName: productName,
    quantity: 1,
    totalPrice: 120,
    status: 'Completed',
    statusDisplay: '已完成',
    createTime: '2026-08-24 10:00',
  );
}

ShopUserBenefit _benefit(String id, String name) {
  return ShopUserBenefit(
    id: id,
    benefitType: 'Badge',
    benefitTypeDisplay: '徽章',
    benefitValue: '这是一个很长的权益值，用于验证文本边界。',
    benefitName: name,
    sourceType: 'Order',
    sourceTypeDisplay: '商城订单',
    sourceOrderId: '9001',
    sourceProductId: '4001',
    durationDisplay: '永久',
    isActive: true,
    isExpired: false,
  );
}

ShopInventoryItem _item(String id, String name) {
  return ShopInventoryItem(
    id: id,
    consumableType: 'RenameCard',
    consumableTypeDisplay: '资料改名卡',
    itemName: name,
    itemValue: '这是一个很长的道具值，用于验证文本边界。',
    quantity: 2,
    sourceProductId: '4001',
    createTime: '2026-08-24 10:00',
  );
}
