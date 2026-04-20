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
  })  : _sessionStore = sessionStore,
        _refreshService = refreshService;

  final SessionStore _sessionStore;
  final SessionRefreshService _refreshService;

  SessionState _state = const SessionState.restoring();
  Future<void>? _restoreFuture;

  SessionState get state => _state;

  Future<void> restore() {
    return _restoreFuture ??= _restore();
  }

  Future<void> setSession(AuthSession session) async {
    await _sessionStore.write(session);
    _state = SessionState.authenticated(session);
    notifyListeners();
  }

  Future<void> clearSession() async {
    await _sessionStore.clear();
    _state = const SessionState.anonymous();
    notifyListeners();
  }

  Future<void> _restore() async {
    final session = await _sessionStore.read();

    if (session == null) {
      _state = const SessionState.anonymous();
      notifyListeners();
      return;
    }

    if (!session.isExpired) {
      _state = SessionState.authenticated(session);
      notifyListeners();
      return;
    }

    try {
      final refreshedSession = await _refreshService.refresh(session);
      await _sessionStore.write(refreshedSession);
      _state = SessionState.authenticated(refreshedSession);
      notifyListeners();
    } on SessionRefreshException catch (error) {
      await _sessionStore.clear();
      _state = SessionState.anonymous(lastErrorMessage: error.message);
      notifyListeners();
    }
  }
}
