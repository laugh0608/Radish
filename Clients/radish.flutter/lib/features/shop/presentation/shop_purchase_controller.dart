import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../../wallet/data/wallet_models.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_issue.dart';

typedef ShopPurchaseKeyBuilder = String Function();

class ShopPurchaseOutcome {
  const ShopPurchaseOutcome({
    required this.result,
    this.orderId,
  });

  final ShopPurchaseResult result;
  final String? orderId;
}

class ShopPurchaseState {
  const ShopPurchaseState({
    this.productId,
    this.accountId,
    this.isAuthenticated = false,
    this.buyCheck,
    this.buyCheckIssue,
    this.isCheckingBuy = false,
    this.balance,
    this.balanceIssue,
    this.isLoadingBalance = false,
    this.isPurchasing = false,
    this.hasPaymentDraft = false,
    this.hasPendingSignIn = false,
    this.notice,
    this.transactionIssue,
    this.result,
  });

  final String? productId;
  final String? accountId;
  final bool isAuthenticated;
  final ShopProductBuyCheckResult? buyCheck;
  final ShopIssue? buyCheckIssue;
  final bool isCheckingBuy;
  final CoinBalance? balance;
  final ShopIssue? balanceIssue;
  final bool isLoadingBalance;
  final bool isPurchasing;
  final bool hasPaymentDraft;
  final bool hasPendingSignIn;
  final String? notice;
  final ShopIssue? transactionIssue;
  final ShopPurchaseResult? result;

  bool get isDirty => hasPaymentDraft;
  bool get canLeave => !isPurchasing && !isDirty;
  bool get isEligibilityStale => buyCheck != null && buyCheckIssue != null;
  bool get isBalanceStale => balance != null && balanceIssue != null;
}

class ShopPurchaseController extends ChangeNotifier {
  ShopPurchaseController({
    required ShopRepository repository,
    required WalletRepository walletRepository,
    ShopPurchaseKeyBuilder? keyBuilder,
  })  : _repository = repository,
        _walletRepository = walletRepository,
        _keyBuilder = keyBuilder ?? _buildPurchaseIdempotencyKey;

  final ShopRepository _repository;
  final WalletRepository _walletRepository;
  final ShopPurchaseKeyBuilder _keyBuilder;

  ShopPurchaseState _state = const ShopPurchaseState();
  String? _accessToken;
  String? _idempotencyKey;
  int _targetGeneration = 0;
  int _buyCheckGeneration = 0;
  int _balanceGeneration = 0;
  bool _isDisposed = false;

  ShopPurchaseState get state => _state;

  void updateTarget({
    required String productId,
    String? accessToken,
    String? accountId,
  }) {
    final normalizedProductId = normalizeShopPositiveLongId(productId);
    final normalizedAccessToken = _normalize(accessToken);
    final normalizedAccountId = normalizedAccessToken == null
        ? null
        : _normalize(accountId) ?? normalizedAccessToken;
    final tokenChanged = normalizedAccessToken != _accessToken;
    final productChanged = normalizedProductId != _state.productId;
    final accountChanged = normalizedAccountId != _state.accountId;
    final completedPendingSignIn = _state.hasPendingSignIn &&
        _state.accountId == null &&
        normalizedAccountId != null &&
        !productChanged;

    _accessToken = normalizedAccessToken;
    if (!productChanged && !accountChanged) {
      if (tokenChanged &&
          normalizedProductId != null &&
          normalizedAccountId != null) {
        unawaited(refreshPrivate());
      }
      return;
    }

    _targetGeneration++;
    _buyCheckGeneration++;
    _balanceGeneration++;
    _idempotencyKey = null;
    _state = ShopPurchaseState(
      productId: normalizedProductId,
      accountId: normalizedAccountId,
      isAuthenticated: normalizedAccountId != null,
      notice: completedPendingSignIn ? '已回到商品详情，可以继续确认购买。' : null,
    );
    _notify();

    if (normalizedProductId != null && normalizedAccountId != null) {
      unawaited(refreshPrivate(preserveCurrent: false));
    }
  }

  void clearTarget() {
    _targetGeneration++;
    _buyCheckGeneration++;
    _balanceGeneration++;
    _accessToken = null;
    _idempotencyKey = null;
    _state = const ShopPurchaseState();
    _notify();
  }

  void markPaymentDraft(bool hasDraft) {
    if (_state.hasPaymentDraft == hasDraft) {
      return;
    }
    _state = _copyState(hasPaymentDraft: hasDraft);
    _notify();
  }

  void discardPaymentDraft() {
    _idempotencyKey = null;
    _state = _copyState(
      hasPaymentDraft: false,
      notice: null,
      transactionIssue: null,
      result: null,
    );
    _notify();
  }

  void beginSignIn() {
    _state = _copyState(
      hasPendingSignIn: true,
      notice: null,
      transactionIssue: null,
    );
    _notify();
  }

  void cancelPendingSignIn({String message = '登录未完成，请重试后继续购买。'}) {
    if (!_state.hasPendingSignIn || _state.isAuthenticated) {
      return;
    }
    _state = _copyState(
      hasPendingSignIn: false,
      transactionIssue: ShopIssue.request(message),
    );
    _notify();
  }

  Future<void> refreshPrivate({bool preserveCurrent = true}) async {
    if (!_state.isAuthenticated || _state.productId == null) {
      return;
    }
    await Future.wait<void>([
      refreshEligibility(preserveCurrent: preserveCurrent),
      refreshBalance(preserveCurrent: preserveCurrent),
    ]);
  }

  Future<ShopProductBuyCheckResult?> refreshEligibility({
    bool preserveCurrent = true,
  }) async {
    final productId = _state.productId;
    final accessToken = _accessToken;
    if (productId == null || accessToken == null) {
      return null;
    }
    final targetGeneration = _targetGeneration;
    final requestGeneration = ++_buyCheckGeneration;
    _state = _copyState(
      isCheckingBuy: true,
      buyCheck: preserveCurrent ? _state.buyCheck : null,
      buyCheckIssue: null,
    );
    _notify();

    try {
      final result = await _repository.checkCanBuy(
        accessToken: accessToken,
        productId: productId,
      );
      if (!_canCommitPrivate(
        targetGeneration,
        requestGeneration,
        _buyCheckGeneration,
      )) {
        return null;
      }
      _state = _copyState(
        buyCheck: result,
        buyCheckIssue: null,
        isCheckingBuy: false,
      );
      _notify();
      return result;
    } on RadishApiClientException catch (error) {
      _commitBuyCheckIssue(
        targetGeneration,
        requestGeneration,
        ShopIssue.fromApi(error),
        preserveCurrent: preserveCurrent,
      );
    } on FormatException catch (error) {
      _commitBuyCheckIssue(
        targetGeneration,
        requestGeneration,
        ShopIssue.invalidResponse(error, resourceLabel: '购买资格'),
        preserveCurrent: preserveCurrent,
      );
    }
    return null;
  }

  Future<CoinBalance?> refreshBalance({bool preserveCurrent = true}) async {
    final accessToken = _accessToken;
    if (accessToken == null || !_state.isAuthenticated) {
      return null;
    }
    final targetGeneration = _targetGeneration;
    final requestGeneration = ++_balanceGeneration;
    _state = _copyState(
      isLoadingBalance: true,
      balance: preserveCurrent ? _state.balance : null,
      balanceIssue: null,
    );
    _notify();

    try {
      final balance = await _walletRepository.getBalance(
        accessToken: accessToken,
      );
      if (!_canCommitPrivate(
        targetGeneration,
        requestGeneration,
        _balanceGeneration,
      )) {
        return null;
      }
      _state = _copyState(
        balance: balance,
        balanceIssue: null,
        isLoadingBalance: false,
      );
      _notify();
      return balance;
    } on RadishApiClientException catch (error) {
      _commitBalanceIssue(
        targetGeneration,
        requestGeneration,
        ShopIssue.fromApi(error),
        preserveCurrent: preserveCurrent,
      );
    } on FormatException catch (error) {
      _commitBalanceIssue(
        targetGeneration,
        requestGeneration,
        ShopIssue.invalidResponse(error, resourceLabel: '余额'),
        preserveCurrent: preserveCurrent,
      );
    }
    return null;
  }

  Future<ShopPurchaseOutcome?> submit({required String paymentPassword}) async {
    if (_state.isPurchasing) {
      return null;
    }
    final productId = _state.productId;
    final accessToken = _accessToken;
    if (productId == null) {
      _setTransactionIssue('商品 ID 不是规范 LongId，无法发起购买。');
      return null;
    }
    if (accessToken == null) {
      _setTransactionIssue('请先登录后购买商品。');
      return null;
    }
    final normalizedPassword = paymentPassword.trim();
    if (normalizedPassword.isEmpty) {
      _setTransactionIssue('请输入支付口令。');
      return null;
    }
    if (!RegExp(r'^\d{6}$').hasMatch(normalizedPassword)) {
      _setTransactionIssue('支付口令必须是 6 位数字。');
      return null;
    }

    var buyCheck = _state.buyCheck;
    if (buyCheck == null || !buyCheck.canBuy) {
      buyCheck = await refreshEligibility(preserveCurrent: true);
    }
    if (buyCheck == null || !_state.isAuthenticated) {
      return null;
    }
    if (!buyCheck.canBuy) {
      _setTransactionIssue(
        buyCheck.reason?.trim().isNotEmpty == true
            ? buyCheck.reason!
            : '当前商品暂不可购买。',
      );
      return null;
    }

    final targetGeneration = _targetGeneration;
    final accountId = _state.accountId;
    _idempotencyKey ??= _keyBuilder();
    final idempotencyKey = _idempotencyKey!;
    _state = _copyState(
      isPurchasing: true,
      notice: null,
      transactionIssue: null,
      result: null,
    );
    _notify();

    try {
      final result = await _repository.purchaseProduct(
        accessToken: accessToken,
        productId: productId,
        paymentPassword: normalizedPassword,
        idempotencyKey: idempotencyKey,
      );
      if (!_canCommitPurchase(targetGeneration, productId, accountId)) {
        return null;
      }
      if (!result.success) {
        _state = _copyState(
          isPurchasing: false,
          result: result,
          transactionIssue: ShopIssue.request(
            result.errorMessage?.trim().isNotEmpty == true
                ? result.errorMessage!
                : '购买失败，请稍后重试。',
            code: result.errorCode,
          ),
        );
        _notify();
        return null;
      }

      _idempotencyKey = null;
      final orderId = normalizeShopPositiveLongId(result.orderId);
      _state = _copyState(
        isPurchasing: false,
        hasPaymentDraft: false,
        result: result,
        notice: _buildPurchaseSuccessNotice(result),
        transactionIssue: orderId == null
            ? ShopIssue.request(
                '购买成功，但返回的订单 ID 不是规范 LongId，已留在商品详情，请到订单列表核对。',
                code: 'Shop.InvalidOrderId',
              )
            : null,
      );
      _notify();
      unawaited(refreshBalance(preserveCurrent: true));
      return ShopPurchaseOutcome(result: result, orderId: orderId);
    } on RadishApiClientException catch (error) {
      _commitPurchaseIssue(
        targetGeneration,
        productId,
        accountId,
        ShopIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _commitPurchaseIssue(
        targetGeneration,
        productId,
        accountId,
        ShopIssue.invalidResponse(error, resourceLabel: '购买'),
      );
    }
    return null;
  }

  void _commitBuyCheckIssue(
    int targetGeneration,
    int requestGeneration,
    ShopIssue issue, {
    required bool preserveCurrent,
  }) {
    if (!_canCommitPrivate(
      targetGeneration,
      requestGeneration,
      _buyCheckGeneration,
    )) {
      return;
    }
    _state = _copyState(
      buyCheck: preserveCurrent ? _state.buyCheck : null,
      buyCheckIssue: issue,
      isCheckingBuy: false,
    );
    _notify();
  }

  void _commitBalanceIssue(
    int targetGeneration,
    int requestGeneration,
    ShopIssue issue, {
    required bool preserveCurrent,
  }) {
    if (!_canCommitPrivate(
      targetGeneration,
      requestGeneration,
      _balanceGeneration,
    )) {
      return;
    }
    _state = _copyState(
      balance: preserveCurrent ? _state.balance : null,
      balanceIssue: issue,
      isLoadingBalance: false,
    );
    _notify();
  }

  void _commitPurchaseIssue(
    int targetGeneration,
    String productId,
    String? accountId,
    ShopIssue issue,
  ) {
    if (!_canCommitPurchase(targetGeneration, productId, accountId)) {
      return;
    }
    _state = _copyState(
      isPurchasing: false,
      transactionIssue: issue,
    );
    _notify();
  }

  void _setTransactionIssue(String message) {
    _state = _copyState(
      notice: null,
      transactionIssue: ShopIssue.request(message),
    );
    _notify();
  }

  bool _canCommitPrivate(
    int targetGeneration,
    int requestGeneration,
    int currentRequestGeneration,
  ) {
    return !_isDisposed &&
        targetGeneration == _targetGeneration &&
        requestGeneration == currentRequestGeneration;
  }

  bool _canCommitPurchase(
    int targetGeneration,
    String productId,
    String? accountId,
  ) {
    return !_isDisposed &&
        targetGeneration == _targetGeneration &&
        productId == _state.productId &&
        accountId == _state.accountId;
  }

  ShopPurchaseState _copyState({
    Object? productId = _unset,
    Object? accountId = _unset,
    bool? isAuthenticated,
    Object? buyCheck = _unset,
    Object? buyCheckIssue = _unset,
    bool? isCheckingBuy,
    Object? balance = _unset,
    Object? balanceIssue = _unset,
    bool? isLoadingBalance,
    bool? isPurchasing,
    bool? hasPaymentDraft,
    bool? hasPendingSignIn,
    Object? notice = _unset,
    Object? transactionIssue = _unset,
    Object? result = _unset,
  }) {
    return ShopPurchaseState(
      productId: identical(productId, _unset)
          ? _state.productId
          : productId as String?,
      accountId: identical(accountId, _unset)
          ? _state.accountId
          : accountId as String?,
      isAuthenticated: isAuthenticated ?? _state.isAuthenticated,
      buyCheck: identical(buyCheck, _unset)
          ? _state.buyCheck
          : buyCheck as ShopProductBuyCheckResult?,
      buyCheckIssue: identical(buyCheckIssue, _unset)
          ? _state.buyCheckIssue
          : buyCheckIssue as ShopIssue?,
      isCheckingBuy: isCheckingBuy ?? _state.isCheckingBuy,
      balance:
          identical(balance, _unset) ? _state.balance : balance as CoinBalance?,
      balanceIssue: identical(balanceIssue, _unset)
          ? _state.balanceIssue
          : balanceIssue as ShopIssue?,
      isLoadingBalance: isLoadingBalance ?? _state.isLoadingBalance,
      isPurchasing: isPurchasing ?? _state.isPurchasing,
      hasPaymentDraft: hasPaymentDraft ?? _state.hasPaymentDraft,
      hasPendingSignIn: hasPendingSignIn ?? _state.hasPendingSignIn,
      notice: identical(notice, _unset) ? _state.notice : notice as String?,
      transactionIssue: identical(transactionIssue, _unset)
          ? _state.transactionIssue
          : transactionIssue as ShopIssue?,
      result: identical(result, _unset)
          ? _state.result
          : result as ShopPurchaseResult?,
    );
  }

  void _notify() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    _targetGeneration++;
    _buyCheckGeneration++;
    _balanceGeneration++;
    _idempotencyKey = null;
    super.dispose();
  }
}

const Object _unset = Object();

final Random _purchaseKeyRandom = _createPurchaseKeyRandom();

Random _createPurchaseKeyRandom() {
  try {
    return Random.secure();
  } on UnsupportedError {
    return Random();
  }
}

String _buildPurchaseIdempotencyKey() {
  final timestamp = DateTime.now().microsecondsSinceEpoch.toRadixString(16);
  final randomPart = List.generate(
    4,
    (_) =>
        _purchaseKeyRandom.nextInt(1 << 32).toRadixString(16).padLeft(8, '0'),
  ).join();
  return 'shop:$timestamp-$randomPart';
}

String _buildPurchaseSuccessNotice(ShopPurchaseResult result) {
  final orderText = result.orderNo?.trim().isNotEmpty == true
      ? '订单号 ${result.orderNo}'
      : result.orderId?.trim().isNotEmpty == true
          ? '订单 ${result.orderId}'
          : '服务端未返回订单 ID';
  final deducted = result.deductedCoins;
  final remaining = result.remainingBalance;
  if (deducted != null && remaining != null) {
    return '购买成功，$orderText。已扣除 $deducted 胡萝卜，剩余 $remaining 胡萝卜。';
  }
  return '购买成功，$orderText。';
}

String? _normalize(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}
