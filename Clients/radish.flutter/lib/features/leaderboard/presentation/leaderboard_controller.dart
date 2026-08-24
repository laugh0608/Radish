import 'package:flutter/foundation.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/leaderboard_models.dart';
import '../data/leaderboard_repository.dart';
import 'leaderboard_issue.dart';

enum LeaderboardStatus { idle, loading, ready, empty, unavailable, stale }

class LeaderboardState {
  const LeaderboardState({
    required this.status,
    this.page,
    this.isRefreshing = false,
    this.issue,
  });

  const LeaderboardState.idle() : this(status: LeaderboardStatus.idle);

  final LeaderboardStatus status;
  final LeaderboardPageResult? page;
  final bool isRefreshing;
  final LeaderboardIssue? issue;

  bool get isIdle => status == LeaderboardStatus.idle;
  bool get isLoading => status == LeaderboardStatus.loading;
  bool get isReady => status == LeaderboardStatus.ready;
  bool get isUnavailable => status == LeaderboardStatus.unavailable;
  bool get isStale => status == LeaderboardStatus.stale;
  bool get hasSnapshot => page != null;
  bool get hasEntries => page?.items.isNotEmpty ?? false;
  bool get hasEmptySnapshot => page?.items.isEmpty ?? false;
  bool get isBusy => isLoading || isRefreshing;
}

class LeaderboardController extends ChangeNotifier {
  LeaderboardController({
    required LeaderboardRepository repository,
    this.pageSize = 20,
  }) : _repository = repository;

  final LeaderboardRepository _repository;
  final int pageSize;
  LeaderboardState _state = const LeaderboardState.idle();
  int _generation = 0;
  bool _isDisposed = false;

  LeaderboardState get state => _state;

  Future<void> open() => _load(preserveSnapshot: false);

  Future<void> refresh() {
    if (_state.isBusy) {
      return Future<void>.value();
    }
    return _load(preserveSnapshot: _state.hasSnapshot);
  }

  Future<void> _load({required bool preserveSnapshot}) async {
    final generation = ++_generation;
    final currentPage = preserveSnapshot ? _state.page : null;
    _state = LeaderboardState(
      status: preserveSnapshot
          ? _statusForSnapshot(currentPage!)
          : LeaderboardStatus.loading,
      page: currentPage,
      isRefreshing: preserveSnapshot,
    );
    _notify();

    try {
      final page = await _repository.getExperienceLeaderboard(
        pageIndex: 1,
        pageSize: pageSize,
      );
      if (!_canCommit(generation)) {
        return;
      }
      _state = LeaderboardState(
        status: _statusForSnapshot(page),
        page: page,
      );
      _notify();
    } on RadishApiClientException catch (error) {
      _commitIssue(
        generation,
        LeaderboardIssue.fromApi(error),
        currentPage: currentPage,
      );
    } on FormatException catch (error) {
      _commitIssue(
        generation,
        LeaderboardIssue.invalidResponse(error),
        currentPage: currentPage,
      );
    }
  }

  void _commitIssue(
    int generation,
    LeaderboardIssue issue, {
    required LeaderboardPageResult? currentPage,
  }) {
    if (!_canCommit(generation)) {
      return;
    }
    _state = LeaderboardState(
      status: currentPage == null
          ? LeaderboardStatus.unavailable
          : LeaderboardStatus.stale,
      page: currentPage,
      issue: issue,
    );
    _notify();
  }

  bool _canCommit(int generation) {
    return !_isDisposed && generation == _generation;
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

LeaderboardStatus _statusForSnapshot(LeaderboardPageResult page) {
  return page.isEmpty ? LeaderboardStatus.empty : LeaderboardStatus.ready;
}
