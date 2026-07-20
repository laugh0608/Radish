import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const clientSource = fs.readFileSync(path.resolve(testDirectory, '../src/client.ts'), 'utf8');

test('带认证请求的刷新重放边界应固定为一次', () => {
  const fetchCalls = [...clientSource.matchAll(/await fetch\(url,/g)];

  assert.equal(fetchCalls.length, 2, '认证请求应只包含首次请求和一次重放');
  assert.match(clientSource, /const retryResponse = await fetch\(url, \{[\s\S]*?\.\.\.fetchOptions,[\s\S]*?headers: retryHeaders,[\s\S]*?\}\);/);
  assert.match(clientSource, /return retryResponse;/);
  assert.doesNotMatch(clientSource, /return apiFetch\(/, '重放不得递归进入刷新流程');
});

test('写请求重放应复用首次序列化后的请求配置', () => {
  assert.match(clientSource, /const fetchOptions: RequestInit = \{[\s\S]*?\.\.\.restOptions,[\s\S]*?headers: finalHeaders,[\s\S]*?\};/);
  assert.match(clientSource, /const retryHeaders = new Headers\(finalHeaders\);/);
  assert.match(clientSource, /const retryResponse = await fetch\(url, \{[\s\S]*?\.\.\.fetchOptions,/);
});
