import assert from 'node:assert/strict';
import test from 'node:test';
import {
  isSensitiveLogField,
  REDACTED_LOG_VALUE,
  sanitizeLogArgs,
  sanitizeLogValue,
} from '../src/utils/logSanitizer.ts';

test('sanitizeLogValue 应递归脱敏敏感字段并保留普通字段', () => {
  const source = {
    userId: 'usr_1',
    password: 'login-pass',
    nested: {
      currentPassword: 'old-pass',
      new_password: 'new-pass',
      confirmPassword: 'new-pass',
    },
    rows: [
      { api_key: 'secret-key', role: 'Admin' },
      { message: 'ok' },
    ],
  };

  const sanitized = sanitizeLogValue(source);

  assert.deepEqual(sanitized, {
    userId: 'usr_1',
    password: REDACTED_LOG_VALUE,
    nested: {
      currentPassword: REDACTED_LOG_VALUE,
      new_password: REDACTED_LOG_VALUE,
      confirmPassword: REDACTED_LOG_VALUE,
    },
    rows: [
      { api_key: REDACTED_LOG_VALUE, role: 'Admin' },
      { message: 'ok' },
    ],
  });

  assert.equal(source.password, 'login-pass');
  assert.equal(source.nested.confirmPassword, 'new-pass');
});

test('sanitizeLogArgs 应共享循环引用上下文并脱敏 Error 附加数据', () => {
  const circular: Record<string, unknown> = { refreshToken: 'secret-token' };
  circular.self = circular;
  const error = new Error('failed');
  Object.assign(error, {
    request: {
      currentPassword: 'old-pass',
      operator: 'admin',
    },
  });

  const sanitized = sanitizeLogArgs([circular, error]);

  assert.deepEqual(sanitized[0], {
    refreshToken: REDACTED_LOG_VALUE,
    self: '[Circular]',
  });

  assert.equal((sanitized[1] as { request: { currentPassword: string } }).request.currentPassword, REDACTED_LOG_VALUE);
  assert.equal((sanitized[1] as { request: { operator: string } }).request.operator, 'admin');
});

test('isSensitiveLogField 应识别常见密码、口令和令牌字段变体', () => {
  assert.equal(isSensitiveLogField('password'), true);
  assert.equal(isSensitiveLogField('new_password'), true);
  assert.equal(isSensitiveLogField('confirm-password'), true);
  assert.equal(isSensitiveLogField('refreshToken'), true);
  assert.equal(isSensitiveLogField('displayName'), false);
});
