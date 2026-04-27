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
    String? bearerToken,
  });

  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
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
    String? bearerToken,
  }) async {
    return _send(
      method: 'GET',
      uri: uri,
      decode: decode,
      bearerToken: bearerToken,
    );
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    return _send(
      method: 'POST',
      uri: uri,
      decode: decode,
      bearerToken: bearerToken,
      body: body,
    );
  }

  Future<T> _send<T>({
    required String method,
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
    Object? body,
  }) async {
    final client = HttpClient();
    if (environment.allowLocalDevelopmentCertificates) {
      client.badCertificateCallback = (certificate, host, port) {
        return environment.allowsBadCertificate(host, port);
      };
    }

    try {
      final request = method == 'POST'
          ? await client.postUrl(uri)
          : await client.getUrl(uri);
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');
      if (bearerToken != null && bearerToken.trim().isNotEmpty) {
        request.headers.set(
          HttpHeaders.authorizationHeader,
          'Bearer ${bearerToken.trim()}',
        );
      }
      if (method == 'POST') {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(body ?? const <String, Object?>{}));
      }

      final response = await request.close();
      final responseBody = await response.transform(utf8.decoder).join();
      final payload = responseBody.isEmpty ? null : jsonDecode(responseBody);

      if (payload is! Map) {
        throw RadishApiClientException(
          '接口返回格式异常：${uri.path}',
          statusCode: response.statusCode,
        );
      }

      final envelope =
          Map<String, Object?>.from(payload.cast<Object?, Object?>());
      final isSuccess = envelope['isSuccess'] == true;
      final statusCode =
          _readInt(envelope['statusCode']) ?? response.statusCode;
      final message = envelope['messageInfo']?.toString() ?? '请求失败';
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
        '网络连接失败：${error.message}',
      );
    } on FormatException catch (error) {
      throw RadishApiClientException(
        '响应解析失败：${error.message}',
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
