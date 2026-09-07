import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'browse_history_issue.dart';

enum BrowseHistoryStatus { idle, loading, ready, unavailable }

class BrowseHistoryState {
  const BrowseHistoryState({
    required this.status,
    this.accountId,
    this.items = const <UserBrowseHistoryItem>[],
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

  const BrowseHistoryState.idle({int pageSize = 20})
      : this(status: BrowseHistoryStatus.idle, pageSize: pageSize);

  final BrowseHistoryStatus status;
  final String? accountId;
  final List<UserBrowseHistoryItem> items;
  final int pageIndex;
  final int pageSize;
  final int pageCount;
  final int dataCount;
  final bool isRefreshing;
  final bool isAppending;
  final BrowseHistoryIssue? issue;
  final BrowseHistoryIssue? refreshIssue;
  final BrowseHistoryIssue? appendIssue;

  bool get isIdle => status == BrowseHistoryStatus.idle;
  bool get isLoading => status == BrowseHistoryStatus.loading;
  bool get isReady => status == BrowseHistoryStatus.ready;
  bool get isUnavailable => status == BrowseHistoryStatus.unavailable;
  bool get isEmpty => isReady && items.isEmpty;
  bool get isStale => isReady && refreshIssue != null;
  bool get isBusy => isLoading || isRefreshing || isAppending;
  bool get hasMore => isReady && pageIndex < pageCount;
}

enum _BrowseHistoryLoadMode { initial, refresh, append }

class BrowseHistoryController extends ChangeNotifier {
  BrowseHistoryController({
    required ProfileRepository repository,
    int pageSize = 20,
  })  : _repository = repository,
        _state = BrowseHistoryState.idle(pageSize: pageSize);

  final ProfileRepository _repository;
  BrowseHistoryState _state;
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  BrowseHistoryState get state => _state;

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

    if (accountChanged || !_state.isReady) {
      return _load(pageIndex: 1, mode: _BrowseHistoryLoadMode.initial);
    }
    if (credentialChanged) {
      return _load(pageIndex: 1, mode: _BrowseHistoryLoadMode.refresh);
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
          ? _BrowseHistoryLoadMode.initial
          : _BrowseHistoryLoadMode.refresh,
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
      mode: _BrowseHistoryLoadMode.append,
    );
  }

  Future<void> _load({
    required int pageIndex,
    required _BrowseHistoryLoadMode mode,
  }) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    _state = switch (mode) {
      _BrowseHistoryLoadMode.initial => BrowseHistoryState(
          status: BrowseHistoryStatus.loading,
          accountId: accountId,
          pageSize: _state.pageSize,
        ),
      _BrowseHistoryLoadMode.refresh => BrowseHistoryState(
          status: BrowseHistoryStatus.ready,
          accountId: accountId,
          items: _state.items,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isRefreshing: true,
        ),
      _BrowseHistoryLoadMode.append => BrowseHistoryState(
          status: BrowseHistoryStatus.ready,
          accountId: accountId,
          items: _state.items,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          isAppending: true,
        ),
    };
    _notify();

    try {
      final page = await _repository.getMyBrowseHistory(
        accessToken: accessToken,
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );
      if (!_canCommit(generation, accountId, accessToken)) {
        return;
      }
      final items = mode == _BrowseHistoryLoadMode.append
          ? _mergeItems(_state.items, page.items)
          : _mergeItems(const <UserBrowseHistoryItem>[], page.items);
      _state = BrowseHistoryState(
        status: BrowseHistoryStatus.ready,
        accountId: accountId,
        items: items,
        pageIndex: page.page,
        pageSize: _state.pageSize,
        pageCount: page.pageCount,
        dataCount: page.total,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        accessToken,
        mode,
        BrowseHistoryIssue.fromApi(error),
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        accessToken,
        mode,
        BrowseHistoryIssue.invalidResponse(error),
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    String accessToken,
    _BrowseHistoryLoadMode mode,
    BrowseHistoryIssue issue,
  ) {
    if (!_canCommit(generation, accountId, accessToken)) {
      return;
    }
    _state = switch (mode) {
      _BrowseHistoryLoadMode.initial => BrowseHistoryState(
          status: BrowseHistoryStatus.unavailable,
          accountId: accountId,
          pageSize: _state.pageSize,
          issue: issue,
        ),
      _BrowseHistoryLoadMode.refresh => BrowseHistoryState(
          status: BrowseHistoryStatus.ready,
          accountId: accountId,
          items: _state.items,
          pageIndex: _state.pageIndex,
          pageSize: _state.pageSize,
          pageCount: _state.pageCount,
          dataCount: _state.dataCount,
          refreshIssue: issue,
        ),
      _BrowseHistoryLoadMode.append => BrowseHistoryState(
          status: BrowseHistoryStatus.ready,
          accountId: accountId,
          items: _state.items,
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
    _state = BrowseHistoryState(
      status: BrowseHistoryStatus.unavailable,
      pageSize: _state.pageSize,
      issue: BrowseHistoryIssue.request(
        '请先登录后查看账号浏览历史。',
        code: 'BrowseHistory.MissingAccount',
      ),
    );
    _notify();
  }

  List<UserBrowseHistoryItem> _mergeItems(
    List<UserBrowseHistoryItem> current,
    List<UserBrowseHistoryItem> incoming,
  ) {
    final merged = <UserBrowseHistoryItem>[];
    final seenIds = <String>{};
    for (final item in <UserBrowseHistoryItem>[...current, ...incoming]) {
      if (seenIds.add(item.id.trim())) {
        merged.add(item);
      }
    }
    return List<UserBrowseHistoryItem>.unmodifiable(merged);
  }

  bool _canCommit(
    int generation,
    String accountId,
    String accessToken,
  ) {
    return !_isDisposed &&
        generation == _generation &&
        accountId == _accountId &&
        accessToken == _accessToken;
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
