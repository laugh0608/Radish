import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/api/experience.ts'),
  'utf8',
);
const consumerSource = [
  '../src/apps/experience-detail/ExperienceDetailApp.tsx',
  '../src/me/MeApp.tsx',
].map((file) => fs.readFileSync(path.resolve(testDirectory, file), 'utf8')).join('\n');

test('当前经验 API 应保留结构化错误并由宿主提供本地化 fallback', () => {
  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /response\.messageKey \? response : \{ \.\.\.response, message: undefined \}/);
  assert.match(apiSource, /getMyExperience\(t: TFunction\): Promise<ExperienceData>/);
  assert.match(apiSource, /getTransactions\([\s\S]*t: TFunction\): Promise<PagedResponse<ExpTransactionData>>/);
  assert.doesNotMatch(apiSource, /暂时无法获取等级数据|加载经验流水失败/);
});

test('经验详情与 Me 摘要不得消费 voExpTypeDisplay', () => {
  assert.doesNotMatch(consumerSource, /voExpTypeDisplay/);
  assert.match(consumerSource, /formatExperienceType\([^,]+\.voExpType, t\)/);
});
