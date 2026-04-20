import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

enum ProfileStatus {
  idle,
  loading,
  ready,
  error,
}

class ProfileState {
  const ProfileState({
    required this.status,
    this.userId,
    this.profile,
    this.errorMessage,
  });

  const ProfileState.idle()
      : this(
          status: ProfileStatus.idle,
        );

  final ProfileStatus status;
  final String? userId;
  final PublicProfileSummary? profile;
  final String? errorMessage;

  bool get isIdle => status == ProfileStatus.idle;

  bool get isLoading => status == ProfileStatus.loading;

  bool get isReady => status == ProfileStatus.ready;

  bool get isError => status == ProfileStatus.error;

  ProfileState copyWith({
    ProfileStatus? status,
    String? userId,
    bool clearUserId = false,
    PublicProfileSummary? profile,
    bool clearProfile = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ProfileState(
      status: status ?? this.status,
      userId: clearUserId ? null : (userId ?? this.userId),
      profile: clearProfile ? null : (profile ?? this.profile),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class ProfileController extends ChangeNotifier {
  ProfileController({
    required ProfileRepository repository,
  }) : _repository = repository;

  final ProfileRepository _repository;
  ProfileState _state = const ProfileState.idle();
  int _requestVersion = 0;

  ProfileState get state => _state;

  Future<void> loadForUser(String? userId) async {
    final normalizedUserId = userId?.trim();
    if (normalizedUserId == null || normalizedUserId.isEmpty) {
      _requestVersion++;
      _state = const ProfileState.idle();
      notifyListeners();
      return;
    }

    if (_state.userId == normalizedUserId &&
        (_state.isLoading || _state.isReady)) {
      return;
    }

    await _load(normalizedUserId);
  }

  Future<void> refresh() async {
    final userId = _state.userId;
    if (userId == null || userId.isEmpty) {
      return;
    }

    await _load(userId);
  }

  Future<void> _load(String userId) async {
    final requestVersion = ++_requestVersion;
    _state = _state.copyWith(
      status: ProfileStatus.loading,
      userId: userId,
      clearError: true,
    );
    notifyListeners();

    try {
      final profile = await _repository.getPublicProfile(userId: userId);

      if (requestVersion != _requestVersion) {
        return;
      }

      _state = _state.copyWith(
        status: ProfileStatus.ready,
        userId: userId,
        profile: profile,
        clearError: true,
      );
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _setError(requestVersion, userId, error.message);
    } on FormatException catch (error) {
      _setError(
        requestVersion,
        userId,
        'Unexpected profile payload: ${error.message}',
      );
    }
  }

  void _setError(int requestVersion, String userId, String message) {
    if (requestVersion != _requestVersion) {
      return;
    }

    _state = _state.copyWith(
      status: ProfileStatus.error,
      userId: userId,
      errorMessage: message,
    );
    notifyListeners();
  }
}
