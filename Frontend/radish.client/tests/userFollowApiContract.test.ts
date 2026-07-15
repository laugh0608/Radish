import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/api/userFollow.ts'),
  'utf8',
);

test('UserFollow API 应保留结构化错误并由宿主提供本地化 fallback', () => {
  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /response\.messageKey \? response : \{ \.\.\.response, message: undefined \}/);
  assert.match(apiSource, /t: TFunction/);
  assert.doesNotMatch(apiSource, /throw new Error/);
  assert.doesNotMatch(apiSource, /关注失败|获取粉丝列表失败|获取关系链动态失败/);
});
