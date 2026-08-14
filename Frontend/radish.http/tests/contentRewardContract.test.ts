import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  ContentRewardErrorCode,
  ContentRewardReasonCodes,
  ContentRewardTargetTypes,
} from '../src/content-reward-contract.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const contractSource = readFileSync(
  resolve(testDirectory, '../src/content-reward-contract.ts'),
  'utf8',
);
const indexSource = readFileSync(resolve(testDirectory, '../src/index.ts'), 'utf8');

test('内容赞赏共享契约固定目标、理由和结构化错误码', () => {
  assert.deepEqual(Object.values(ContentRewardTargetTypes), ['Post', 'Comment']);
  assert.deepEqual(Object.values(ContentRewardReasonCodes), [
    'Helpful',
    'Insightful',
    'WellWritten',
    'Detailed',
    'Warm',
  ]);
  assert.equal(ContentRewardErrorCode.InsufficientBalance, 'ContentReward.InsufficientBalance');
  assert.equal(ContentRewardErrorCode.Processing, 'ContentReward.Processing');
  assert.equal(
    ContentRewardErrorCode.RelationshipTemporarilyUnavailable,
    'UserBlock.RelationshipTemporarilyUnavailable',
  );
});

test('创建请求不允许客户端提交金额、收款人或自由理由文本', () => {
  const requestMatch = contractSource.match(
    /export interface CreateContentRewardRequest \{([\s\S]*?)\n\}/,
  );
  assert.ok(requestMatch);
  assert.match(requestMatch[1], /targetType:/);
  assert.match(requestMatch[1], /targetId:/);
  assert.match(requestMatch[1], /reasonCode:/);
  assert.match(requestMatch[1], /idempotencyKey:/);
  assert.doesNotMatch(requestMatch[1], /amount|recipient|reasonText/i);
});

test('目标状态显式暴露创建开关并从统一入口导出', () => {
  assert.match(
    contractSource,
    /interface ContentRewardTargetStateVo[\s\S]*voCreateEnabled: boolean;/,
  );
  assert.match(indexSource, /ContentRewardTargetStateVo,[\s\S]*from '\.\/content-reward-contract';/);
  assert.match(indexSource, /ContentRewardErrorCode,[\s\S]*from '\.\/content-reward-contract';/);
});
