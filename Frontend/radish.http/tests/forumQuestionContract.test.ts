import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { ForumQuestionErrorCode } from '../src/forum-question-contract.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const contractSource = readFileSync(
  resolve(testDirectory, '../src/forum-question-contract.ts'),
  'utf8',
);
const moderationSource = readFileSync(
  resolve(testDirectory, '../src/content-moderation-contract.ts'),
  'utf8',
);
const clientSource = readFileSync(
  resolve(testDirectory, '../src/forum-question-client.ts'),
  'utf8',
);

test('问答回答契约固定 PublicId、双 CAS 与结构化错误码', () => {
  assert.equal(ForumQuestionErrorCode.Conflict, 'Forum.AnswerRevisionConflict');
  assert.equal(
    ForumQuestionErrorCode.AcceptanceConflict,
    'Forum.QuestionAcceptanceConflict',
  );
  assert.match(contractSource, /answerPublicId: string;/);
  assert.match(contractSource, /expectedContentRevision: number;/);
  assert.match(contractSource, /expectedAcceptanceRevision: number;/);
});

test('回答分页和治理契约显式支持 PostAnswer', () => {
  assert.match(contractSource, /interface PostAnswerPageVo[\s\S]*voItems: PostAnswerVo\[\];/);
  assert.match(moderationSource, /\| 'PostAnswer'/);
});

test('问答客户端方法统一走认证 API 客户端', () => {
  assert.match(clientSource, /const authenticated = \{ withAuth: true \}/);
  assert.match(clientSource, /function getPostAnswerPage/);
  assert.match(clientSource, /function restorePostAnswerRevision/);
  assert.match(clientSource, /function acceptPostAnswer/);
  assert.match(clientSource, /function revokePostAnswerAcceptance/);
  assert.doesNotMatch(clientSource, /\bfetch\(/);
});
