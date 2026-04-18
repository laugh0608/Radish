import 'package:flutter/foundation.dart';

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
  });

  const SessionState.restoring()
      : this._(
          status: SessionStatus.restoring,
        );

  const SessionState.anonymous()
      : this._(
          status: SessionStatus.anonymous,
        );

  const SessionState.authenticated(AuthSession session)
      : this._(
          status: SessionStatus.authenticated,
          session: session,
        );

  final SessionStatus status;
  final AuthSession? session;

  bool get isRestoring => status == SessionStatus.restoring;

  bool get isAnonymous => status == SessionStatus.anonymous;

  bool get isAuthenticated => status == SessionStatus.authenticated;
}

class SessionController extends ChangeNotifier {
  SessionController({
    required SessionStore sessionStore,
  }) : _sessionStore = sessionStore;

  final SessionStore _sessionStore;

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

    _state = SessionState.authenticated(session);
    notifyListeners();
  }
}
