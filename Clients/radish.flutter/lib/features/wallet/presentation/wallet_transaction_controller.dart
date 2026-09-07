import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/wallet_models.dart';
import '../data/wallet_repository.dart';
import 'wallet_issue.dart';

class WalletTransactionQuery {
  const WalletTransactionQuery({
    this.transactionType,
    this.status,
    this.businessType,
    this.businessId,
  });

  factory WalletTransactionQuery.normalized({
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) {
    return WalletTransactionQuery(
      transactionType: _normalize(transactionType),
      status: _normalize(status),
      businessType: _normalize(businessType),
      businessId: _normalize(businessId),
    );
  }

  final String? transactionType;
  final String? status;
  final String? businessType;
  final String? businessId;

  bool get hasInvalidBusinessId =>
      businessId != null && _normalizePositiveLongId(businessId!) == null;

  bool get hasBusinessFilter => businessType != null && businessId != null;

  @override
  bool operator ==(Object other) {
    return other is WalletTransactionQuery &&
        other.transactionType == transactionType &&
        other.status == status &&
        other.businessType == businessType &&
        other.businessId == businessId;
  }

  @override
  int get hashCode =>
      Object.hash(transactionType, status, businessType, businessId);
}

enum WalletTransactionStatus { idle, loading, ready, unavailable }

class WalletTransactionState {
  const WalletTransactionState({
    required this.status,
    this.accountId,
    this.query = const WalletTransactionQuery(),
    this.transactions = const <CoinTransaction>[],
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

  const WalletTransactionState.idle({int pageSize = 20})
      : this(status: WalletTransactionStatus.idle, pageSize: pageSize);

  final WalletTransactionStatus status;
  final String? accountId;
  final WalletTransactionQuery query;
  final List<CoinTransaction> transactions;
  final int pageIndex;
  final int pageSize;
  final int pageCount;
  final int dataCount;
  final bool isRefreshing;
  final bool isAppending;
  final WalletIssue? issue;
  final WalletIssue? refreshIssue;
  final WalletIssue? appendIssue;

  bool get isIdle => status == WalletTransactionStatus.idle;
  bool get isLoading => status == WalletTransactionStatus.loading;
  bool get isReady => status == WalletTransactionStatus.ready;
  bool get isUnavailable => status == WalletTransactionStatus.unavailable;
  bool get isEmpty => isReady && transactions.isEmpty;
  bool get isStale => isReady && refreshIssue != null;
  bool get isBusy => isLoading || isRefreshing || isAppending;
  bool get hasMore => pageIndex < pageCount;
}

enum _WalletTransactionLoadMode { initial, refresh, append }

class WalletTransactionController extends ChangeNotifier {
  WalletTransactionController({
    required WalletRepository repository,
    int pageSize = 20,
  })  : _repository = repository,
        _state = WalletTransactionState.idle(pageSize: pageSize);

  final WalletRepository _repository;
  WalletTransactionState _state;
  String? _accessToken;
  String? _accountId;
  WalletTransactionQuery? _query;
  int _generation = 0;
  bool _isDisposed = false;

  WalletTransactionState get state => _state;

  Future<void> openAccount({
    required String accessToken,
    String? accountId,
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) {
    final normalizedAccessToken = _normalize(accessToken);
    if (normalizedAccessToken == null) {
      _commitInvalidTarget(
        message: '请先登录后查看胡萝卜流水。',
        code: 'Wallet.MissingAccount',
      );
      return Future<void>.value();
    }
    final query = WalletTransactionQuery.normalized(
      transactionType: transactionType,
      status: status,
      businessType: businessType,
      businessId: businessId,
    );
    if (query.hasInvalidBusinessId) {
      _commitInvalidTarget(
        message: '胡萝卜流水入口缺少有效业务 ID。',
        code: 'Wallet.InvalidBusinessId',
      );
      return Future<void>.value();
    }

    final normalizedAccountId = _normalize(accountId) ?? normalizedAccessToken;
    final accountChanged = normalizedAccountId != _accountId;
    final queryChanged = query != _query;
    final credentialChanged = normalizedAccessToken != _accessToken;
    _accountId = normalizedAccountId;
    _accessToken = normalizedAccessToken;
    _query = query;

    if (accountChanged ||
        queryChanged ||
        _state.isIdle ||
        _state.isUnavailable) {
      return _load(pageIndex: 1, mode: _WalletTransactionLoadMode.initial);
    }
    if (credentialChanged) {
      return _load(pageIndex: 1, mode: _WalletTransactionLoadMode.refresh);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isBusy ||
        _accountId == null ||
        _accessToken == null ||
        _query == null) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: 1,
      mode: _state.isUnavailable
          ? _WalletTransactionLoadMode.initial
          : _WalletTransactionLoadMode.refresh,
    );
  }

  Future<void> loadMore() {
    if (!_state.hasMore ||
        _state.isBusy ||
        _accountId == null ||
        _accessToken == null ||
        _query == null) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: _state.pageIndex + 1,
      mode: _WalletTransactionLoadMode.append,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required _WalletTransactionLoadMode mode,
  }) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    final query = _query;
    if (accessToken == null || accountId == null || query == null) {
      return;
    }

    final generation = ++_generation;
    _state = switch (mode) {
      _WalletTransactionLoadMode.initial => WalletTransactionState(
          status: WalletTransactionStatus.loading,
          accountId: accountId,
          query: query,
          pageSize: _state.pageSize,
        ),
      _WalletTransactionLoadMode.refresh => WalletTransactionState(
          status: WalletTransactionStatus.ready,
          accountId: accountId,
          query: query,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isRefreshing: true,
        ),
      _WalletTransactionLoadMode.append => WalletTransactionState(
          status: WalletTransactionStatus.ready,
          accountId: accountId,
          query: query,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isAppending: true,
        ),
    };
    _notify();

    try {
      final page = await _repository.getTransactions(
        accessToken: accessToken,
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
        transactionType: query.transactionType,
        status: query.status,
        businessType: query.businessType,
        businessId: query.businessId,
      );
      if (!_canCommit(generation, accountId, query)) {
        return;
      }
      final transactions = mode == _WalletTransactionLoadMode.append
          ? _mergeTransactions(_state.transactions, page.transactions)
          : _mergeTransactions(const <CoinTransaction>[], page.transactions);
      _state = WalletTransactionState(
        status: WalletTransactionStatus.ready,
        accountId: accountId,
        query: query,
        transactions: transactions,
        pageIndex: page.page,
        pageSize: _state.pageSize,
        pageCount: page.pageCount,
        dataCount: page.dataCount,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        query,
        mode,
        WalletIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        query,
        mode,
        WalletIssue.invalidResponse(error, resourceLabel: '胡萝卜流水'),
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    WalletTransactionQuery query,
    _WalletTransactionLoadMode mode,
    WalletIssue issue,
  ) {
    if (!_canCommit(generation, accountId, query)) {
      return;
    }
    _state = switch (mode) {
      _WalletTransactionLoadMode.initial => WalletTransactionState(
          status: WalletTransactionStatus.unavailable,
          accountId: accountId,
          query: query,
          pageSize: _state.pageSize,
          issue: issue,
        ),
      _WalletTransactionLoadMode.refresh => WalletTransactionState(
          status: WalletTransactionStatus.ready,
          accountId: accountId,
          query: query,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          refreshIssue: issue,
        ),
      _WalletTransactionLoadMode.append => WalletTransactionState(
          status: WalletTransactionStatus.ready,
          accountId: accountId,
          query: query,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          appendIssue: issue,
        ),
    };
    _notify();
  }

  void _commitInvalidTarget({required String message, required String code}) {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _query = null;
    _state = WalletTransactionState(
      status: WalletTransactionStatus.unavailable,
      pageSize: _state.pageSize,
      issue: WalletIssue.request(message, code: code),
    );
    _notify();
  }

  List<CoinTransaction> _mergeTransactions(
    List<CoinTransaction> current,
    List<CoinTransaction> incoming,
  ) {
    final merged = <CoinTransaction>[];
    final seen = <String>{};
    for (final transaction in <CoinTransaction>[...current, ...incoming]) {
      final id = _normalize(transaction.id);
      final transactionNo = _normalize(transaction.transactionNo);
      final key = id ??
          transactionNo ??
          'fallback:${transaction.createTime}:${transaction.amount}:${transaction.businessId}';
      if (seen.add(key)) {
        merged.add(transaction);
      }
    }
    return List<CoinTransaction>.unmodifiable(merged);
  }

  bool _canCommit(
    int generation,
    String accountId,
    WalletTransactionQuery query,
  ) {
    return !_isDisposed &&
        generation == _generation &&
        accountId == _accountId &&
        query == _query;
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

String? _normalizePositiveLongId(String value) {
  final normalized = value.trim();
  if (!RegExp(r'^[1-9][0-9]*$').hasMatch(normalized)) {
    return null;
  }
  return normalized;
}
