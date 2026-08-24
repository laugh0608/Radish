import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import 'shop_issue.dart';

enum ShopPrivateCollectionStatus { idle, loading, ready, unavailable, stale }

class ShopPrivateCollectionState<T> {
  const ShopPrivateCollectionState({
    required this.status,
    this.accountId,
    this.items = const [],
    this.isRefreshing = false,
    this.issue,
  });

  const ShopPrivateCollectionState.idle()
      : this(status: ShopPrivateCollectionStatus.idle);

  final ShopPrivateCollectionStatus status;
  final String? accountId;
  final List<T> items;
  final bool isRefreshing;
  final ShopIssue? issue;

  bool get isIdle => status == ShopPrivateCollectionStatus.idle;
  bool get isLoading => status == ShopPrivateCollectionStatus.loading;
  bool get isReady => status == ShopPrivateCollectionStatus.ready;
  bool get isUnavailable => status == ShopPrivateCollectionStatus.unavailable;
  bool get isStale => status == ShopPrivateCollectionStatus.stale;
  bool get isEmpty => isReady && items.isEmpty;
  bool get hasItems => items.isNotEmpty;
}

class ShopPrivateCollectionController<T> extends ChangeNotifier {
  ShopPrivateCollectionController({
    required Future<List<T>> Function(String accessToken) loader,
    required String resourceLabel,
  })  : _loader = loader,
        _resourceLabel = resourceLabel;

  final Future<List<T>> Function(String accessToken) _loader;
  final String _resourceLabel;
  ShopPrivateCollectionState<T> _state =
      const ShopPrivateCollectionState.idle();
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  ShopPrivateCollectionState<T> get state => _state;

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
      return _load(preserveCurrentItems: false);
    }
    if (credentialChanged) {
      return _load(preserveCurrentItems: true);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isLoading ||
        _state.isRefreshing ||
        _accountId == null ||
        _accessToken == null) {
      return Future<void>.value();
    }
    return _load(preserveCurrentItems: true);
  }

  Future<void> _load({required bool preserveCurrentItems}) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    final currentItems =
        preserveCurrentItems ? _state.items : List<T>.empty(growable: false);
    _state = ShopPrivateCollectionState<T>(
      status: preserveCurrentItems
          ? ShopPrivateCollectionStatus.ready
          : ShopPrivateCollectionStatus.loading,
      accountId: accountId,
      items: currentItems,
      isRefreshing: preserveCurrentItems,
    );
    _notify();

    try {
      final items = await _loader(accessToken);
      if (!_canCommit(generation, accountId)) {
        return;
      }
      _state = ShopPrivateCollectionState<T>(
        status: ShopPrivateCollectionStatus.ready,
        accountId: accountId,
        items: List<T>.unmodifiable(items),
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        ShopIssue.fromApi(error),
        preserveCurrentItems: preserveCurrentItems,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        ShopIssue.invalidResponse(error, resourceLabel: _resourceLabel),
        preserveCurrentItems: preserveCurrentItems,
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    ShopIssue issue, {
    required bool preserveCurrentItems,
  }) {
    if (!_canCommit(generation, accountId)) {
      return;
    }
    final currentItems =
        preserveCurrentItems ? _state.items : List<T>.empty(growable: false);
    _state = ShopPrivateCollectionState<T>(
      status: preserveCurrentItems
          ? ShopPrivateCollectionStatus.stale
          : ShopPrivateCollectionStatus.unavailable,
      accountId: accountId,
      items: currentItems,
      issue: issue,
    );
    _notify();
  }

  void _commitInvalidAccount() {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = ShopPrivateCollectionState<T>(
      status: ShopPrivateCollectionStatus.unavailable,
      issue: const ShopIssue(
        kind: ShopIssueKind.request,
        message: '请先登录后查看背包。',
        code: 'Shop.MissingAccount',
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
