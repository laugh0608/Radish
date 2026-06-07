import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';

void main() {
  test('http api client decodes backend MessageModel envelope', () async {
    await _withHttpServer(
      handler: (request) async {
        request.response.headers.contentType = ContentType.json;
        request.response.write(
          jsonEncode({
            'isSuccess': true,
            'statusCode': 200,
            'messageInfo': '查询成功',
            'responseData': {
              'voCanBuy': true,
              'voReason': null,
            },
          }),
        );
        await request.response.close();
      },
      run: (uri) async {
        final client = HttpRadishApiClient(
          environment: _localHttpEnvironment(uri),
        );

        final result = await client.get<Map<String, Object?>>(
          uri: uri,
          decode: (json) => Map<String, Object?>.from(json as Map),
        );

        expect(result['voCanBuy'], isTrue);
        expect(result['voReason'], isNull);
      },
    );
  });

  test('http api client reports empty unauthorized response as login issue',
      () async {
    await _withHttpServer(
      handler: (request) async {
        request.response.statusCode = HttpStatus.unauthorized;
        await request.response.close();
      },
      run: (uri) async {
        final client = HttpRadishApiClient(
          environment: _localHttpEnvironment(uri),
        );

        await expectLater(
          client.get<void>(
            uri: uri,
            decode: (_) {},
          ),
          throwsA(
            isA<RadishApiClientException>()
                .having((error) => error.statusCode, 'statusCode', 401)
                .having(
                  (error) => error.message,
                  'message',
                  contains('登录已失效，请重新登录'),
                ),
          ),
        );
      },
    );
  });

  test('http api client keeps successful non-envelope response invalid',
      () async {
    await _withHttpServer(
      handler: (request) async {
        request.response.headers.contentType = ContentType.json;
        request.response.write(jsonEncode({'voCanBuy': true}));
        await request.response.close();
      },
      run: (uri) async {
        final client = HttpRadishApiClient(
          environment: _localHttpEnvironment(uri),
        );

        await expectLater(
          client.get<void>(
            uri: uri,
            decode: (_) {},
          ),
          throwsA(
            isA<RadishApiClientException>().having(
              (error) => error.message,
              'message',
              contains('接口返回格式异常'),
            ),
          ),
        );
      },
    );
  });
}

Future<void> _withHttpServer({
  required Future<void> Function(HttpRequest request) handler,
  required Future<void> Function(Uri uri) run,
}) async {
  final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
  server.listen((request) async {
    await handler(request);
  });

  try {
    await run(
      Uri.parse(
        'http://${InternetAddress.loopbackIPv4.address}:${server.port}/api/v1/Shop/CheckCanBuy/100061',
      ),
    );
  } finally {
    await server.close(force: true);
  }
}

AppEnvironment _localHttpEnvironment(Uri uri) {
  final baseUrl = '${uri.scheme}://${uri.host}:${uri.port}';

  return AppEnvironment(
    name: 'test',
    apiBaseUrl: baseUrl,
    authBaseUrl: baseUrl,
    gatewayBaseUrl: baseUrl,
    oidcClientId: 'radish-client',
    nativeOidcRedirectUri: 'radish://oidc/callback',
    nativeOidcPostLogoutRedirectUri: 'radish://oidc/logout-complete',
    oidcScopes: 'openid profile offline_access radish-api',
  );
}
