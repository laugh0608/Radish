import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/experience_models.dart';
import '../data/experience_repository.dart';
import 'experience_issue.dart';

enum ExperienceTransactionStatus { idle, loading, ready, unavailable }

class ExperienceTransactionState {
  const ExperienceTransactionState({
    required this.status,
    this.accountId,
    this.transactions = const <ExperienceTransaction>[],
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

  const ExperienceTransactionState.idle({int pageSize = 20})
      : this(status: ExperienceTransactionStatus.idle, pageSize: pageSize);

  final ExperienceTransactionStatus status;
  final String? accountId;
  final List<ExperienceTransaction> transactions;
  final int pageIndex;
  final int pageSize;
  final int pageCount;
  final int dataCount;
  final bool isRefreshing;
  final bool isAppending;
  final ExperienceIssue? issue;
  final ExperienceIssue? refreshIssue;
  final ExperienceIssue? appendIssue;

  bool get isIdle => status == ExperienceTransactionStatus.idle;
  bool get isLoading => status == ExperienceTransactionStatus.loading;
  bool get isReady => status == ExperienceTransactionStatus.ready;
  bool get isUnavailable => status == ExperienceTransactionStatus.unavailable;
  bool get isEmpty => isReady && transactions.isEmpty;
  bool get isStale => isReady && refreshIssue != null;
  bool get isBusy => isLoading || isRefreshing || isAppending;
  bool get hasMore => pageIndex < pageCount;
}

enum _ExperienceTransactionLoadMode { initial, refresh, append }

class ExperienceTransactionController extends ChangeNotifier {
  ExperienceTransactionController({
    required ExperienceRepository repository,
    int pageSize = 20,
  })  : _repository = repository,
        _state = ExperienceTransactionState.idle(pageSize: pageSize);

  final ExperienceRepository _repository;
  ExperienceTransactionState _state;
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  ExperienceTransactionState get state => _state;

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
      return _load(pageIndex: 1, mode: _ExperienceTransactionLoadMode.initial);
    }
    if (credentialChanged) {
      return _load(pageIndex: 1, mode: _ExperienceTransactionLoadMode.refresh);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isBusy || _accountId == null || _accessToken == null) {
      return Future<void>.value();
    }
    return _load(
      pageIndex: 1,
      mode: _state.isUnavailable
          ? _ExperienceTransactionLoadMode.initial
          : _ExperienceTransactionLoadMode.refresh,
    );
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
      mode: _ExperienceTransactionLoadMode.append,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required _ExperienceTransactionLoadMode mode,
  }) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    _state = switch (mode) {
      _ExperienceTransactionLoadMode.initial => ExperienceTransactionState(
          status: ExperienceTransactionStatus.loading,
          accountId: accountId,
          pageSize: _state.pageSize,
        ),
      _ExperienceTransactionLoadMode.refresh => ExperienceTransactionState(
          status: ExperienceTransactionStatus.ready,
          accountId: accountId,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isRefreshing: true,
        ),
      _ExperienceTransactionLoadMode.append => ExperienceTransactionState(
          status: ExperienceTransactionStatus.ready,
          accountId: accountId,
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
      );
      if (!_canCommit(generation, accountId)) {
        return;
      }
      final transactions = mode == _ExperienceTransactionLoadMode.append
          ? _mergeTransactions(_state.transactions, page.transactions)
          : _mergeTransactions(
              const <ExperienceTransaction>[],
              page.transactions,
            );
      _state = ExperienceTransactionState(
        status: ExperienceTransactionStatus.ready,
        accountId: accountId,
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
        mode,
        ExperienceIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        mode,
        ExperienceIssue.invalidResponse(error, resourceLabel: '经验流水'),
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    _ExperienceTransactionLoadMode mode,
    ExperienceIssue issue,
  ) {
    if (!_canCommit(generation, accountId)) {
      return;
    }
    _state = switch (mode) {
      _ExperienceTransactionLoadMode.initial => ExperienceTransactionState(
          status: ExperienceTransactionStatus.unavailable,
          accountId: accountId,
          pageSize: _state.pageSize,
          issue: issue,
        ),
      _ExperienceTransactionLoadMode.refresh => ExperienceTransactionState(
          status: ExperienceTransactionStatus.ready,
          accountId: accountId,
          transactions: _state.transactions,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          refreshIssue: issue,
        ),
      _ExperienceTransactionLoadMode.append => ExperienceTransactionState(
          status: ExperienceTransactionStatus.ready,
          accountId: accountId,
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

  void _commitInvalidAccount() {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = ExperienceTransactionState(
      status: ExperienceTransactionStatus.unavailable,
      pageSize: _state.pageSize,
      issue: ExperienceIssue.request(
        '请先登录后查看经验流水。',
        code: 'Experience.MissingAccount',
      ),
    );
    _notify();
  }

  List<ExperienceTransaction> _mergeTransactions(
    List<ExperienceTransaction> current,
    List<ExperienceTransaction> incoming,
  ) {
    final merged = <ExperienceTransaction>[];
    final seen = <String>{};
    for (final transaction in <ExperienceTransaction>[
      ...current,
      ...incoming,
    ]) {
      final id = _normalize(transaction.id);
      final key = id ??
          'fallback:${transaction.userId}:${transaction.createTime}:${transaction.expBefore}:${transaction.expAfter}:${transaction.businessId}';
      if (seen.add(key)) {
        merged.add(transaction);
      }
    }
    return List<ExperienceTransaction>.unmodifiable(merged);
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
