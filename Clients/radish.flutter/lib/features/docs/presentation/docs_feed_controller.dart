import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';

enum DocsFeedStatus {
  loading,
  ready,
  error,
}

class DocsFeedState {
  const DocsFeedState({
    required this.status,
    required this.pageIndex,
    required this.pageSize,
    this.page,
    this.errorMessage,
  });

  const DocsFeedState.initial()
      : this(
          status: DocsFeedStatus.loading,
          pageIndex: 1,
          pageSize: 20,
        );

  final DocsFeedStatus status;
  final int pageIndex;
  final int pageSize;
  final DocsDocumentPage? page;
  final String? errorMessage;

  bool get isLoading => status == DocsFeedStatus.loading;

  bool get isReady => status == DocsFeedStatus.ready;

  bool get isError => status == DocsFeedStatus.error;

  bool get hasPreviousPage => pageIndex > 1;

  bool get hasNextPage => page != null && pageIndex < page!.pageCount;

  DocsFeedState copyWith({
    DocsFeedStatus? status,
    int? pageIndex,
    int? pageSize,
    DocsDocumentPage? page,
    bool clearPage = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return DocsFeedState(
      status: status ?? this.status,
      pageIndex: pageIndex ?? this.pageIndex,
      pageSize: pageSize ?? this.pageSize,
      page: clearPage ? null : (page ?? this.page),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class DocsFeedController extends ChangeNotifier {
  DocsFeedController({
    required DocsRepository repository,
  }) : _repository = repository;

  final DocsRepository _repository;
  DocsFeedState _state = const DocsFeedState.initial();
  int _requestVersion = 0;

  DocsFeedState get state => _state;

  Future<void> loadInitial() async {
    await _load(pageIndex: _state.pageIndex);
  }

  Future<void> refresh() async {
    await _load(pageIndex: _state.pageIndex);
  }

  Future<void> goToPage(int pageIndex) async {
    if (pageIndex < 1) {
      return;
    }

    if (pageIndex == _state.pageIndex && _state.page != null) {
      return;
    }

    await _load(pageIndex: pageIndex);
  }

  Future<void> _load({
    required int pageIndex,
  }) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: DocsFeedStatus.loading,
      pageIndex: pageIndex,
      clearError: true,
    );
    notifyListeners();

    try {
      final page = await _repository.getDocumentPage(
        pageIndex: pageIndex,
        pageSize: _state.pageSize,
      );

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: DocsFeedStatus.ready,
        pageIndex: page.page,
        page: page,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, pageIndex, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        pageIndex,
        'Unexpected docs payload: ${error.message}',
      );
    }
  }

  void _setError(int requestVersion, int pageIndex, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: DocsFeedStatus.error,
      pageIndex: pageIndex,
      errorMessage: message,
    );
    notifyListeners();
  }
}
