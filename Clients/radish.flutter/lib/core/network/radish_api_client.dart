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

  Future<T> put<T>({
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

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    return _send(
      method: 'PUT',
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
      final request = switch (method) {
        'POST' => await client.postUrl(uri),
        'PUT' => await client.putUrl(uri),
        _ => await client.getUrl(uri),
      };
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');
      if (bearerToken != null && bearerToken.trim().isNotEmpty) {
        request.headers.set(
          HttpHeaders.authorizationHeader,
          'Bearer ${bearerToken.trim()}',
        );
      }
      if (method == 'POST' || method == 'PUT') {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(body ?? const <String, Object?>{}));
      }

      final response = await request.close();
      final responseBody = await response.transform(utf8.decoder).join();
      final statusCode = response.statusCode;
      if (responseBody.isEmpty) {
        throw RadishApiClientException(
          _buildHttpFailureMessage(
            statusCode: statusCode,
            path: uri.path,
          ),
          statusCode: statusCode,
        );
      }

      final Object? payload;
      try {
        payload = jsonDecode(responseBody);
      } on FormatException catch (error) {
        if (!_isHttpSuccess(statusCode)) {
          throw RadishApiClientException(
            _buildHttpFailureMessage(
              statusCode: statusCode,
              path: uri.path,
              fallbackMessage: responseBody,
            ),
            statusCode: statusCode,
          );
        }

        throw RadishApiClientException(
          '响应解析失败：${error.message}',
          statusCode: statusCode,
        );
      }

      if (payload is! Map) {
        throw RadishApiClientException(
          _isHttpSuccess(statusCode)
              ? '接口返回格式异常：${uri.path}'
              : _buildHttpFailureMessage(
                  statusCode: statusCode,
                  path: uri.path,
                ),
          statusCode: statusCode,
        );
      }

      final envelope =
          Map<String, Object?>.from(payload.cast<Object?, Object?>());
      final envelopeStatusCode = _readInt(
            _readEnvelopeValue(envelope, const [
              'statusCode',
              'StatusCode',
              'status',
            ]),
          ) ??
          statusCode;
      final isSuccess = _readBool(
        _readEnvelopeValue(envelope, const [
          'isSuccess',
          'IsSuccess',
          'success',
          'Success',
        ]),
      );
      final message = _readString(
            _readEnvelopeValue(envelope, const [
              'messageInfo',
              'MessageInfo',
              'message',
              'Message',
              'title',
              'Title',
            ]),
          ) ??
          _buildHttpFailureMessage(
            statusCode: envelopeStatusCode,
            path: uri.path,
          );
      final code = _readString(
        _readEnvelopeValue(envelope, const [
          'code',
          'Code',
          'error',
          'Error',
        ]),
      );

      if (isSuccess == null && _isHttpSuccess(statusCode)) {
        throw RadishApiClientException(
          '接口返回格式异常：${uri.path}',
          statusCode: statusCode,
        );
      }

      if (isSuccess != true || !_isHttpSuccess(statusCode)) {
        throw RadishApiClientException(
          message,
          statusCode: envelopeStatusCode,
          code: code,
        );
      }

      return decode(
        _readEnvelopeValue(envelope, const [
          'responseData',
          'ResponseData',
          'data',
          'Data',
        ]),
      );
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

  bool? _readBool(Object? value) {
    if (value is bool) {
      return value;
    }

    final text = value?.toString().trim().toLowerCase();
    if (text == null || text.isEmpty) {
      return null;
    }

    if (text == 'true' || text == '1') {
      return true;
    }

    if (text == 'false' || text == '0') {
      return false;
    }

    return null;
  }

  String? _readString(Object? value) {
    if (value == null) {
      return null;
    }

    final text = value.toString().trim();
    return text.isEmpty ? null : text;
  }

  Object? _readEnvelopeValue(
    Map<String, Object?> envelope,
    List<String> keys,
  ) {
    for (final key in keys) {
      if (envelope.containsKey(key)) {
        return envelope[key];
      }
    }

    return null;
  }

  bool _isHttpSuccess(int statusCode) {
    return statusCode >= 200 && statusCode < 300;
  }

  String _buildHttpFailureMessage({
    required int statusCode,
    required String path,
    String? fallbackMessage,
  }) {
    final normalizedFallback = fallbackMessage?.trim();
    if (normalizedFallback != null && normalizedFallback.isNotEmpty) {
      return normalizedFallback;
    }

    return switch (statusCode) {
      401 => '登录已失效，请重新登录：$path',
      403 => '当前账号没有访问权限：$path',
      404 => '接口不存在或资源不可用：$path',
      >= 500 => '服务暂时不可用，状态码 $statusCode：$path',
      _ => '请求失败，状态码 $statusCode：$path',
    };
  }
}
