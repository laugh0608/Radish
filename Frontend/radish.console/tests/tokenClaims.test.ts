import assert from 'node:assert/strict';
import test from 'node:test';
import { getUserNameFromTokenPayload } from '../src/services/tokenClaims.ts';

test('getUserNameFromTokenPayload 应优先返回标准 name claim', () => {
  assert.equal(
    getUserNameFromTokenPayload({
      name: 'standard-name',
      preferred_username: 'preferred-name',
      nickname: 'nick-name',
      unique_name: 'unique-name',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'legacy-name',
    }),
    'standard-name',
  );
});

test('getUserNameFromTokenPayload 在标准字段缺失时应 fallback 到兼容字段', () => {
  assert.equal(
    getUserNameFromTokenPayload({
      nickname: 'nick-name',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'legacy-name',
    }),
    'nick-name',
  );

  assert.equal(
    getUserNameFromTokenPayload({
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'legacy-name',
    }),
    'legacy-name',
  );
});
