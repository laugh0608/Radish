import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_issue.dart';

enum DocsDetailStatus { idle, loading, ready, unavailable, stale }

class DocsDetailState {
  const DocsDetailState({
    required this.status,
    this.target,
    this.detail,
    this.isRefreshing = false,
    this.issue,
  });

  const DocsDetailState.idle() : this(status: DocsDetailStatus.idle);

  final DocsDetailStatus status;
  final DocsDetailHandoffTarget? target;
  final DocsDocumentDetail? detail;
  final bool isRefreshing;
  final DocsIssue? issue;

  String? get slug => target?.normalizedSlug;
  String? get errorMessage => issue?.message;
  bool get isIdle => status == DocsDetailStatus.idle;
  bool get isLoading => status == DocsDetailStatus.loading;
  bool get isReady => status == DocsDetailStatus.ready;
  bool get isUnavailable => status == DocsDetailStatus.unavailable;
  bool get isStale => status == DocsDetailStatus.stale;
  bool get isError => isUnavailable;
  bool get hasDetail => detail != null;
}

class DocsDetailController extends ChangeNotifier {
  DocsDetailController({required DocsRepository repository})
      : _repository = repository;

  final DocsRepository _repository;
  DocsDetailState _state = const DocsDetailState.idle();
  int _requestVersion = 0;
  bool _isDisposed = false;

  DocsDetailState get state => _state;

  Future<void> openDocument(
    String slug, {
    DocsDetailHandoffSource source = DocsDetailHandoffSource.docsList,
    String? initialTitle,
  }) {
    return openTarget(
      DocsDetailHandoffTarget(
        slug: slug,
        source: source,
        initialTitle: initialTitle,
      ),
    );
  }

  Future<void> openTarget(DocsDetailHandoffTarget target) {
    final normalizedTarget = DocsDetailHandoffTarget(
      slug: target.normalizedSlug,
      source: target.source,
      initialTitle: target.normalizedInitialTitle,
    );
    if (!normalizedTarget.hasValidSlug) {
      return Future<void>.value();
    }
    if (_state.slug == normalizedTarget.normalizedSlug &&
        !_state.isUnavailable) {
      return Future<void>.value();
    }
    return _load(normalizedTarget, preserveCurrentDetail: false);
  }

  Future<void> refresh() {
    final target = _state.target;
    if (target == null || !target.hasValidSlug) {
      return Future<void>.value();
    }
    return _load(target, preserveCurrentDetail: _state.detail != null);
  }

  void close() {
    _requestVersion++;
    _state = const DocsDetailState.idle();
    _notify();
  }

  Future<void> _load(
    DocsDetailHandoffTarget target, {
    required bool preserveCurrentDetail,
  }) async {
    final requestVersion = ++_requestVersion;
    _state = DocsDetailState(
      status: preserveCurrentDetail
          ? DocsDetailStatus.ready
          : DocsDetailStatus.loading,
      target: target,
      detail: preserveCurrentDetail ? _state.detail : null,
      isRefreshing: preserveCurrentDetail,
    );
    _notify();

    try {
      final detail = await _repository.getDocumentDetail(
        slug: target.normalizedSlug,
      );
      if (!_canCommit(requestVersion, target)) {
        return;
      }
      _state = DocsDetailState(
        status: DocsDetailStatus.ready,
        target: target,
        detail: detail,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        requestVersion,
        target,
        DocsIssue.fromApi(error),
        preserveCurrentDetail: preserveCurrentDetail,
      );
    } on FormatException catch (error) {
      _commitIssue(
        requestVersion,
        target,
        DocsIssue.invalidResponse(error, resourceLabel: '文档详情'),
        preserveCurrentDetail: preserveCurrentDetail,
      );
    }
  }

  void _commitIssue(
    int requestVersion,
    DocsDetailHandoffTarget target,
    DocsIssue issue, {
    required bool preserveCurrentDetail,
  }) {
    if (!_canCommit(requestVersion, target)) {
      return;
    }
    _state = DocsDetailState(
      status: preserveCurrentDetail
          ? DocsDetailStatus.stale
          : DocsDetailStatus.unavailable,
      target: target,
      detail: preserveCurrentDetail ? _state.detail : null,
      issue: issue,
    );
    _notify();
  }

  bool _canCommit(int requestVersion, DocsDetailHandoffTarget target) {
    return !_isDisposed &&
        requestVersion == _requestVersion &&
        _state.slug == target.normalizedSlug;
  }

  void _notify() {
    if (!_isDisposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    _requestVersion++;
    super.dispose();
  }
}
