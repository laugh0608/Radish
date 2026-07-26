import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/api/userBlock.ts'),
  'utf8',
);
const syncSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/services/userInteractionSync.ts'),
  'utf8',
);
const profileSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/public/profile/PublicProfileApp.tsx'),
  'utf8',
);
const chatSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/apps/chat/ChatConversationHeader.tsx'),
  'utf8',
);

test('UserBlock Web 消费应固定权威端点与结构化错误', () => {
  assert.match(apiSource, /\/api\/v1\/UserBlock\/Block/);
  assert.match(apiSource, /\/api\/v1\/UserBlock\/Unblock/);
  assert.match(apiSource, /\/api\/v1\/UserBlock\/GetMine/);
  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /targetUserPublicId, operationKey/);
});

test('关系失效同步只广播版本并保留稳定 operation key', () => {
  assert.match(syncSource, /radish:user-interaction:operation/);
  assert.match(syncSource, /relationshipVersion/);
  assert.match(syncSource, /getStableUserInteractionOperationKey/);
  assert.match(syncSource, /completeUserInteractionOperation/);
  assert.doesNotMatch(syncSource, /isBlockedByCurrentUser/);
});

test('公开主页与 Direct 应共同消费 UserBlock 权威契约', () => {
  assert.match(profileSource, /blockUser/);
  assert.match(profileSource, /voCanDirectMessage/);
  assert.match(profileSource, /voIsBlockedByCurrentUser/);
  assert.match(chatSource, /blockUser/);
  assert.match(chatSource, /peerPublicId/);
  assert.doesNotMatch(chatSource, /blockDirectConversation/);
});
