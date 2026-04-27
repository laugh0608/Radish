import 'dart:convert';
import 'dart:io';

import '../config/app_environment.dart';
import 'auth_token_codec.dart';
import 'session_store.dart';

class SessionRefreshException implements Exception {
  const SessionRefreshException(this.message);

  final String message;

  @override
  String toString() => 'SessionRefreshException(message: $message)';
}

class SessionRefreshService {
  const SessionRefreshService({
    required this.environment,
  });

  final AppEnvironment environment;

  Future<AuthSession> refresh(AuthSession session) async {
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
          'grant_type': 'refresh_token',
          'client_id': 'radish-client',
          'refresh_token': session.refreshToken,
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
        throw SessionRefreshException(
          detail.isEmpty ? '刷新登录会话失败，状态码 ${response.statusCode}' : detail,
        );
      }

      if (payload is! Map) {
        throw const SessionRefreshException(
          '刷新登录会话返回内容无效。',
        );
      }

      final accessToken = payload['access_token']?.toString();
      final refreshToken =
          payload['refresh_token']?.toString() ?? session.refreshToken;
      if (accessToken == null || accessToken.isEmpty) {
        throw const SessionRefreshException(
          '刷新登录会话返回内容缺少 access_token。',
        );
      }

      final decoded = decodeAccessToken(accessToken);
      if (decoded == null) {
        throw const SessionRefreshException(
          '无法解析刷新后的 access token。',
        );
      }

      return AuthSession(
        accessToken: accessToken,
        refreshToken: refreshToken,
        userId: decoded.userId,
        expiresAt: decoded.expiresAt,
      );
    } on SocketException catch (error) {
      throw SessionRefreshException('网络错误：${error.message}');
    } on FormatException catch (error) {
      throw SessionRefreshException(
        '无法解析登录刷新响应：${error.message}',
      );
    } finally {
      client.close(force: true);
    }
  }
}
