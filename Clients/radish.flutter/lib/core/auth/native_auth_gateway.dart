import 'dart:convert';

import 'package:flutter/services.dart';

import '../storage/secure_value_store.dart';

enum NativeAuthCallbackType {
  login,
  logout,
}

class NativeAuthCallbackPayload {
  const NativeAuthCallbackPayload({
    required this.type,
    this.code,
    this.state,
    this.error,
    this.errorDescription,
  });

  final NativeAuthCallbackType type;
  final String? code;
  final String? state;
  final String? error;
  final String? errorDescription;

  static NativeAuthCallbackPayload? fromJson(Object? json) {
    if (json is! Map) {
      return null;
    }

    final typeValue = json['type']?.toString();
    final type = switch (typeValue) {
      'login' => NativeAuthCallbackType.login,
      'logout' => NativeAuthCallbackType.logout,
      _ => null,
    };
    if (type == null) {
      return null;
    }

    return NativeAuthCallbackPayload(
      type: type,
      code: json['code']?.toString(),
      state: json['state']?.toString(),
      error: json['error']?.toString(),
      errorDescription: json['errorDescription']?.toString(),
    );
  }
}

class NativeOidcAuthorizationAttempt {
  const NativeOidcAuthorizationAttempt({
    required this.state,
    required this.codeVerifier,
    required this.redirectUri,
    required this.startedAt,
  });

  final String state;
  final String codeVerifier;
  final String redirectUri;
  final DateTime startedAt;

  Map<String, Object?> toJson() {
    return {
      'state': state,
      'codeVerifier': codeVerifier,
      'redirectUri': redirectUri,
      'startedAt': startedAt.toUtc().toIso8601String(),
    };
  }

  static NativeOidcAuthorizationAttempt? fromJson(Object? json) {
    if (json is! Map) {
      return null;
    }

    final state = json['state']?.toString().trim();
    final codeVerifier = json['codeVerifier']?.toString().trim();
    final redirectUri = json['redirectUri']?.toString().trim();
    final startedAt =
        DateTime.tryParse(json['startedAt']?.toString() ?? '')?.toUtc();
    if (state == null ||
        state.isEmpty ||
        codeVerifier == null ||
        codeVerifier.length < 43 ||
        redirectUri == null ||
        redirectUri.isEmpty ||
        startedAt == null) {
      return null;
    }

    return NativeOidcAuthorizationAttempt(
      state: state,
      codeVerifier: codeVerifier,
      redirectUri: redirectUri,
      startedAt: startedAt,
    );
  }
}

abstract interface class AuthorizationAttemptStore {
  Future<NativeOidcAuthorizationAttempt?> read();

  Future<void> write(NativeOidcAuthorizationAttempt attempt);

  Future<NativeOidcAuthorizationAttempt?> take();

  Future<void> clear();
}

class SecureAuthorizationAttemptStore implements AuthorizationAttemptStore {
  SecureAuthorizationAttemptStore({
    required SecureValueStore secureValues,
  }) : _secureValues = secureValues;

  static const storageKey = 'radish.auth.oidc_attempt.v1';

  final SecureValueStore _secureValues;

  @override
  Future<void> clear() {
    return _secureValues.delete(storageKey);
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> read() async {
    final payload = await _secureValues.read(storageKey);
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return NativeOidcAuthorizationAttempt.fromJson(jsonDecode(payload));
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> take() async {
    final attempt = await read();
    await clear();
    return attempt;
  }

  @override
  Future<void> write(NativeOidcAuthorizationAttempt attempt) {
    return _secureValues.write(
      storageKey,
      jsonEncode(attempt.toJson()),
    );
  }
}

abstract class NativeAuthGateway {
  Future<void> openAuthorizeUrl(Uri authorizeUri);

  Future<void> openLogoutUrl(Uri logoutUri);

  Future<NativeAuthCallbackPayload?> takePendingCallback();

  Future<void> writeAuthorizationAttempt(
      NativeOidcAuthorizationAttempt attempt);

  Future<NativeOidcAuthorizationAttempt?> takeAuthorizationAttempt();

  Future<void> clearAuthorizationAttempt();
}

class InMemoryNativeAuthGateway implements NativeAuthGateway {
  InMemoryNativeAuthGateway({
    NativeAuthCallbackPayload? initialPendingCallback,
    NativeOidcAuthorizationAttempt? initialAuthorizationAttempt,
  })  : _pendingCallback = initialPendingCallback,
        _authorizationAttempt = initialAuthorizationAttempt;

  NativeAuthCallbackPayload? _pendingCallback;
  NativeOidcAuthorizationAttempt? _authorizationAttempt;
  Uri? lastAuthorizeUri;
  Uri? lastLogoutUri;

  void setPendingCallback(NativeAuthCallbackPayload? callback) {
    _pendingCallback = callback;
  }

  @override
  Future<void> openAuthorizeUrl(Uri authorizeUri) async {
    lastAuthorizeUri = authorizeUri;
  }

  @override
  Future<void> openLogoutUrl(Uri logoutUri) async {
    lastLogoutUri = logoutUri;
  }

  @override
  Future<NativeAuthCallbackPayload?> takePendingCallback() async {
    final callback = _pendingCallback;
    _pendingCallback = null;
    return callback;
  }

  @override
  Future<void> writeAuthorizationAttempt(
      NativeOidcAuthorizationAttempt attempt) async {
    _authorizationAttempt = attempt;
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> takeAuthorizationAttempt() async {
    final attempt = _authorizationAttempt;
    _authorizationAttempt = null;
    return attempt;
  }

  @override
  Future<void> clearAuthorizationAttempt() async {
    _authorizationAttempt = null;
  }
}

class PlatformNativeAuthGateway implements NativeAuthGateway {
  PlatformNativeAuthGateway({
    MethodChannel? channel,
    required AuthorizationAttemptStore authorizationAttemptStore,
  })  : _channel = channel ?? const MethodChannel('radish.flutter/native_auth'),
        _authorizationAttemptStore = authorizationAttemptStore;

  final MethodChannel _channel;
  final AuthorizationAttemptStore _authorizationAttemptStore;

  @override
  Future<void> openAuthorizeUrl(Uri authorizeUri) async {
    await _channel.invokeMethod<void>(
      'openAuthorizeUrl',
      authorizeUri.toString(),
    );
  }

  @override
  Future<void> openLogoutUrl(Uri logoutUri) async {
    await _channel.invokeMethod<void>(
      'openLogoutUrl',
      logoutUri.toString(),
    );
  }

  @override
  Future<NativeAuthCallbackPayload?> takePendingCallback() async {
    final payload = await _channel.invokeMethod<String>('takePendingCallback');
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return NativeAuthCallbackPayload.fromJson(jsonDecode(payload));
  }

  @override
  Future<void> writeAuthorizationAttempt(
      NativeOidcAuthorizationAttempt attempt) async {
    await _authorizationAttemptStore.write(attempt);
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> takeAuthorizationAttempt() async {
    return _authorizationAttemptStore.take();
  }

  @override
  Future<void> clearAuthorizationAttempt() async {
    await _authorizationAttemptStore.clear();
  }
}
