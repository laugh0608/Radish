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
    recipientId: 'usr_1',
    paymentPassword: '123456',
    nested: {
      currentPassword: 'old-pass',
      new_password: 'new-pass',
      profile: {
        displayName: '萝卜',
        confirmPassword: 'new-pass',
      },
    },
    items: [
      { apiKey: 'secret-key', amount: 20 },
      { message: 'ok' },
    ],
  };

  const sanitized = sanitizeLogValue(source);

  assert.deepEqual(sanitized, {
    recipientId: 'usr_1',
    paymentPassword: REDACTED_LOG_VALUE,
    nested: {
      currentPassword: REDACTED_LOG_VALUE,
      new_password: REDACTED_LOG_VALUE,
      profile: {
        displayName: '萝卜',
        confirmPassword: REDACTED_LOG_VALUE,
      },
    },
    items: [
      { apiKey: REDACTED_LOG_VALUE, amount: 20 },
      { message: 'ok' },
    ],
  });

  assert.equal(source.paymentPassword, '123456');
  assert.equal(source.nested.profile.confirmPassword, 'new-pass');
});

test('sanitizeLogArgs 应共享循环引用上下文并脱敏 Error 附加数据', () => {
  const circular: Record<string, unknown> = { password: 'secret' };
  circular.self = circular;
  const error = new Error('failed');
  Object.assign(error, {
    request: {
      paymentPasscode: '654321',
      amount: 10,
    },
  });

  const sanitized = sanitizeLogArgs([circular, error]);

  assert.deepEqual(sanitized[0], {
    password: REDACTED_LOG_VALUE,
    self: '[Circular]',
  });

  assert.equal((sanitized[1] as { request: { paymentPasscode: string } }).request.paymentPasscode, REDACTED_LOG_VALUE);
  assert.equal((sanitized[1] as { request: { amount: number } }).request.amount, 10);
});

test('isSensitiveLogField 应识别常见密码、口令和令牌字段变体', () => {
  assert.equal(isSensitiveLogField('paymentPassword'), true);
  assert.equal(isSensitiveLogField('current_password'), true);
  assert.equal(isSensitiveLogField('confirm-password'), true);
  assert.equal(isSensitiveLogField('accessToken'), true);
  assert.equal(isSensitiveLogField('recipientId'), false);
});
