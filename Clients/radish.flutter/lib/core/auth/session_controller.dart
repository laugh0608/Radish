import 'package:flutter/foundation.dart';

import 'session_refresh_service.dart';
import 'session_store.dart';

enum SessionStatus {
  restoring,
  anonymous,
  authenticated,
}

class SessionState {
  const SessionState._({
    required this.status,
    this.session,
    this.lastErrorMessage,
  });

  const SessionState.restoring()
      : this._(
          status: SessionStatus.restoring,
        );

  const SessionState.anonymous({
    String? lastErrorMessage,
  }) : this._(
          status: SessionStatus.anonymous,
          lastErrorMessage: lastErrorMessage,
        );

  const SessionState.authenticated(AuthSession session)
      : this._(
          status: SessionStatus.authenticated,
          session: session,
        );

  const SessionState.authenticatedWithIssue(
    AuthSession session, {
    required String lastErrorMessage,
  }) : this._(
          status: SessionStatus.authenticated,
          session: session,
          lastErrorMessage: lastErrorMessage,
        );

  final SessionStatus status;
  final AuthSession? session;
  final String? lastErrorMessage;

  bool get isRestoring => status == SessionStatus.restoring;

  bool get isAnonymous => status == SessionStatus.anonymous;

  bool get isAuthenticated => status == SessionStatus.authenticated;
}

class SessionController extends ChangeNotifier {
  SessionController({
    required SessionStore sessionStore,
    required SessionRefreshService refreshService,
    DateTime Function()? utcNow,
    this.refreshBeforeExpiry = const Duration(seconds: 30),
  })  : _sessionStore = sessionStore,
        _refreshService = refreshService,
        _utcNow = utcNow ?? _systemUtcNow;

  final SessionStore _sessionStore;
  final SessionRefreshService _refreshService;
  final DateTime Function() _utcNow;
  final Duration refreshBeforeExpiry;

  SessionState _state = const SessionState.restoring();
  Future<void>? _restoreFuture;
  Future<AuthSession>? _refreshFuture;
  String? _refreshSourceAccessToken;
  int _sessionEpoch = 0;

  SessionState get state => _state;

  Future<void> restore() {
    return _restoreFuture ??= _restore();
  }

  Future<void> setSession(AuthSession session) async {
    _sessionEpoch += 1;
    await _sessionStore.write(session);
    _state = SessionState.authenticated(session);
    notifyListeners();
  }

  Future<void> clearSession() async {
    _sessionEpoch += 1;
    await _sessionStore.clear();
    _state = const SessionState.anonymous();
    notifyListeners();
  }

  Future<String?> resolveAccessToken({
    String? rejectedAccessToken,
    bool forceRefresh = false,
  }) async {
    final session = _state.session;
    if (session == null || !_state.isAuthenticated) {
      return null;
    }

    final normalizedRejectedToken = rejectedAccessToken?.trim();
    if (normalizedRejectedToken != null &&
        normalizedRejectedToken.isNotEmpty &&
        normalizedRejectedToken != session.accessToken) {
      return session.accessToken;
    }

    if (!forceRefresh && !_expiresSoon(session)) {
      return session.accessToken;
    }

    try {
      final refreshedSession = await _refreshSession(session);
      return refreshedSession.accessToken;
    } on SessionRefreshException catch (error) {
      await _applyRefreshFailure(error, sourceSession: session);
      rethrow;
    }
  }

  Future<void> _restore() async {
    final session = await _sessionStore.read();

    if (session == null) {
      _state = const SessionState.anonymous();
      notifyListeners();
      return;
    }

    _state = SessionState.authenticated(session);
    if (!_expiresSoon(session)) {
      notifyListeners();
      return;
    }

    try {
      await _refreshSession(session);
    } on SessionRefreshException catch (error) {
      await _applyRefreshFailure(error, sourceSession: session);
    }
  }

  bool _expiresSoon(AuthSession session) {
    final refreshAt = _utcNow().toUtc().add(refreshBeforeExpiry);
    return !session.expiresAt.toUtc().isAfter(refreshAt);
  }

  Future<AuthSession> _refreshSession(AuthSession sourceSession) {
    final currentRefresh = _refreshFuture;
    if (currentRefresh != null &&
        _refreshSourceAccessToken == sourceSession.accessToken) {
      return currentRefresh;
    }

    late final Future<AuthSession> refreshFuture;
    refreshFuture = _performRefresh(sourceSession).whenComplete(() {
      if (identical(_refreshFuture, refreshFuture)) {
        _refreshFuture = null;
        _refreshSourceAccessToken = null;
      }
    });
    _refreshSourceAccessToken = sourceSession.accessToken;
    _refreshFuture = refreshFuture;
    return refreshFuture;
  }

  Future<AuthSession> _performRefresh(AuthSession sourceSession) async {
    final sourceEpoch = _sessionEpoch;
    final refreshedSession = await _refreshService.refresh(sourceSession);
    final currentSession = _state.session;

    if (sourceEpoch != _sessionEpoch ||
        currentSession == null ||
        currentSession.accessToken != sourceSession.accessToken) {
      if (currentSession != null && _state.isAuthenticated) {
        return currentSession;
      }

      throw const SessionRefreshException('登录会话已变更。');
    }

    await _sessionStore.write(refreshedSession);
    _sessionEpoch += 1;
    _state = SessionState.authenticated(refreshedSession);
    notifyListeners();
    return refreshedSession;
  }

  Future<void> _applyRefreshFailure(
    SessionRefreshException error, {
    required AuthSession sourceSession,
  }) async {
    final currentSession = _state.session;
    if (currentSession == null ||
        currentSession.accessToken != sourceSession.accessToken) {
      return;
    }

    if (error.invalidatesSession) {
      _sessionEpoch += 1;
      await _sessionStore.clear();
      _state = SessionState.anonymous(lastErrorMessage: error.message);
    } else {
      _state = SessionState.authenticatedWithIssue(
        currentSession,
        lastErrorMessage: error.message,
      );
    }
    notifyListeners();
  }

  static DateTime _systemUtcNow() => DateTime.now().toUtc();
}
