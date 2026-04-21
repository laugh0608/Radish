import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';

enum DocsDetailStatus {
  idle,
  loading,
  ready,
  error,
}

class DocsDetailState {
  const DocsDetailState({
    required this.status,
    this.slug,
    this.detail,
    this.errorMessage,
  });

  const DocsDetailState.idle()
      : this(
          status: DocsDetailStatus.idle,
        );

  final DocsDetailStatus status;
  final String? slug;
  final DocsDocumentDetail? detail;
  final String? errorMessage;

  bool get isIdle => status == DocsDetailStatus.idle;

  bool get isLoading => status == DocsDetailStatus.loading;

  bool get isReady => status == DocsDetailStatus.ready;

  bool get isError => status == DocsDetailStatus.error;

  DocsDetailState copyWith({
    DocsDetailStatus? status,
    String? slug,
    bool clearSlug = false,
    DocsDocumentDetail? detail,
    bool clearDetail = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return DocsDetailState(
      status: status ?? this.status,
      slug: clearSlug ? null : (slug ?? this.slug),
      detail: clearDetail ? null : (detail ?? this.detail),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class DocsDetailController extends ChangeNotifier {
  DocsDetailController({
    required DocsRepository repository,
  }) : _repository = repository;

  final DocsRepository _repository;
  DocsDetailState _state = const DocsDetailState.idle();
  int _requestVersion = 0;

  DocsDetailState get state => _state;

  Future<void> openDocument(String slug) async {
    final normalizedSlug = slug.trim();
    if (normalizedSlug.isEmpty) {
      return;
    }

    if (_state.slug == normalizedSlug && (_state.isLoading || _state.isReady)) {
      return;
    }

    await _load(normalizedSlug);
  }

  Future<void> refresh() async {
    final slug = _state.slug;
    if (slug == null || slug.isEmpty) {
      return;
    }

    await _load(slug);
  }

  void close() {
    _requestVersion++;
    _state = const DocsDetailState.idle();
    notifyListeners();
  }

  Future<void> _load(String slug) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: DocsDetailStatus.loading,
      slug: slug,
      clearError: true,
    );
    notifyListeners();

    try {
      final detail = await _repository.getDocumentDetail(slug: slug);

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: DocsDetailStatus.ready,
        slug: slug,
        detail: detail,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, slug, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        slug,
        'Unexpected docs detail payload: ${error.message}',
      );
    }
  }

  void _setError(int requestVersion, String slug, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DocsDetailStatus.error,
      slug: slug,
      errorMessage: message,
    );
    notifyListeners();
  }
}
