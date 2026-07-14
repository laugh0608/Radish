import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ApiResponseError,
  createApiResponseError,
  isApiResponseNotFoundError,
} from '../src/api-response-error.ts';

test('ApiResponseError 应保留结构化错误字段', () => {
  const error = createApiResponseError({
    ok: false,
    message: 'Not found',
    messageInfo: 'Server fallback',
    messageKey: 'error.common.not_found',
    code: 'COMMON_NOT_FOUND',
    statusCode: 404,
    httpStatus: 404,
    traceId: 'trace-1',
  }, 'fallback');

  assert.ok(error instanceof ApiResponseError);
  assert.equal(error.message, 'Not found');
  assert.equal(error.messageInfo, 'Server fallback');
  assert.equal(error.messageKey, 'error.common.not_found');
  assert.equal(error.code, 'COMMON_NOT_FOUND');
  assert.equal(error.traceId, 'trace-1');
});

test('not-found 判定只依赖结构化状态、错误码或消息键', () => {
  assert.equal(isApiResponseNotFoundError(
    createApiResponseError({ ok: false, httpStatus: 404 }, 'missing')
  ), true);
  assert.equal(isApiResponseNotFoundError(
    createApiResponseError({ ok: false, code: 'USER_NOT_FOUND' }, 'missing')
  ), true);
  assert.equal(isApiResponseNotFoundError(
    createApiResponseError({ ok: false, messageKey: 'error.common.not_found' }, 'missing')
  ), true);
  assert.equal(isApiResponseNotFoundError(new Error('404 Not Found')), false);
});
