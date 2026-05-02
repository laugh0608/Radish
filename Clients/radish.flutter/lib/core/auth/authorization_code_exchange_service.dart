import 'dart:convert';
import 'dart:io';

import '../config/app_environment.dart';
import 'auth_token_codec.dart';
import 'session_store.dart';

class AuthorizationCodeExchangeException implements Exception {
  const AuthorizationCodeExchangeException(this.message);

  final String message;

  @override
  String toString() => 'AuthorizationCodeExchangeException(message: $message)';
}

abstract class AuthorizationCodeExchangeService {
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
  });
}

class HttpAuthorizationCodeExchangeService
    implements AuthorizationCodeExchangeService {
  const HttpAuthorizationCodeExchangeService({
    required this.environment,
  });

  final AppEnvironment environment;

  @override
  Future<AuthSession> redeemAuthorizationCode({
    required String code,
    required String redirectUri,
  }) async {
    final client = HttpClient();
    if (environment.allowLocalDevelopmentCertificates) {
      client.badCertificateCallback = (certificate, host, port) {
        return environment.allowsBadCertificate(host, port);
      };
    }

    try {
      final uri = Uri.parse('${environment.authBaseUrl}/connect/token');
      final request = await client.postUrl(uri);
      request.headers.set(
        HttpHeaders.contentTypeHeader,
        'application/x-www-form-urlencoded',
      );

      final body = Uri(
        queryParameters: {
          'grant_type': 'authorization_code',
          'client_id': environment.oidcClientId,
          'code': code,
          'redirect_uri': redirectUri,
        },
      ).query;
      request.write(body);

      final response = await request.close();
      final responseBody = await response.transform(utf8.decoder).join();
      final payload =
          responseBody.isEmpty ? <String, Object?>{} : jsonDecode(responseBody);

      if (response.statusCode < 200 || response.statusCode >= 300) {
        final error = payload is Map ? payload['error']?.toString() : null;
        final description =
            payload is Map ? payload['error_description']?.toString() : null;
        final detail = [error, description]
            .whereType<String>()
            .where((item) => item.isNotEmpty)
            .join(': ');
        throw AuthorizationCodeExchangeException(
          detail.isEmpty ? '登录授权码换取会话失败，状态码 ${response.statusCode}' : detail,
        );
      }

      if (payload is! Map) {
        throw const AuthorizationCodeExchangeException(
          '登录授权码响应内容无效。',
        );
      }

      final accessToken = payload['access_token']?.toString();
      final refreshToken = payload['refresh_token']?.toString();
      if (accessToken == null || accessToken.isEmpty) {
        throw const AuthorizationCodeExchangeException(
          '登录授权码响应缺少 access_token。',
        );
      }
      if (refreshToken == null || refreshToken.isEmpty) {
        throw const AuthorizationCodeExchangeException(
          '登录授权码响应缺少 refresh_token。',
        );
      }

      final decoded = decodeAccessToken(accessToken);
      if (decoded == null) {
        throw const AuthorizationCodeExchangeException(
          '无法解析登录返回的 access token。',
        );
      }

      return AuthSession(
        accessToken: accessToken,
        refreshToken: refreshToken,
        userId: decoded.userId,
        expiresAt: decoded.expiresAt,
      );
    } on SocketException catch (error) {
      throw AuthorizationCodeExchangeException(
        '网络错误：${error.message}',
      );
    } on FormatException catch (error) {
      throw AuthorizationCodeExchangeException(
        '无法解析登录授权码响应：${error.message}',
      );
    } finally {
      client.close(force: true);
    }
  }
}
