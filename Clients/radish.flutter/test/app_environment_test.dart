import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/core/config/app_environment.dart';

void main() {
  group('AppEnvironment.fromDartDefines', () {
    test('defaults Android builds to the local Gateway development endpoint',
        () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {},
        isAndroid: true,
      );

      expect(environment.name, 'development');
      expect(environment.gatewayBaseUrl, 'https://localhost:5000');
      expect(environment.apiBaseUrl, environment.gatewayBaseUrl);
      expect(environment.authBaseUrl, environment.gatewayBaseUrl);
      expect(environment.allowLocalDevelopmentCertificates, isTrue);
      expect(environment.allowsBadCertificate('localhost', 5000), isTrue);
      expect(environment.allowsBadCertificate('localhost', 5001), isFalse);
    });

    test('keeps non-Android local development strict by default', () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {},
        isAndroid: false,
      );

      expect(environment.name, 'development');
      expect(environment.gatewayBaseUrl, 'https://localhost:5000');
      expect(environment.allowLocalDevelopmentCertificates, isFalse);
      expect(environment.allowsBadCertificate('localhost', 5000), isFalse);
    });

    test('uses a test Gateway URL from dart-define for all HTTP surfaces', () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {
          'RADISH_ENVIRONMENT': 'testing',
          'RADISH_GATEWAY_BASE_URL': 'https://test-gateway.radish.local/',
        },
        isAndroid: true,
      );

      expect(environment.name, 'testing');
      expect(environment.gatewayBaseUrl, 'https://test-gateway.radish.local');
      expect(environment.apiBaseUrl, environment.gatewayBaseUrl);
      expect(environment.authBaseUrl, environment.gatewayBaseUrl);
      expect(environment.allowLocalDevelopmentCertificates, isFalse);
    });

    test('falls back to production default when only production mode is set',
        () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {
          'RADISH_ENVIRONMENT': 'production',
        },
        isAndroid: true,
      );

      expect(environment.name, 'production');
      expect(environment.gatewayBaseUrl, 'https://gateway.radish.example');
      expect(environment.allowLocalDevelopmentCertificates, isFalse);
    });

    test('ignores invalid Gateway URLs and honors explicit cert override', () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {
          'RADISH_GATEWAY_BASE_URL': 'not a gateway URL',
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'false',
        },
        isAndroid: true,
      );

      expect(environment.gatewayBaseUrl, 'https://localhost:5000');
      expect(environment.allowLocalDevelopmentCertificates, isFalse);
      expect(environment.allowsBadCertificate('localhost', 5000), isFalse);
    });
  });
}
