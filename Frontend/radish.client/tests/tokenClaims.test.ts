import assert from 'node:assert/strict';
import test from 'node:test';
import { getRolesFromTokenPayload, getUserIdentityFromTokenPayload } from '../src/services/tokenClaims.ts';

test('getUserIdentityFromTokenPayload 应优先使用标准 claims', () => {
  const identity = getUserIdentityFromTokenPayload({
    sub: '101',
    preferred_username: 'standard-user',
    name: 'display-name',
    tenant_id: '9',
    TenantId: '99',
    role: ['Admin', 'User'],
    roles: ['User', 'Auditor'],
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['LegacyAdmin'],
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': '202',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'legacy-user',
  });

  assert.deepEqual(identity, {
    userId: 101,
    userName: 'standard-user',
    tenantId: 9,
    roles: ['Admin', 'User', 'Auditor'],
  });
});

test('getUserIdentityFromTokenPayload 在标准 claims 缺失时应 fallback 到 legacy claims', () => {
  const identity = getUserIdentityFromTokenPayload({
    TenantId: '12',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'LegacyAdmin,LegacyUser',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': '202',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'legacy-user',
  });

  assert.deepEqual(identity, {
    userId: 202,
    userName: 'legacy-user',
    tenantId: 12,
    roles: ['LegacyAdmin', 'LegacyUser'],
  });
});

test('getRolesFromTokenPayload 在标准角色存在时不应混入 legacy role URI', () => {
  assert.deepEqual(
    getRolesFromTokenPayload({
      role: 'Admin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'LegacyOnly',
    }),
    ['Admin'],
  );
});
