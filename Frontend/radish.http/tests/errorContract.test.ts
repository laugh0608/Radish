import assert from 'node:assert/strict';
import test from 'node:test';
import { parseHttpResponse } from '../src/response-parser.ts';

test('parseHttpResponse 应保留真实 HTTP 状态与 MessageModel 契约状态', async () => {
  const response = new Response(JSON.stringify({
    statusCode: 409,
    isSuccess: false,
    messageInfo: '资源状态已发生变化，请刷新后重试',
    code: 'Common.Conflict',
    messageKey: 'error.common.conflict',
    traceId: 'trace-body',
  }), {
    status: 409,
    headers: {
      'content-type': 'application/json',
      'x-correlation-id': 'trace-header',
    },
  });

  const parsed = await parseHttpResponse(response);

  assert.deepEqual(parsed, {
    ok: false,
    message: '资源状态已发生变化，请刷新后重试',
    messageInfo: '资源状态已发生变化，请刷新后重试',
    messageKey: 'error.common.conflict',
    code: 'Common.Conflict',
    statusCode: 409,
    traceId: 'trace-body',
    httpStatus: 409,
  });
});

test('parseHttpResponse 应兼容 HTTP 200 的历史失败体但保留真实状态', async () => {
  const response = new Response(JSON.stringify({
    statusCode: 400,
    isSuccess: false,
    messageInfo: '请求参数验证失败',
    code: 'Common.ValidationFailed',
  }), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  });

  const parsed = await parseHttpResponse(response);

  assert.equal(parsed.ok, false);
  assert.equal(parsed.statusCode, 400);
  assert.equal(parsed.httpStatus, 200);
});

test('parseHttpResponse 应保留非空动态消息参数', async () => {
  const response = new Response(JSON.stringify({
    statusCode: 413,
    isSuccess: false,
    messageInfo: '所选文件超过上传大小限制（最大 5 MB）。',
    code: 'Attachment.FileTooLarge',
    messageKey: 'error.attachment.file_too_large',
    messageArguments: ['5 MB'],
  }), {
    status: 413,
    headers: { 'content-type': 'application/json' },
  });

  const parsed = await parseHttpResponse(response);

  assert.deepEqual(parsed.messageArguments, ['5 MB']);
});

test('parseHttpResponse 应从响应头补充 TraceId 并安全处理非 JSON 错误', async () => {
  const jsonResponse = new Response(JSON.stringify({
    statusCode: 500,
    isSuccess: false,
    messageInfo: '服务器处理请求时发生错误，请稍后重试',
    code: 'System.UnexpectedError',
  }), {
    status: 500,
    headers: {
      'content-type': 'application/json',
      'x-correlation-id': 'trace-header',
    },
  });

  const parsedJson = await parseHttpResponse(jsonResponse);
  assert.equal(parsedJson.traceId, 'trace-header');

  const textResponse = new Response('Service unavailable', {
    status: 503,
    headers: { 'content-type': 'text/plain' },
  });
  const parsedText = await parseHttpResponse(textResponse);
  assert.equal(parsedText.message, 'Service unavailable');
  assert.equal(parsedText.statusCode, 503);
  assert.equal(parsedText.httpStatus, 503);
});
