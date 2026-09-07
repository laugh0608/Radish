import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_catalog_controller.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_detail_controller.dart';
import 'package:radish_flutter/features/shop/presentation/shop_purchase_controller.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  group('ShopCatalogController', () {
    test('ignores an older initial response after a newer request wins',
        () async {
      final repository = _DeferredCatalogRepository();
      final controller = ShopCatalogController(repository: repository);
      addTearDown(controller.dispose);

      final olderRequest = controller.loadInitial();
      final newerRequest = controller.loadInitial();
      repository.requests[1].complete(
        _productPage(products: [_summary('4002', 'New product')]),
      );
      await newerRequest;
      repository.requests[0].complete(
        _productPage(products: [_summary('4001', 'Old product')]),
      );
      await olderRequest;

      expect(controller.state.products.single.id, '4002');
      expect(controller.state.products.single.name, 'New product');
    });

    test('keeps catalog readable when refresh fails', () async {
      final repository = _RefreshFailingCatalogRepository();
      final controller = ShopCatalogController(repository: repository);
      addTearDown(controller.dispose);

      await controller.loadInitial();
      await controller.refresh();

      expect(controller.state.isReady, isTrue);
      expect(controller.state.isStale, isTrue);
      expect(controller.state.products.single.id, '4001');
      expect(controller.state.refreshIssue?.message, '目录刷新失败');
    });

    test('appends pages with stable LongId de-duplication', () async {
      final repository = _PagedCatalogRepository();
      final controller = ShopCatalogController(repository: repository);
      addTearDown(controller.dispose);

      await controller.loadInitial();
      await controller.loadMore();

      expect(
        controller.state.products.map((product) => product.id),
        ['4001', '4002', '4003'],
      );
      expect(controller.state.hasMore, isFalse);
    });
  });

  test('detail owner ignores a late response from the previous product',
      () async {
    final repository = _DeferredDetailRepository();
    final controller = ShopProductDetailController(repository: repository);
    addTearDown(controller.dispose);

    final olderRequest = controller.openProduct('4001');
    final newerRequest = controller.openProduct('4002');
    repository.requests['4002']!.complete(_detail('4002', 'New detail'));
    await newerRequest;
    repository.requests['4001']!.complete(_detail('4001', 'Old detail'));
    await olderRequest;

    expect(controller.state.productId, '4002');
    expect(controller.state.product?.name, 'New detail');
  });

  group('ShopPurchaseController', () {
    test('reuses the key after failure and clears the draft after success',
        () async {
      final repository = _RetryPurchaseRepository();
      var keyCount = 0;
      final controller = ShopPurchaseController(
        repository: repository,
        walletRepository: const _ReadyWalletRepository(),
        keyBuilder: () => 'purchase-key-${++keyCount}',
      );
      addTearDown(controller.dispose);
      controller.updateTarget(
        productId: '4001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await pumpEventQueue();
      controller.markPaymentDraft(true);

      final firstOutcome = await controller.submit(
        paymentPassword: '123456',
      );
      final secondOutcome = await controller.submit(
        paymentPassword: '123456',
      );

      expect(firstOutcome, isNull);
      expect(secondOutcome?.orderId, '9001');
      expect(repository.idempotencyKeys, ['purchase-key-1', 'purchase-key-1']);
      expect(controller.state.isDirty, isFalse);
      expect(controller.state.transactionIssue, isNull);
    });

    test('eligibility refresh does not erase a transaction issue', () async {
      final controller = ShopPurchaseController(
        repository: _RetryPurchaseRepository(),
        walletRepository: const _ReadyWalletRepository(),
      );
      addTearDown(controller.dispose);
      controller.updateTarget(
        productId: '4001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await pumpEventQueue();

      await controller.submit(paymentPassword: 'bad');
      final transactionMessage = controller.state.transactionIssue?.message;
      await controller.refreshEligibility();

      expect(transactionMessage, '支付口令必须是 6 位数字。');
      expect(controller.state.transactionIssue?.message, transactionMessage);
      expect(controller.state.buyCheck?.canBuy, isTrue);
    });

    test('isolates an in-flight purchase from a new product and account',
        () async {
      final repository = _IsolatedPurchaseRepository();
      var keyCount = 0;
      final controller = ShopPurchaseController(
        repository: repository,
        walletRepository: const _ReadyWalletRepository(),
        keyBuilder: () => 'isolated-key-${++keyCount}',
      );
      addTearDown(controller.dispose);
      controller.updateTarget(
        productId: '4001',
        accessToken: 'token-a',
        accountId: 'user-a',
      );
      await pumpEventQueue();
      controller.markPaymentDraft(true);

      final oldPurchase = controller.submit(paymentPassword: '123456');
      await pumpEventQueue();
      expect(controller.state.isPurchasing, isTrue);
      expect(controller.state.canLeave, isFalse);

      controller.updateTarget(
        productId: '4002',
        accessToken: 'token-b',
        accountId: 'user-b',
      );
      repository.firstPurchase.complete(
        const ShopPurchaseResult(
          success: true,
          orderId: '9001',
          orderNo: 'OLD-ORDER',
        ),
      );
      expect(await oldPurchase, isNull);

      await pumpEventQueue();
      final newOutcome = await controller.submit(paymentPassword: '123456');
      expect(newOutcome?.orderId, '9002');
      expect(controller.state.productId, '4002');
      expect(controller.state.accountId, 'user-b');
      expect(controller.state.result?.orderNo, 'NEW-ORDER');
      expect(repository.idempotencyKeys, ['isolated-key-1', 'isolated-key-2']);
    });
  });
}

class _DeferredCatalogRepository extends _ShopRepositoryStub {
  final List<Completer<ShopProductPage>> requests = [];

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    final request = Completer<ShopProductPage>();
    requests.add(request);
    return request.future;
  }
}

class _RefreshFailingCatalogRepository extends _ShopRepositoryStub {
  int requestCount = 0;

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    requestCount++;
    if (requestCount > 1) {
      throw const RadishApiClientException('目录刷新失败');
    }
    return _productPage(products: [_summary('4001', 'Stable product')]);
  }
}

class _PagedCatalogRepository extends _ShopRepositoryStub {
  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    if (pageIndex == 1) {
      return _productPage(
        page: 1,
        pageCount: 2,
        dataCount: 3,
        products: [
          _summary('4001', 'One'),
          _summary('4002', 'Two'),
        ],
      );
    }
    return _productPage(
      page: 2,
      pageCount: 2,
      dataCount: 3,
      products: [
        _summary('4002', 'Two duplicate'),
        _summary('4003', 'Three'),
      ],
    );
  }
}

class _DeferredDetailRepository extends _ShopRepositoryStub {
  final Map<String, Completer<ShopProductDetail>> requests = {};

  @override
  Future<ShopProductDetail> getProductDetail({required String productId}) {
    final request = Completer<ShopProductDetail>();
    requests[productId] = request;
    return request.future;
  }
}

class _RetryPurchaseRepository extends _ShopRepositoryStub {
  final List<String> idempotencyKeys = [];

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
    idempotencyKeys.add(idempotencyKey);
    if (idempotencyKeys.length == 1) {
      return const ShopPurchaseResult(
        success: false,
        errorMessage: '请求处理中，请使用相同键重试',
      );
    }
    return const ShopPurchaseResult(
      success: true,
      orderId: '9001',
      orderNo: 'ORDER-9001',
    );
  }
}

class _IsolatedPurchaseRepository extends _ShopRepositoryStub {
  final Completer<ShopPurchaseResult> firstPurchase =
      Completer<ShopPurchaseResult>();
  final List<String> idempotencyKeys = [];

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
  }) {
    idempotencyKeys.add(idempotencyKey);
    if (idempotencyKeys.length == 1) {
      return firstPurchase.future;
    }
    return Future.value(
      const ShopPurchaseResult(
        success: true,
        orderId: '9002',
        orderNo: 'NEW-ORDER',
      ),
    );
  }
}

class _ReadyWalletRepository extends _WalletRepositoryStub {
  const _ReadyWalletRepository();

  @override
  Future<CoinBalance> getBalance({required String accessToken}) async {
    return _balance();
  }
}

class _ShopRepositoryStub implements ShopRepository {
  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<ShopProductDetail> getProductDetail({required String productId}) {
    throw UnimplementedError();
  }

  @override
  Future<ShopProductBuyCheckResult> checkCanBuy({
    required String accessToken,
    required String productId,
    int quantity = 1,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<ShopPurchaseResult> purchaseProduct({
    required String accessToken,
    required String productId,
    required String paymentPassword,
    required String idempotencyKey,
    int quantity = 1,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    throw UnimplementedError();
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
    throw UnimplementedError();
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    throw UnimplementedError();
  }
}

class _WalletRepositoryStub implements WalletRepository {
  const _WalletRepositoryStub();

  @override
  Future<CoinBalance> getBalance({required String accessToken}) {
    throw UnimplementedError();
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

ShopProductPage _productPage({
  int page = 1,
  int pageCount = 1,
  int? dataCount,
  required List<ShopProductSummary> products,
}) {
  return ShopProductPage(
    page: page,
    pageSize: 20,
    dataCount: dataCount ?? products.length,
    pageCount: pageCount,
    products: products,
  );
}

ShopProductSummary _summary(String id, String name) {
  return ShopProductSummary(
    id: id,
    name: name,
    productType: '消耗品',
    price: 120,
  );
}

ShopProductDetail _detail(String id, String name) {
  return ShopProductDetail(
    id: id,
    name: name,
    productType: '消耗品',
    price: 120,
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

CoinBalance _balance() {
  return const CoinBalance(
    userId: 'user-a',
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
