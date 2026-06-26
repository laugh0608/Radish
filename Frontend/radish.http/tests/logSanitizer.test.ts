import assert from 'node:assert/strict';
import test from 'node:test';
import {
  isSensitiveLogField,
  REDACTED_LOG_VALUE,
  sanitizeLogArgs,
  sanitizeLogValue,
} from '../src/logSanitizer.ts';

test('sanitizeLogValue 应递归脱敏 HTTP 错误对象中的敏感字段', () => {
  const source = {
    code: 'BadRequest',
    request: {
      body: {
        paymentPassword: '123456',
        current_password: 'old-pass',
      },
      headers: {
        accessToken: 'access-token',
        traceId: 'trace-1',
      },
    },
  };

  const sanitized = sanitizeLogValue(source);

  assert.deepEqual(sanitized, {
    code: 'BadRequest',
    request: {
      body: {
        paymentPassword: REDACTED_LOG_VALUE,
        current_password: REDACTED_LOG_VALUE,
      },
      headers: {
        accessToken: REDACTED_LOG_VALUE,
        traceId: 'trace-1',
      },
    },
  });

  assert.equal(source.request.body.paymentPassword, '123456');
});

test('sanitizeLogArgs 应处理循环引用和 Error 附加字段', () => {
  const circular: Record<string, unknown> = { apiKey: 'key' };
  circular.self = circular;
  const error = new Error('failed');
  Object.assign(error, {
    config: {
      data: {
        confirmPassword: 'secret',
      },
    },
  });

  const sanitized = sanitizeLogArgs([circular, error]);

  assert.deepEqual(sanitized[0], {
    apiKey: REDACTED_LOG_VALUE,
    self: '[Circular]',
  });

  assert.equal((sanitized[1] as { config: { data: { confirmPassword: string } } }).config.data.confirmPassword, REDACTED_LOG_VALUE);
});

test('isSensitiveLogField 应识别 HTTP 客户端常见敏感字段', () => {
  assert.equal(isSensitiveLogField('api_key'), true);
  assert.equal(isSensitiveLogField('refreshToken'), true);
  assert.equal(isSensitiveLogField('id-token'), true);
  assert.equal(isSensitiveLogField('traceId'), false);
});
