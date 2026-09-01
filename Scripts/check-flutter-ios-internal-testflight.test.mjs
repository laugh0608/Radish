import assert from 'node:assert/strict';
import test from 'node:test';

import {
  checkFlutterIosInternalTestFlight,
  validateDistributionGatewayOrigin,
  validateInternalTestFlightDefines,
} from './check-flutter-ios-internal-testflight.mjs';

test('distribution Gateway accepts the approved production origin', () => {
  assert.equal(validateDistributionGatewayOrigin('https://radishx.com'), null);
});

test('distribution Gateway rejects unsafe or non-origin URLs', () => {
  for (const candidate of [
    '',
    ' http://radishx.com',
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
    'https://radishx.com/',
    'https://radishx.com/api',
    'https://radishx.com?source=testflight',
    'https://radishx.com#internal',
    'https://user@radishx.com',
  ]) {
    assert.notEqual(validateDistributionGatewayOrigin(candidate), null, candidate);
  }
});

test('Internal TestFlight defines are exact and fail closed', () => {
  const validDefines = {
    RADISH_ENVIRONMENT: 'testing',
    RADISH_GATEWAY_BASE_URL: 'https://radishx.com',
    RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES: 'false',
  };
  assert.deepEqual(validateInternalTestFlightDefines(validDefines), []);

  assert.ok(validateInternalTestFlightDefines({
    ...validDefines,
    RADISH_ENVIRONMENT: 'production',
  }).length > 0);
  assert.ok(validateInternalTestFlightDefines({
    ...validDefines,
    RADISH_GATEWAY_BASE_URL: 'https://other.radishx.com',
  }).length > 0);
  assert.ok(validateInternalTestFlightDefines({
    ...validDefines,
    RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES: 'true',
  }).length > 0);
  assert.ok(validateInternalTestFlightDefines({
    ...validDefines,
    EXTRA_DEFINE: 'not-allowed',
  }).length > 0);
});

test('repository Internal TestFlight preflight passes current candidate facts', () => {
  const result = checkFlutterIosInternalTestFlight();
  assert.deepEqual(result.issues, []);
  assert.equal(result.summary.gatewayOrigin, 'https://radishx.com');
  assert.equal(result.summary.productVersion, '26.8.2');
  assert.equal(result.summary.buildNumber, 1);
  assert.deepEqual(result.summary.dependencyPrivacyManifestPackages, [
    'flutter_secure_storage_darwin',
    'shared_preferences_foundation',
  ]);
});
