import 'dart:async';
import 'dart:convert';

import 'package:flutter/services.dart';

class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.userId,
    required this.expiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final String userId;
  final DateTime expiresAt;

  bool get isExpired => !expiresAt.isAfter(DateTime.now().toUtc());

  Map<String, Object?> toJson() {
    return {
      'accessToken': accessToken,
      'refreshToken': refreshToken,
      'userId': userId,
      'expiresAt': expiresAt.toUtc().toIso8601String(),
    };
  }

  static AuthSession? fromJson(Object? json) {
    if (json is! Map) {
      return null;
    }

    final accessToken = json['accessToken']?.toString();
    final refreshToken = json['refreshToken']?.toString();
    final userId = json['userId']?.toString();
    final expiresAtValue = json['expiresAt']?.toString();
    final expiresAt = DateTime.tryParse(expiresAtValue ?? '')?.toUtc();

    if (accessToken == null ||
        accessToken.isEmpty ||
        refreshToken == null ||
        refreshToken.isEmpty ||
        userId == null ||
        userId.isEmpty ||
        expiresAt == null) {
      return null;
    }

    return AuthSession(
      accessToken: accessToken,
      refreshToken: refreshToken,
      userId: userId,
      expiresAt: expiresAt,
    );
  }

  AuthSession copyWith({
    String? accessToken,
    String? refreshToken,
    String? userId,
    DateTime? expiresAt,
  }) {
    return AuthSession(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      userId: userId ?? this.userId,
      expiresAt: expiresAt ?? this.expiresAt,
    );
  }
}

abstract class SessionStore {
  Future<AuthSession?> read();

  Future<void> write(AuthSession session);

  Future<void> clear();
}

class InMemorySessionStore implements SessionStore {
  InMemorySessionStore({
    AuthSession? initialSession,
  }) : _session = initialSession;

  AuthSession? _session;

  @override
  Future<void> clear() async {
    _session = null;
  }

  @override
  Future<AuthSession?> read() async {
    return _session;
  }

  @override
  Future<void> write(AuthSession session) async {
    _session = session;
  }
}

class PlatformSessionStore implements SessionStore {
  PlatformSessionStore({
    MethodChannel? channel,
  }) : _channel =
            channel ?? const MethodChannel('radish.flutter/session_store');

  final MethodChannel _channel;

  @override
  Future<void> clear() async {
    await _channel.invokeMethod<void>('clear');
  }

  @override
  Future<AuthSession?> read() async {
    final rawJson = await _channel.invokeMethod<String>('read');
    if (rawJson == null || rawJson.isEmpty) {
      return null;
    }

    return AuthSession.fromJson(jsonDecode(rawJson));
  }

  @override
  Future<void> write(AuthSession session) async {
    await _channel.invokeMethod<void>(
      'write',
      jsonEncode(session.toJson()),
    );
  }
}
