import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/experience_models.dart';
import '../data/experience_repository.dart';
import 'experience_issue.dart';

enum ExperienceSummaryStatus { idle, loading, ready, unavailable, stale }

class ExperienceSummaryState {
  const ExperienceSummaryState({
    required this.status,
    this.accountId,
    this.experience,
    this.isRefreshing = false,
    this.issue,
  });

  const ExperienceSummaryState.idle()
      : this(status: ExperienceSummaryStatus.idle);

  final ExperienceSummaryStatus status;
  final String? accountId;
  final UserExperience? experience;
  final bool isRefreshing;
  final ExperienceIssue? issue;

  bool get isIdle => status == ExperienceSummaryStatus.idle;
  bool get isLoading => status == ExperienceSummaryStatus.loading;
  bool get isReady => status == ExperienceSummaryStatus.ready;
  bool get isUnavailable => status == ExperienceSummaryStatus.unavailable;
  bool get isStale => status == ExperienceSummaryStatus.stale;
  bool get hasExperience => experience != null;
  bool get isBusy => isLoading || isRefreshing;
}

class ExperienceSummaryController extends ChangeNotifier {
  ExperienceSummaryController({required ExperienceRepository repository})
      : _repository = repository;

  final ExperienceRepository _repository;
  ExperienceSummaryState _state = const ExperienceSummaryState.idle();
  String? _accessToken;
  String? _accountId;
  int _generation = 0;
  bool _isDisposed = false;

  ExperienceSummaryState get state => _state;

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
      return _load(preserveCurrent: _state.hasExperience);
    }
    return Future<void>.value();
  }

  Future<void> refresh() {
    if (_state.isBusy || _accountId == null || _accessToken == null) {
      return Future<void>.value();
    }
    return _load(preserveCurrent: _state.hasExperience);
  }

  Future<void> _load({required bool preserveCurrent}) async {
    final accessToken = _accessToken;
    final accountId = _accountId;
    if (accessToken == null || accountId == null) {
      return;
    }

    final generation = ++_generation;
    final currentExperience = preserveCurrent ? _state.experience : null;
    _state = ExperienceSummaryState(
      status: preserveCurrent
          ? ExperienceSummaryStatus.ready
          : ExperienceSummaryStatus.loading,
      accountId: accountId,
      experience: currentExperience,
      isRefreshing: preserveCurrent,
    );
    _notify();

    try {
      final experience =
          await _repository.getMyExperience(accessToken: accessToken);
      if (!_canCommit(generation, accountId)) {
        return;
      }
      _state = ExperienceSummaryState(
        status: ExperienceSummaryStatus.ready,
        accountId: accountId,
        experience: experience,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        accountId,
        ExperienceIssue.fromApi(error),
        preserveCurrent: preserveCurrent,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        accountId,
        ExperienceIssue.invalidResponse(error, resourceLabel: '经验概要'),
        preserveCurrent: preserveCurrent,
      );
    }
  }

  void _commitIssue(
    int generation,
    String accountId,
    ExperienceIssue issue, {
    required bool preserveCurrent,
  }) {
    if (!_canCommit(generation, accountId)) {
      return;
    }
    _state = ExperienceSummaryState(
      status: preserveCurrent
          ? ExperienceSummaryStatus.stale
          : ExperienceSummaryStatus.unavailable,
      accountId: accountId,
      experience: preserveCurrent ? _state.experience : null,
      issue: issue,
    );
    _notify();
  }

  void _commitInvalidAccount() {
    _generation++;
    _accessToken = null;
    _accountId = null;
    _state = ExperienceSummaryState(
      status: ExperienceSummaryStatus.unavailable,
      issue: ExperienceIssue.request(
        '请先登录后查看经验记录。',
        code: 'Experience.MissingAccount',
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
