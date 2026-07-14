import assert from 'node:assert/strict';
import test from 'node:test';
import { createLocalizedRequestHeaders, localizeParsedApiResponse } from '../src/i18n-contract.ts';

test('请求语言契约应补充 Accept-Language 且尊重显式请求头', () => {
  const localized = createLocalizedRequestHeaders(undefined, 'en-US');
  assert.equal(localized.get('Accept'), 'application/json');
  assert.equal(localized.get('Accept-Language'), 'en-US');

  const explicit = createLocalizedRequestHeaders({ 'Accept-Language': 'zh-CN' }, 'en-US');
  assert.equal(explicit.get('Accept-Language'), 'zh-CN');
});

test('响应本地化契约应保留服务端诊断字段并仅替换用户可见消息', () => {
  const parsed = {
    ok: false,
    message: '资源状态已发生变化，请刷新后重试',
    messageInfo: '资源状态已发生变化，请刷新后重试',
    messageKey: 'error.common.conflict',
    code: 'Common.Conflict',
    traceId: 'trace-i18n',
    statusCode: 409,
    httpStatus: 409,
  };

  const localized = localizeParsedApiResponse(
    parsed,
    (key) => key === 'error.common.conflict' ? 'The resource has changed.' : key,
  );

  assert.deepEqual(localized, {
    ...parsed,
    message: 'The resource has changed.',
  });
});

test('缺少本地翻译时应回退服务端消息', () => {
  const parsed = {
    ok: false,
    message: '服务端消息',
    messageKey: 'error.unknown',
  };

  assert.equal(localizeParsedApiResponse(parsed, (key) => key), parsed);
});
