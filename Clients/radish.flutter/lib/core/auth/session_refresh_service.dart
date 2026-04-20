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
        return environment.name == 'development' &&
            host == 'localhost' &&
            port == 5000;
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
          detail.isEmpty
              ? 'Refresh token request failed with status ${response.statusCode}'
              : detail,
        );
      }

      if (payload is! Map) {
        throw const SessionRefreshException(
          'Refresh token response payload is invalid.',
        );
      }

      final accessToken = payload['access_token']?.toString();
      final refreshToken =
          payload['refresh_token']?.toString() ?? session.refreshToken;
      if (accessToken == null || accessToken.isEmpty) {
        throw const SessionRefreshException(
          'Refresh token response is missing access_token.',
        );
      }

      final decoded = decodeAccessToken(accessToken);
      if (decoded == null) {
        throw const SessionRefreshException(
          'Failed to decode refreshed access token.',
        );
      }

      return AuthSession(
        accessToken: accessToken,
        refreshToken: refreshToken,
        userId: decoded.userId,
        expiresAt: decoded.expiresAt,
      );
    } on SocketException catch (error) {
      throw SessionRefreshException('Network error: ${error.message}');
    } on FormatException catch (error) {
      throw SessionRefreshException(
        'Failed to decode refresh token response: ${error.message}',
      );
    } finally {
      client.close(force: true);
    }
  }
}
