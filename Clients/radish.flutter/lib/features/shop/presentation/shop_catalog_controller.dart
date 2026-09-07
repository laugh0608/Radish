import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_issue.dart';

enum ShopCatalogStatus { loading, ready, unavailable }

class ShopCatalogState {
  const ShopCatalogState({
    required this.status,
    required this.pageSize,
    this.products = const <ShopProductSummary>[],
    this.pageIndex = 1,
    this.pageCount = 1,
    this.dataCount = 0,
    this.isRefreshing = false,
    this.isAppending = false,
    this.issue,
    this.refreshIssue,
    this.appendIssue,
  });

  const ShopCatalogState.initial({int pageSize = 20})
      : this(status: ShopCatalogStatus.loading, pageSize: pageSize);

  final ShopCatalogStatus status;
  final int pageSize;
  final List<ShopProductSummary> products;
  final int pageIndex;
  final int pageCount;
  final int dataCount;
  final bool isRefreshing;
  final bool isAppending;
  final ShopIssue? issue;
  final ShopIssue? refreshIssue;
  final ShopIssue? appendIssue;

  bool get isLoading => status == ShopCatalogStatus.loading;
  bool get isReady => status == ShopCatalogStatus.ready;
  bool get isUnavailable => status == ShopCatalogStatus.unavailable;
  bool get isEmpty => isReady && products.isEmpty;
  bool get isStale => products.isNotEmpty && refreshIssue != null;
  bool get isBusy => isLoading || isRefreshing || isAppending;
  bool get hasMore => pageIndex < pageCount;
}

enum _ShopCatalogLoadMode { initial, refresh, append }

class ShopCatalogController extends ChangeNotifier {
  ShopCatalogController({
    required ShopRepository repository,
    int pageSize = 20,
  })  : _repository = repository,
        _state = ShopCatalogState.initial(pageSize: pageSize);

  final ShopRepository _repository;
  ShopCatalogState _state;
  int _generation = 0;
  bool _isDisposed = false;

  ShopCatalogState get state => _state;

  Future<void> loadInitial() => _load(
        pageIndex: 1,
        mode: _ShopCatalogLoadMode.initial,
      );

  Future<void> refresh() {
    if (_state.isRefreshing || _state.isAppending) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: 1,
      mode: _ShopCatalogLoadMode.refresh,
    );
  }

  Future<void> loadMore() {
    if (!_state.hasMore || _state.isBusy) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: _state.pageIndex + 1,
      mode: _ShopCatalogLoadMode.append,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required _ShopCatalogLoadMode mode,
  }) async {
    final generation = ++_generation;
    _state = switch (mode) {
      _ShopCatalogLoadMode.initial => ShopCatalogState(
          status: ShopCatalogStatus.loading,
          pageSize: _state.pageSize,
        ),
      _ShopCatalogLoadMode.refresh => ShopCatalogState(
          status: ShopCatalogStatus.ready,
          pageSize: _state.pageSize,
          products: _state.products,
          pageIndex: _state.pageIndex,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isRefreshing: true,
        ),
      _ShopCatalogLoadMode.append => ShopCatalogState(
          status: ShopCatalogStatus.ready,
          pageSize: _state.pageSize,
          products: _state.products,
          pageIndex: _state.pageIndex,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isAppending: true,
        ),
    };
    _notify();

    try {
      final page = await _repository.getProductPage(
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );
      if (!_canCommit(generation)) {
        return;
      }
      final products = mode == _ShopCatalogLoadMode.append
          ? _mergeProducts(_state.products, page.products)
          : _mergeProducts(const <ShopProductSummary>[], page.products);
      _state = ShopCatalogState(
        status: ShopCatalogStatus.ready,
        pageSize: _state.pageSize,
        products: products,
        pageIndex: page.page,
        pageCount: page.pageCount,
        dataCount: page.dataCount,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(generation, mode, ShopIssue.fromApi(error));
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        mode,
        ShopIssue.invalidResponse(error, resourceLabel: '商品列表'),
      );
    }
  }

  void _commitIssue(
    int generation,
    _ShopCatalogLoadMode mode,
    ShopIssue issue,
  ) {
    if (!_canCommit(generation)) {
      return;
    }
    _state = switch (mode) {
      _ShopCatalogLoadMode.initial => ShopCatalogState(
          status: ShopCatalogStatus.unavailable,
          pageSize: _state.pageSize,
          issue: issue,
        ),
      _ShopCatalogLoadMode.refresh => ShopCatalogState(
          status: ShopCatalogStatus.ready,
          pageSize: _state.pageSize,
          products: _state.products,
          pageIndex: _state.pageIndex,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          refreshIssue: issue,
        ),
      _ShopCatalogLoadMode.append => ShopCatalogState(
          status: ShopCatalogStatus.ready,
          pageSize: _state.pageSize,
          products: _state.products,
          pageIndex: _state.pageIndex,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          appendIssue: issue,
        ),
    };
    _notify();
  }

  List<ShopProductSummary> _mergeProducts(
    List<ShopProductSummary> current,
    List<ShopProductSummary> incoming,
  ) {
    final merged = <ShopProductSummary>[];
    final seen = <String>{};
    for (final product in <ShopProductSummary>[...current, ...incoming]) {
      final normalizedId = normalizeShopPositiveLongId(product.id);
      final key = normalizedId ?? 'invalid:${product.id.trim()}';
      if (seen.add(key)) {
        merged.add(product);
      }
    }
    return List<ShopProductSummary>.unmodifiable(merged);
  }

  bool _canCommit(int generation) {
    return !_isDisposed && generation == _generation;
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
