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
    this.error,
    this.errorDescription,
  });

  final NativeAuthCallbackType type;
  final String? code;
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
      error: json['error']?.toString(),
      errorDescription: json['errorDescription']?.toString(),
    );
  }
}

abstract class NativeAuthGateway {
  Future<void> openAuthorizeUrl(Uri authorizeUri);

  Future<void> openLogoutUrl(Uri logoutUri);

  Future<NativeAuthCallbackPayload?> takePendingCallback();
}

class InMemoryNativeAuthGateway implements NativeAuthGateway {
  InMemoryNativeAuthGateway({
    NativeAuthCallbackPayload? initialPendingCallback,
  }) : _pendingCallback = initialPendingCallback;

  NativeAuthCallbackPayload? _pendingCallback;
  Uri? lastAuthorizeUri;
  Uri? lastLogoutUri;

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
}
