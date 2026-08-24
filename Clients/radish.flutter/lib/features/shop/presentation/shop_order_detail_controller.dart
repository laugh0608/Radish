import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_issue.dart';

enum ShopOrderDetailStatus { idle, loading, ready, unavailable, stale }

class ShopOrderDetailState {
  const ShopOrderDetailState({
    required this.status,
    this.accountId,
    this.orderId,
    this.order,
    this.isRefreshing = false,
    this.issue,
  });

  const ShopOrderDetailState.idle() : this(status: ShopOrderDetailStatus.idle);

  final ShopOrderDetailStatus status;
  final String? accountId;
  final String? orderId;
  final ShopOrderDetail? order;
  final bool isRefreshing;
  final ShopIssue? issue;

  bool get isIdle => status == ShopOrderDetailStatus.idle;
  bool get isLoading => status == ShopOrderDetailStatus.loading;
  bool get isReady => status == ShopOrderDetailStatus.ready;
  bool get isUnavailable => status == ShopOrderDetailStatus.unavailable;
  bool get isStale => status == ShopOrderDetailStatus.stale;
  bool get hasOrder => order != null;
}

class ShopOrderDetailController extends ChangeNotifier {
  ShopOrderDetailController({required ShopRepository repository})
      : _repository = repository;

  final ShopRepository _repository;
  ShopOrderDetailState _state = const ShopOrderDetailState.idle();
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  ShopOrderDetailState get state => _state;

  Future<void> openOrder({
    required String orderId,
    required String accessToken,
    String? accountId,
  }) {
    final normalizedOrderId = normalizeShopPositiveLongId(orderId);
    final normalizedAccessToken = _normalize(accessToken);
    if (normalizedOrderId == null) {
      _commitInvalidTarget(
        message: '订单详情入口缺少有效订单 ID。',
        code: 'Shop.InvalidOrderId',
      );
      return Future<void>.value();
    }
    if (normalizedAccessToken == null) {
      _commitInvalidTarget(
        message: '请先登录后查看订单详情。',
        code: 'Shop.MissingAccount',
      );
      return Future<void>.value();
    }

    final normalizedAccountId = _normalize(accountId) ?? normalizedAccessToken;
    final targetChanged = normalizedOrderId != _state.orderId ||
        normalizedAccountId != _accountId;
    final credentialChanged = normalizedAccessToken != _accessToken;
    _accountId = normalizedAccountId;
    _accessToken = normalizedAccessToken;

    if (targetChanged || _state.isIdle || _state.isUnavailable) {
      return _load(normalizedOrderId, preserveCurrentOrder: false);
    }
    if (credentialChanged) {
      return _load(
        normalizedOrderId,
        preserveCurrentOrder: _state.order != null,
      );
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    final orderId = _state.orderId;
    if (orderId == null || _accountId == null || _accessToken == null) {
      return Future<void>.value();
    }
    return _load(orderId, preserveCurrentOrder: _state.order != null);
  }

  Future<void> _load(
    String orderId, {
    required bool preserveCurrentOrder,
  }) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    _state = ShopOrderDetailState(
      status: preserveCurrentOrder
          ? ShopOrderDetailStatus.ready
          : ShopOrderDetailStatus.loading,
      accountId: accountId,
      orderId: orderId,
      order: preserveCurrentOrder ? _state.order : null,
      isRefreshing: preserveCurrentOrder,
    );
    _notify();

    try {
      final order = await _repository.getOrderDetail(
        accessToken: accessToken,
        orderId: orderId,
      );
      if (!_canCommit(generation, accountId, orderId)) {
        return;
      }
      _state = ShopOrderDetailState(
        status: ShopOrderDetailStatus.ready,
        accountId: accountId,
        orderId: orderId,
        order: order,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        orderId,
        ShopIssue.fromApi(error),
        preserveCurrentOrder: preserveCurrentOrder,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        orderId,
        ShopIssue.invalidResponse(error, resourceLabel: '订单详情'),
        preserveCurrentOrder: preserveCurrentOrder,
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    String orderId,
    ShopIssue issue, {
    required bool preserveCurrentOrder,
  }) {
    if (!_canCommit(generation, accountId, orderId)) {
      return;
    }
    _state = ShopOrderDetailState(
      status: preserveCurrentOrder
          ? ShopOrderDetailStatus.stale
          : ShopOrderDetailStatus.unavailable,
      accountId: accountId,
      orderId: orderId,
      order: preserveCurrentOrder ? _state.order : null,
      issue: issue,
    );
    _notify();
  }

  void _commitInvalidTarget({required String message, required String code}) {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = ShopOrderDetailState(
      status: ShopOrderDetailStatus.unavailable,
      issue: ShopIssue(
        kind: ShopIssueKind.request,
        message: message,
        code: code,
      ),
    );
    _notify();
  }

  bool _canCommit(int generation, String accountId, String orderId) {
    return !_isDisposed &&
        generation == _generation &&
        accountId == _accountId &&
        orderId == _state.orderId;
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
