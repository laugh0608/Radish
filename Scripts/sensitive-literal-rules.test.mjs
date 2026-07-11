import assert from 'node:assert/strict';
import test from 'node:test';

import { findSensitiveLiteralMatches } from './sensitive-literal-rules.mjs';

function buildJwtFixture() {
  return [
    `eyJ${'a'.repeat(20)}`,
    `eyJ${'b'.repeat(30)}`,
    'c'.repeat(40),
  ].join('.');
}

test('应识别完整 JWT 且不回显命中值', () => {
  const token = buildJwtFixture();
  const matches = findSensitiveLiteralMatches('fixture.txt', `ACCESS_TOKEN="${token}"`);

  assert.deepEqual(matches, [
    {
      filePath: 'fixture.txt',
      lineNumber: 1,
      ruleId: 'compact-jwt',
      description: '完整 JWT 字面量',
    },
  ]);
  assert.equal(JSON.stringify(matches).includes(token), false);
});

test('应识别硬编码 Bearer Token 与私钥头', () => {
  const bearer = ['Bearer ', 'literal-token-value-', 'x'.repeat(32)].join('');
  const privateKeyHeader = ['-----BEGIN ', 'PRIVATE KEY-----'].join('');
  const matches = findSensitiveLiteralMatches(
    'fixture.txt',
    `${bearer}\n${privateKeyHeader}`
  );

  assert.deepEqual(
    matches.map((match) => match.ruleId),
    ['literal-bearer-token', 'private-key-header']
  );
});

test('应允许环境变量、模板变量、占位值和无效 Token 测试值', () => {
  const content = [
    'Authorization: Bearer $RADISH_ACCESS_TOKEN',
    'Authorization: Bearer ${accessToken}',
    'Authorization: Bearer {{accessToken}}',
    'Authorization: Bearer <access-token>',
    'Authorization: Bearer invalid_token',
  ].join('\n');

  assert.deepEqual(findSensitiveLiteralMatches('fixture.txt', content), []);
});
