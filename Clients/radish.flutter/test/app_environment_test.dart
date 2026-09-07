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

    test('uses an approved HTTPS Gateway for all testing HTTP surfaces', () {
      final environment = AppEnvironment.fromDartDefines(
        defines: const {
          'RADISH_ENVIRONMENT': 'testing',
          'RADISH_GATEWAY_BASE_URL': 'https://radishx.com/',
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'false',
        },
        isAndroid: true,
      );

      expect(environment.name, 'testing');
      expect(environment.gatewayBaseUrl, 'https://radishx.com');
      expect(environment.apiBaseUrl, environment.gatewayBaseUrl);
      expect(environment.authBaseUrl, environment.gatewayBaseUrl);
      expect(environment.allowLocalDevelopmentCertificates, isFalse);
    });

    test('requires an explicit Gateway for testing and production', () {
      for (final name in const ['testing', 'production']) {
        expect(
          () => AppEnvironment.fromDartDefines(
            defines: {'RADISH_ENVIRONMENT': name},
            isAndroid: true,
          ),
          throwsArgumentError,
        );
      }
    });

    test('rejects invalid explicit environment and Gateway values', () {
      for (final defines in const [
        {'RADISH_ENVIRONMENT': ''},
        {'RADISH_ENVIRONMENT': 'staging'},
        {'RADISH_ENVIRONMENT': 'testing', 'RADISH_ENV': 'production'},
        {'RADISH_GATEWAY_BASE_URL': ''},
        {'RADISH_GATEWAY_BASE_URL': 'not a gateway URL'},
        {'RADISH_GATEWAY_BASE_URL': 'ftp://localhost'},
      ]) {
        expect(
          () => AppEnvironment.fromDartDefines(
            defines: defines,
            isAndroid: true,
          ),
          throwsArgumentError,
        );
      }
    });

    test('rejects unsafe distribution Gateway origins', () {
      for (final gatewayBaseUrl in const [
        'http://radishx.com',
        'https://localhost:5000',
        'https://127.0.0.1',
        'https://[::1]',
        'https://192.0.2.10',
        'https://gateway.example',
        'https://gateway.example.com',
        'https://gateway.radish.local',
        'https://singlelabel',
        'https://-bad.radishx.com',
        'https://bad..radishx.com',
        'https://radishx.com:0',
        'https://radishx.com/api',
        'https://radishx.com?source=testflight',
        'https://radishx.com#internal',
        'https://user@radishx.com',
      ]) {
        expect(
          () => AppEnvironment.fromDartDefines(
            defines: {
              'RADISH_ENVIRONMENT': 'testing',
              'RADISH_GATEWAY_BASE_URL': gatewayBaseUrl,
              'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'false',
            },
            isAndroid: true,
          ),
          throwsArgumentError,
          reason: gatewayBaseUrl,
        );
      }
    });

    test('rejects local certificate opt-in outside loopback development', () {
      for (final defines in const [
        {
          'RADISH_ENVIRONMENT': 'testing',
          'RADISH_GATEWAY_BASE_URL': 'https://radishx.com',
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'true',
        },
        {
          'RADISH_ENVIRONMENT': 'development',
          'RADISH_GATEWAY_BASE_URL': 'https://radishx.com',
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'true',
        },
        {
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': 'sometimes',
        },
      ]) {
        expect(
          () => AppEnvironment.fromDartDefines(
            defines: defines,
            isAndroid: true,
          ),
          throwsArgumentError,
        );
      }
    });
  });
}
