import assert from 'node:assert/strict';
import test from 'node:test';
import { parseHttpResponse } from '../src/response-parser.ts';

test('失败响应应保留结构化数据与真实 HTTP 状态', async () => {
  const parsed = await parseHttpResponse<{ voConflicts: Array<{ voIndex: number }> }>(new Response(JSON.stringify({
    isSuccess: false,
    statusCode: 409,
    messageInfo: 'Conflict',
    messageKey: 'error.sticker.batch_code_conflict',
    code: 'BatchCodeConflict',
    responseData: {
      voConflicts: [{ voIndex: 1 }],
    },
  }), {
    status: 409,
    headers: { 'Content-Type': 'application/json' },
  }));

  assert.equal(parsed.ok, false);
  assert.equal(parsed.httpStatus, 409);
  assert.equal(parsed.code, 'BatchCodeConflict');
  assert.deepEqual(parsed.data, { voConflicts: [{ voIndex: 1 }] });
});
