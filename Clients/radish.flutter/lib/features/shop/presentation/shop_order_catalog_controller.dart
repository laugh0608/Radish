import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_issue.dart';

enum ShopOrderCatalogStatus { idle, loading, ready, unavailable }

class ShopOrderCatalogState {
  const ShopOrderCatalogState({
    required this.status,
    this.accountId,
    this.orders = const <ShopOrderSummary>[],
    this.pageIndex = 1,
    this.pageSize = 20,
    this.pageCount = 1,
    this.dataCount = 0,
    this.isRefreshing = false,
    this.isAppending = false,
    this.issue,
    this.refreshIssue,
    this.appendIssue,
  });

  const ShopOrderCatalogState.idle({int pageSize = 20})
      : this(status: ShopOrderCatalogStatus.idle, pageSize: pageSize);

  final ShopOrderCatalogStatus status;
  final String? accountId;
  final List<ShopOrderSummary> orders;
  final int pageIndex;
  final int pageSize;
  final int pageCount;
  final int dataCount;
  final bool isRefreshing;
  final bool isAppending;
  final ShopIssue? issue;
  final ShopIssue? refreshIssue;
  final ShopIssue? appendIssue;

  bool get isIdle => status == ShopOrderCatalogStatus.idle;
  bool get isLoading => status == ShopOrderCatalogStatus.loading;
  bool get isReady => status == ShopOrderCatalogStatus.ready;
  bool get isUnavailable => status == ShopOrderCatalogStatus.unavailable;
  bool get isEmpty => isReady && orders.isEmpty;
  bool get isStale => orders.isNotEmpty && refreshIssue != null;
  bool get isBusy => isLoading || isRefreshing || isAppending;
  bool get hasMore => pageIndex < pageCount;
}

enum _ShopOrderCatalogLoadMode { initial, refresh, append }

class ShopOrderCatalogController extends ChangeNotifier {
  ShopOrderCatalogController({
    required ShopRepository repository,
    int pageSize = 20,
  })  : _repository = repository,
        _state = ShopOrderCatalogState.idle(pageSize: pageSize);

  final ShopRepository _repository;
  ShopOrderCatalogState _state;
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  ShopOrderCatalogState get state => _state;

  Future<void> openAccount({
    required String accessToken,
    String? accountId,
  }) {
    final normalizedAccessToken = _normalize(accessToken);
    if (normalizedAccessToken == null) {
      _commitInvalidAccount();
      return Future<void>.value();
    }

    final normalizedAccountId = _normalize(accountId) ?? normalizedAccessToken;
    final accountChanged = normalizedAccountId != _accountId;
    final credentialChanged = normalizedAccessToken != _accessToken;
    _accountId = normalizedAccountId;
    _accessToken = normalizedAccessToken;

    if (accountChanged || _state.isIdle || _state.isUnavailable) {
      return _load(pageIndex: 1, mode: _ShopOrderCatalogLoadMode.initial);
    }
    if (credentialChanged) {
      return _load(pageIndex: 1, mode: _ShopOrderCatalogLoadMode.refresh);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isBusy || _accountId == null || _accessToken == null) {
      return Future<void>.value();
    }
    return _load(pageIndex: 1, mode: _ShopOrderCatalogLoadMode.refresh);
  }

  Future<void> loadMore() {
    if (!_state.hasMore ||
        _state.isBusy ||
        _accountId == null ||
        _accessToken == null) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: _state.pageIndex + 1,
      mode: _ShopOrderCatalogLoadMode.append,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required _ShopOrderCatalogLoadMode mode,
  }) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      _commitInvalidAccount();
      return;
    }

    final generation = ++_generation;
    _state = switch (mode) {
      _ShopOrderCatalogLoadMode.initial => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.loading,
          accountId: accountId,
          pageSize: _state.pageSize,
        ),
      _ShopOrderCatalogLoadMode.refresh => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.ready,
          accountId: accountId,
          orders: _state.orders,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isRefreshing: true,
        ),
      _ShopOrderCatalogLoadMode.append => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.ready,
          accountId: accountId,
          orders: _state.orders,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isAppending: true,
        ),
    };
    _notify();

    try {
      final page = await _repository.getMyOrders(
        accessToken: accessToken,
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );
      if (!_canCommit(generation, accountId)) {
        return;
      }
      final orders = mode == _ShopOrderCatalogLoadMode.append
          ? _mergeOrders(_state.orders, page.orders)
          : _mergeOrders(const <ShopOrderSummary>[], page.orders);
      _state = ShopOrderCatalogState(
        status: ShopOrderCatalogStatus.ready,
        accountId: accountId,
        orders: orders,
        pageIndex: page.page,
        pageSize: _state.pageSize,
        pageCount: page.pageCount,
        dataCount: page.dataCount,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(generation, accountId, mode, ShopIssue.fromApi(error));
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        mode,
        ShopIssue.invalidResponse(error, resourceLabel: '订单列表'),
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    _ShopOrderCatalogLoadMode mode,
    ShopIssue issue,
  ) {
    if (!_canCommit(generation, accountId)) {
      return;
    }
    _state = switch (mode) {
      _ShopOrderCatalogLoadMode.initial => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.unavailable,
          accountId: accountId,
          pageSize: _state.pageSize,
          issue: issue,
        ),
      _ShopOrderCatalogLoadMode.refresh => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.ready,
          accountId: accountId,
          orders: _state.orders,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          refreshIssue: issue,
        ),
      _ShopOrderCatalogLoadMode.append => ShopOrderCatalogState(
          status: ShopOrderCatalogStatus.ready,
          accountId: accountId,
          orders: _state.orders,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          appendIssue: issue,
        ),
    };
    _notify();
  }

  void _commitInvalidAccount() {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = ShopOrderCatalogState(
      status: ShopOrderCatalogStatus.unavailable,
      pageSize: _state.pageSize,
      issue: const ShopIssue(
        kind: ShopIssueKind.request,
        message: '请先登录后查看订单。',
        code: 'Shop.MissingAccount',
      ),
    );
    _notify();
  }

  List<ShopOrderSummary> _mergeOrders(
    List<ShopOrderSummary> current,
    List<ShopOrderSummary> incoming,
  ) {
    final merged = <ShopOrderSummary>[];
    final seen = <String>{};
    for (final order in <ShopOrderSummary>[...current, ...incoming]) {
      final normalizedId = normalizeShopPositiveLongId(order.id);
      final key = normalizedId ?? 'invalid:${order.id.trim()}';
      if (seen.add(key)) {
        merged.add(order);
      }
    }
    return List<ShopOrderSummary>.unmodifiable(merged);
  }

  bool _canCommit(int generation, String accountId) {
    return !_isDisposed && generation == _generation && accountId == _accountId;
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

String? _normalize(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}
