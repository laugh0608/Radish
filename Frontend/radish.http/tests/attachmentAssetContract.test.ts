import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const source = fs.readFileSync(
  path.resolve(testDirectory, '../src/attachment-asset.ts'),
  'utf8',
);
const clientSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/client.ts'),
  'utf8',
);

test('认证附件二进制读取复用统一客户端且不在协议层创建 object URL', () => {
  assert.match(source, /apiFetch\(/);
  assert.match(source, /withAuth:\s*true/);
  assert.match(source, /signal,/);
  assert.match(source, /response\.blob\(\)/);
  assert.doesNotMatch(source, /createObjectURL|revokeObjectURL|fetch\(/);
});

test('统一客户端把宿主 AbortSignal 合并进超时控制器', () => {
  assert.match(clientSource, /signal:\s*externalSignal/);
  assert.match(clientSource, /externalSignal\?\.addEventListener\('abort', handleExternalAbort/);
  assert.match(clientSource, /signal:\s*controller\.signal/);
  assert.match(clientSource, /externalSignal\?\.removeEventListener\('abort', handleExternalAbort\)/);
});
