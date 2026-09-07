import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';

void main() {
  test('refreshes a session before its access token expires', () async {
    var now = DateTime.utc(2026, 8, 29, 7);
    final initialSession = _session(
      accessToken: 'access-old',
      refreshToken: 'refresh-old',
      expiresAt: now.add(const Duration(minutes: 2)),
    );
    final refreshedSession = _session(
      accessToken: 'access-new',
      refreshToken: 'refresh-new',
      expiresAt: now.add(const Duration(minutes: 10)),
    );
    final store = InMemorySessionStore(initialSession: initialSession);
    final refreshService = _TestSessionRefreshService.success(refreshedSession);
    final controller = SessionController(
      sessionStore: store,
      refreshService: refreshService,
      utcNow: () => now,
    );

    await controller.restore();
    expect(refreshService.callCount, 0);

    now = now.add(const Duration(minutes: 1, seconds: 45));
    final accessToken = await controller.resolveAccessToken();

    expect(accessToken, 'access-new');
    expect(refreshService.callCount, 1);
    expect(controller.state.session?.accessToken, 'access-new');
    expect((await store.read())?.refreshToken, 'refresh-new');
  });

  test('shares one in-flight refresh between concurrent requests', () async {
    var now = DateTime.utc(2026, 8, 29, 7);
    final initialSession = _session(
      accessToken: 'access-old',
      refreshToken: 'refresh-old',
      expiresAt: now.add(const Duration(minutes: 2)),
    );
    final refreshCompleter = Completer<AuthSession>();
    final refreshService = _TestSessionRefreshService.pending(refreshCompleter);
    final controller = SessionController(
      sessionStore: InMemorySessionStore(initialSession: initialSession),
      refreshService: refreshService,
      utcNow: () => now,
    );
    await controller.restore();
    now = now.add(const Duration(minutes: 2));

    final first = controller.resolveAccessToken();
    final second = controller.resolveAccessToken();
    expect(refreshService.callCount, 1);

    refreshCompleter.complete(
      _session(
        accessToken: 'access-new',
        refreshToken: 'refresh-new',
        expiresAt: now.add(const Duration(minutes: 10)),
      ),
    );

    expect(await first, 'access-new');
    expect(await second, 'access-new');
    expect(refreshService.callCount, 1);
  });

  test('keeps a recoverable session after a transient refresh failure',
      () async {
    final now = DateTime.utc(2026, 8, 29, 7);
    final initialSession = _session(
      accessToken: 'access-old',
      refreshToken: 'refresh-old',
      expiresAt: now.subtract(const Duration(minutes: 1)),
    );
    final store = InMemorySessionStore(initialSession: initialSession);
    final controller = SessionController(
      sessionStore: store,
      refreshService: _TestSessionRefreshService.failure(
        const SessionRefreshException('网络暂时不可用'),
      ),
      utcNow: () => now,
    );

    await controller.restore();

    expect(controller.state.isAuthenticated, isTrue);
    expect(controller.state.session, same(initialSession));
    expect(controller.state.lastErrorMessage, '网络暂时不可用');
    expect(await store.read(), same(initialSession));
  });

  test('clears a session when the refresh token is invalid', () async {
    final now = DateTime.utc(2026, 8, 29, 7);
    final initialSession = _session(
      accessToken: 'access-old',
      refreshToken: 'refresh-old',
      expiresAt: now.subtract(const Duration(minutes: 1)),
    );
    final store = InMemorySessionStore(initialSession: initialSession);
    final controller = SessionController(
      sessionStore: store,
      refreshService: _TestSessionRefreshService.failure(
        const SessionRefreshException(
          'invalid_grant: refresh token expired',
          invalidatesSession: true,
        ),
      ),
      utcNow: () => now,
    );

    await controller.restore();

    expect(controller.state.isAnonymous, isTrue);
    expect(controller.state.lastErrorMessage, contains('invalid_grant'));
    expect(await store.read(), isNull);
  });

  test('reuses a token refreshed by another request', () async {
    final now = DateTime.utc(2026, 8, 29, 7);
    final currentSession = _session(
      accessToken: 'access-new',
      refreshToken: 'refresh-new',
      expiresAt: now.add(const Duration(minutes: 10)),
    );
    final refreshService = _TestSessionRefreshService.failure(
      const SessionRefreshException('must not refresh'),
    );
    final controller = SessionController(
      sessionStore: InMemorySessionStore(initialSession: currentSession),
      refreshService: refreshService,
      utcNow: () => now,
    );
    await controller.restore();

    final accessToken = await controller.resolveAccessToken(
      rejectedAccessToken: 'access-old',
      forceRefresh: true,
    );

    expect(accessToken, 'access-new');
    expect(refreshService.callCount, 0);
  });
}

AuthSession _session({
  required String accessToken,
  required String refreshToken,
  required DateTime expiresAt,
}) {
  return AuthSession(
    accessToken: accessToken,
    refreshToken: refreshToken,
    userId: 'user-42',
    expiresAt: expiresAt,
  );
}

class _TestSessionRefreshService extends SessionRefreshService {
  _TestSessionRefreshService.success(AuthSession session)
      : _result = (() async => session),
        super(environment: const AppEnvironment.development());

  _TestSessionRefreshService.pending(Completer<AuthSession> completer)
      : _result = (() => completer.future),
        super(environment: const AppEnvironment.development());

  _TestSessionRefreshService.failure(SessionRefreshException error)
      : _result = (() => Future<AuthSession>.error(error)),
        super(environment: const AppEnvironment.development());

  final Future<AuthSession> Function() _result;
  int callCount = 0;

  @override
  Future<AuthSession> refresh(AuthSession session) {
    callCount += 1;
    return _result();
  }
}
