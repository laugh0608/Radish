import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_issue.dart';

enum ShopProductDetailStatus { idle, loading, ready, unavailable, stale }

class ShopProductDetailState {
  const ShopProductDetailState({
    required this.status,
    this.productId,
    this.product,
    this.isRefreshing = false,
    this.issue,
  });

  const ShopProductDetailState.idle()
      : this(status: ShopProductDetailStatus.idle);

  final ShopProductDetailStatus status;
  final String? productId;
  final ShopProductDetail? product;
  final bool isRefreshing;
  final ShopIssue? issue;

  bool get isIdle => status == ShopProductDetailStatus.idle;
  bool get isLoading => status == ShopProductDetailStatus.loading;
  bool get isReady => status == ShopProductDetailStatus.ready;
  bool get isUnavailable => status == ShopProductDetailStatus.unavailable;
  bool get isStale => status == ShopProductDetailStatus.stale;
  bool get hasProduct => product != null;
}

class ShopProductDetailController extends ChangeNotifier {
  ShopProductDetailController({required ShopRepository repository})
      : _repository = repository;

  final ShopRepository _repository;
  ShopProductDetailState _state = const ShopProductDetailState.idle();
  int _generation = 0;
  bool _isDisposed = false;

  ShopProductDetailState get state => _state;

  Future<void> openProduct(String productId) {
    final normalizedProductId = normalizeShopPositiveLongId(productId);
    if (normalizedProductId == null) {
      _generation++;
      _state = const ShopProductDetailState(
        status: ShopProductDetailStatus.unavailable,
        issue: ShopIssue(
          kind: ShopIssueKind.request,
          message: '商品详情入口缺少有效商品 ID。',
          code: 'Shop.InvalidProductId',
        ),
      );
      _notify();
      return Future<void>.value();
    }
    if (_state.productId == normalizedProductId && !_state.isUnavailable) {
      return Future<void>.value();
    }
    return _load(normalizedProductId, preserveCurrentProduct: false);
  }

  Future<void> refresh() {
    final productId = _state.productId;
    if (productId == null) {
      return Future<void>.value();
    }
    return _load(
      productId,
      preserveCurrentProduct: _state.product != null,
    );
  }

  void close() {
    _generation++;
    _state = const ShopProductDetailState.idle();
    _notify();
  }

  Future<void> _load(
    String productId, {
    required bool preserveCurrentProduct,
  }) async {
    final generation = ++_generation;
    _state = ShopProductDetailState(
      status: preserveCurrentProduct
          ? ShopProductDetailStatus.ready
          : ShopProductDetailStatus.loading,
      productId: productId,
      product: preserveCurrentProduct ? _state.product : null,
      isRefreshing: preserveCurrentProduct,
    );
    _notify();

    try {
      final product = await _repository.getProductDetail(productId: productId);
      if (!_canCommit(generation, productId)) {
        return;
      }
      _state = ShopProductDetailState(
        status: ShopProductDetailStatus.ready,
        productId: productId,
        product: product,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        productId,
        ShopIssue.fromApi(error),
        preserveCurrentProduct: preserveCurrentProduct,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        productId,
        ShopIssue.invalidResponse(error, resourceLabel: '商品详情'),
        preserveCurrentProduct: preserveCurrentProduct,
      );
    }
  }

  void _commitIssue(
    int generation,
    String productId,
    ShopIssue issue, {
    required bool preserveCurrentProduct,
  }) {
    if (!_canCommit(generation, productId)) {
      return;
    }
    _state = ShopProductDetailState(
      status: preserveCurrentProduct
          ? ShopProductDetailStatus.stale
          : ShopProductDetailStatus.unavailable,
      productId: productId,
      product: preserveCurrentProduct ? _state.product : null,
      issue: issue,
    );
    _notify();
  }

  bool _canCommit(int generation, String productId) {
    return !_isDisposed &&
        generation == _generation &&
        _state.productId == productId;
  }

  void _notify() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    _generation++;
    super.dispose();
  }
}
