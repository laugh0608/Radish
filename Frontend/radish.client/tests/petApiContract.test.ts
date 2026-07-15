import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/api/pet.ts'),
  'utf8',
);

test('Pet API 应保留结构化错误并由宿主提供本地化 fallback', () => {
  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /response\.messageKey \? response : \{ \.\.\.response, message: undefined \}/);
  assert.match(apiSource, /t: TFunction/);
  assert.doesNotMatch(apiSource, /throw new Error/);
  assert.doesNotMatch(apiSource, /加载宠物状态失败|领取宠物失败|照顾宠物失败/);
});
