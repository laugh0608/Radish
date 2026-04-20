import 'dart:convert';
import 'dart:io';

import '../config/app_environment.dart';

typedef JsonFactory<T> = T Function(Object? json);

class RadishApiClientException implements Exception {
  const RadishApiClientException(
    this.message, {
    this.statusCode,
    this.code,
  });

  final String message;
  final int? statusCode;
  final String? code;

  @override
  String toString() {
    return 'RadishApiClientException(statusCode: $statusCode, code: $code, message: $message)';
  }
}

abstract class RadishApiClient {
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
  });
}

class HttpRadishApiClient implements RadishApiClient {
  const HttpRadishApiClient({
    required this.environment,
  });

  final AppEnvironment environment;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
  }) async {
    final client = HttpClient();
    if (environment.allowLocalDevelopmentCertificates) {
      client.badCertificateCallback = (certificate, host, port) {
        return environment.name == 'development' &&
            host == 'localhost' &&
            port == 5000;
      };
    }

    try {
      final request = await client.getUrl(uri);
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');

      final response = await request.close();
      final body = await response.transform(utf8.decoder).join();
      final payload = body.isEmpty ? null : jsonDecode(body);

      if (payload is! Map) {
        throw RadishApiClientException(
          'Unexpected response payload from ${uri.path}',
          statusCode: response.statusCode,
        );
      }

      final envelope =
          Map<String, Object?>.from(payload.cast<Object?, Object?>());
      final isSuccess = envelope['isSuccess'] == true;
      final statusCode =
          _readInt(envelope['statusCode']) ?? response.statusCode;
      final message = envelope['messageInfo']?.toString() ?? 'Request failed';
      final code = envelope['code']?.toString();

      if (!isSuccess) {
        throw RadishApiClientException(
          message,
          statusCode: statusCode,
          code: code,
        );
      }

      return decode(envelope['responseData']);
    } on SocketException catch (error) {
      throw RadishApiClientException(
        'Network error: ${error.message}',
      );
    } on FormatException catch (error) {
      throw RadishApiClientException(
        'Failed to decode response: ${error.message}',
      );
    } finally {
      client.close(force: true);
    }
  }

  int? _readInt(Object? value) {
    if (value is int) {
      return value;
    }

    return int.tryParse(value?.toString() ?? '');
  }
}
