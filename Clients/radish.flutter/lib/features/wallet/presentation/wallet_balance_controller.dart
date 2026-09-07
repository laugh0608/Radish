import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/wallet_models.dart';
import '../data/wallet_repository.dart';
import 'wallet_issue.dart';

enum WalletBalanceStatus { idle, loading, ready, unavailable, stale }

class WalletBalanceState {
  const WalletBalanceState({
    required this.status,
    this.accountId,
    this.balance,
    this.isRefreshing = false,
    this.issue,
  });

  const WalletBalanceState.idle() : this(status: WalletBalanceStatus.idle);

  final WalletBalanceStatus status;
  final String? accountId;
  final CoinBalance? balance;
  final bool isRefreshing;
  final WalletIssue? issue;

  bool get isIdle => status == WalletBalanceStatus.idle;
  bool get isLoading => status == WalletBalanceStatus.loading;
  bool get isReady => status == WalletBalanceStatus.ready;
  bool get isUnavailable => status == WalletBalanceStatus.unavailable;
  bool get isStale => status == WalletBalanceStatus.stale;
  bool get hasBalance => balance != null;
  bool get isBusy => isLoading || isRefreshing;
}

class WalletBalanceController extends ChangeNotifier {
  WalletBalanceController({required WalletRepository repository})
      : _repository = repository;

  final WalletRepository _repository;
  WalletBalanceState _state = const WalletBalanceState.idle();
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  WalletBalanceState get state => _state;

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
      return _load(preserveCurrent: false);
    }
    if (credentialChanged) {
      return _load(preserveCurrent: _state.hasBalance);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isBusy || _accountId == null || _accessToken == null) {
      return Future<void>.value();
    }
    return _load(preserveCurrent: _state.hasBalance);
  }

  Future<void> _load({required bool preserveCurrent}) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    final currentBalance = preserveCurrent ? _state.balance : null;
    _state = WalletBalanceState(
      status: preserveCurrent
          ? WalletBalanceStatus.ready
          : WalletBalanceStatus.loading,
      accountId: accountId,
      balance: currentBalance,
      isRefreshing: preserveCurrent,
    );
    _notify();

    try {
      final balance = await _repository.getBalance(accessToken: accessToken);
      if (!_canCommit(generation, accountId)) {
        return;
      }
      _state = WalletBalanceState(
        status: WalletBalanceStatus.ready,
        accountId: accountId,
        balance: balance,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        WalletIssue.fromApi(error),
        preserveCurrent: preserveCurrent,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        WalletIssue.invalidResponse(error, resourceLabel: '胡萝卜余额'),
        preserveCurrent: preserveCurrent,
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    WalletIssue issue, {
    required bool preserveCurrent,
  }) {
    if (!_canCommit(generation, accountId)) {
      return;
    }
    _state = WalletBalanceState(
      status: preserveCurrent
          ? WalletBalanceStatus.stale
          : WalletBalanceStatus.unavailable,
      accountId: accountId,
      balance: preserveCurrent ? _state.balance : null,
      issue: issue,
    );
    _notify();
  }

  void _commitInvalidAccount() {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = WalletBalanceState(
      status: WalletBalanceStatus.unavailable,
      issue: WalletIssue.request(
        '请先登录后查看胡萝卜资产。',
        code: 'Wallet.MissingAccount',
      ),
    );
    _notify();
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
