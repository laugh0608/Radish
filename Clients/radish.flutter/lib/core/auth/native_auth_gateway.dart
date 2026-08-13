import 'dart:convert';

import 'package:flutter/services.dart';

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
  }) : _channel = channel ?? const MethodChannel('radish.flutter/native_auth');

  final MethodChannel _channel;

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
    await _channel.invokeMethod<void>(
      'writeAuthorizationAttempt',
      jsonEncode(attempt.toJson()),
    );
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> takeAuthorizationAttempt() async {
    final payload =
        await _channel.invokeMethod<String>('takeAuthorizationAttempt');
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return NativeOidcAuthorizationAttempt.fromJson(jsonDecode(payload));
  }

  @override
  Future<void> clearAuthorizationAttempt() async {
    await _channel.invokeMethod<void>('clearAuthorizationAttempt');
  }
}
