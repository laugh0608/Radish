import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_benefit_inventory_controller.dart';
import 'package:radish_flutter/features/shop/presentation/shop_item_inventory_controller.dart';
import 'package:radish_flutter/features/shop/presentation/shop_order_catalog_controller.dart';
import 'package:radish_flutter/features/shop/presentation/shop_order_detail_controller.dart';

void main() {
  group('ShopOrderCatalogController', () {
    test('appends with stable order id de-duplication', () async {
      final controller = ShopOrderCatalogController(
        repository: const _PagedOrderRepository(),
      );
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await controller.loadMore();

      expect(
        controller.state.orders.map((order) => order.id),
        ['9001', '9002', '9003'],
      );
      expect(controller.state.hasMore, isFalse);
    });

    test('keeps append failure separate from the readable catalog', () async {
      final controller = ShopOrderCatalogController(
        repository: _AppendFailingOrderRepository(),
      );
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await controller.loadMore();

      expect(controller.state.orders.single.id, '9001');
      expect(controller.state.refreshIssue, isNull);
      expect(controller.state.appendIssue?.message, '订单下一页暂时不可用');
    });

    test('isolates a late response from the previous account', () async {
      final repository = _DeferredAccountOrderRepository();
      final controller = ShopOrderCatalogController(repository: repository);
      addTearDown(controller.dispose);

      final olderRequest = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      final newerRequest = controller.openAccount(
        accessToken: 'token-b',
        accountId: 'user-b',
      );
      repository.requests['token-b']!.complete(
        _orderPage(orders: [_orderSummary('9002', 'New account order')]),
      );
      await newerRequest;
      repository.requests['token-a']!.complete(
        _orderPage(orders: [_orderSummary('9001', 'Old account order')]),
      );
      await olderRequest;

      expect(controller.state.accountId, 'user-b');
      expect(controller.state.orders.single.id, '9002');
    });

    test('does not publish a deferred response after dispose', () async {
      final repository = _DeferredAccountOrderRepository();
      final controller = ShopOrderCatalogController(repository: repository);
      var notifications = 0;
      controller.addListener(() => notifications++);

      final request = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      expect(notifications, 1);
      controller.dispose();
      repository.requests['token-a']!.complete(
        _orderPage(orders: [_orderSummary('9001', 'Late order')]),
      );
      await request;

      expect(notifications, 1);
      expect(controller.state.isLoading, isTrue);
    });
  });

  group('ShopOrderDetailController', () {
    test('rejects non-canonical order ids before repository access', () async {
      final repository = _RecordingOrderDetailRepository();
      final controller = ShopOrderDetailController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openOrder(
        orderId: '09001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );

      expect(controller.state.isUnavailable, isTrue);
      expect(controller.state.issue?.code, 'Shop.InvalidOrderId');
      expect(repository.requests, 0);
    });

    test('keeps an order readable when refresh fails', () async {
      final repository = _RefreshFailingOrderDetailRepository();
      final controller = ShopOrderDetailController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openOrder(
        orderId: '9001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.order?.id, '9001');
      expect(controller.state.issue?.message, '订单详情刷新失败');
    });

    test('ignores a late response from the previous order target', () async {
      final repository = _DeferredOrderDetailRepository();
      final controller = ShopOrderDetailController(repository: repository);
      addTearDown(controller.dispose);

      final olderRequest = controller.openOrder(
        orderId: '9001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      final newerRequest = controller.openOrder(
        orderId: '9002',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      repository.requests['9002']!.complete(
        _orderDetail('9002', 'New order'),
      );
      await newerRequest;
      repository.requests['9001']!.complete(
        _orderDetail('9001', 'Old order'),
      );
      await olderRequest;

      expect(controller.state.orderId, '9002');
      expect(controller.state.order?.productName, 'New order');
    });
  });

  group('inventory collection owners', () {
    test('benefit failure does not hide successful inventory items', () async {
      const repository = _PartialInventoryRepository();
      final benefitController =
          ShopBenefitInventoryController(repository: repository);
      final itemController =
          ShopItemInventoryController(repository: repository);
      addTearDown(benefitController.dispose);
      addTearDown(itemController.dispose);

      await Future.wait<void>([
        benefitController.openAccount(
          accessToken: 'token-a',
          accountId: 'user-a',
        ),
        itemController.openAccount(
          accessToken: 'token-a',
          accountId: 'user-a',
        ),
      ]);

      expect(benefitController.state.isUnavailable, isTrue);
      expect(benefitController.state.issue?.message, '权益暂时不可用');
      expect(itemController.state.isReady, isTrue);
      expect(itemController.state.items.single.itemName, 'Rename Card');
    });

    test('benefit refresh failure preserves only its own stale snapshot',
        () async {
      final repository = _RefreshFailingBenefitRepository();
      final controller = ShopBenefitInventoryController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.items.single.benefitName, 'Early Badge');
      expect(controller.state.issue?.message, '权益刷新失败');
    });

    test('empty benefit snapshot remains known when refresh fails', () async {
      final repository = _EmptyRefreshFailingBenefitRepository();
      final controller = ShopBenefitInventoryController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      expect(controller.state.isEmpty, isTrue);
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.items, isEmpty);
      expect(controller.state.issue?.message, '空权益刷新失败');
    });
  });
}

class _PagedOrderRepository extends EmptyShopRepository {
  const _PagedOrderRepository();

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (pageIndex == 1) {
      return _orderPage(
        page: 1,
        pageCount: 2,
        dataCount: 3,
        orders: [
          _orderSummary('9001', 'First'),
          _orderSummary('9002', 'Second'),
        ],
      );
    }
    return _orderPage(
      page: 2,
      pageCount: 2,
      dataCount: 3,
      orders: [
        _orderSummary('9002', 'Duplicate'),
        _orderSummary('9003', 'Third'),
      ],
    );
  }
}

class _AppendFailingOrderRepository extends EmptyShopRepository {
  int requests = 0;

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    requests++;
    if (requests > 1) {
      throw const RadishApiClientException('订单下一页暂时不可用');
    }
    return _orderPage(
      page: 1,
      pageCount: 2,
      dataCount: 2,
      orders: [_orderSummary('9001', 'Stable')],
    );
  }
}

class _DeferredAccountOrderRepository extends EmptyShopRepository {
  final Map<String, Completer<ShopOrderPage>> requests = {};

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    final request = Completer<ShopOrderPage>();
    requests[accessToken] = request;
    return request.future;
  }
}

class _RecordingOrderDetailRepository extends EmptyShopRepository {
  int requests = 0;

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    requests++;
    return _orderDetail(orderId, 'Recorded order');
  }
}

class _RefreshFailingOrderDetailRepository extends EmptyShopRepository {
  int requests = 0;

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    requests++;
    if (requests > 1) {
      throw const RadishApiClientException('订单详情刷新失败');
    }
    return _orderDetail(orderId, 'Stable order');
  }
}

class _DeferredOrderDetailRepository extends EmptyShopRepository {
  final Map<String, Completer<ShopOrderDetail>> requests = {};

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    final request = Completer<ShopOrderDetail>();
    requests[orderId] = request;
    return request.future;
  }
}

class _PartialInventoryRepository extends EmptyShopRepository {
  const _PartialInventoryRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    throw const RadishApiClientException('权益暂时不可用');
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return [_inventoryItem('7001', 'Rename Card')];
  }
}

class _RefreshFailingBenefitRepository extends EmptyShopRepository {
  int requests = 0;

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    requests++;
    if (requests > 1) {
      throw const RadishApiClientException('权益刷新失败');
    }
    return [_benefit('6001', 'Early Badge')];
  }
}

class _EmptyRefreshFailingBenefitRepository extends EmptyShopRepository {
  int requests = 0;

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    requests++;
    if (requests > 1) {
      throw const RadishApiClientException('空权益刷新失败');
    }
    return const <ShopUserBenefit>[];
  }
}

ShopOrderPage _orderPage({
  int page = 1,
  int pageCount = 1,
  int? dataCount,
  required List<ShopOrderSummary> orders,
}) {
  return ShopOrderPage(
    page: page,
    pageSize: 20,
    dataCount: dataCount ?? orders.length,
    pageCount: pageCount,
    orders: orders,
  );
}

ShopOrderSummary _orderSummary(String id, String productName) {
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

ShopOrderDetail _orderDetail(String id, String productName) {
  return ShopOrderDetail(
    id: id,
    orderNo: 'ORDER-$id',
    productId: '4001',
    productName: productName,
    productType: 'Consumable',
    quantity: 1,
    unitPrice: 120,
    totalPrice: 120,
    status: 'Completed',
    createTime: '2026-08-24 10:00',
  );
}

ShopUserBenefit _benefit(String id, String name) {
  return ShopUserBenefit(
    id: id,
    benefitType: 'Badge',
    benefitName: name,
    sourceType: 'Order',
    isActive: true,
    isExpired: false,
  );
}

ShopInventoryItem _inventoryItem(String id, String name) {
  return ShopInventoryItem(
    id: id,
    consumableType: 'RenameCard',
    itemName: name,
    quantity: 1,
  );
}
