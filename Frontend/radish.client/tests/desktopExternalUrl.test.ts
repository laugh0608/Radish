import assert from 'node:assert/strict';
import test from 'node:test';

import {
  resolveConsoleExternalUrl,
  resolveScalarExternalUrl,
} from '../src/desktop/externalAppUrl.ts';

test('resolveConsoleExternalUrl 在本地 client 开发直连时返回 Console 开发地址', () => {
  assert.equal(
    resolveConsoleExternalUrl({
      hostname: 'localhost',
      port: '3000',
    }),
    'http://localhost:3100',
  );

  assert.equal(
    resolveConsoleExternalUrl({
      hostname: '127.0.0.1',
      port: '3000',
    }),
    'http://localhost:3100',
  );
});

test('resolveConsoleExternalUrl 在 Gateway 和生产域名下返回相对路径', () => {
  assert.equal(
    resolveConsoleExternalUrl({
      hostname: 'localhost',
      port: '5000',
    }),
    '/console/',
  );

  assert.equal(
    resolveConsoleExternalUrl({
      hostname: 'radishx.com',
      port: '',
    }),
    '/console/',
  );
});

test('resolveScalarExternalUrl 仅在本地 client 开发直连时返回 Scalar 开发地址', () => {
  assert.equal(
    resolveScalarExternalUrl({
      hostname: 'localhost',
      port: '3000',
    }),
    'http://localhost:5100/scalar?auto=1',
  );

  assert.equal(
    resolveScalarExternalUrl({
      hostname: 'radishx.com',
      port: '',
    }),
    '/scalar?auto=1',
  );
});
