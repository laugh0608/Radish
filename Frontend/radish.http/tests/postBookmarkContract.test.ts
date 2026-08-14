import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { PostBookmarkErrorCode } from '../src/post-bookmark-contract.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const contractSource = readFileSync(
  resolve(testDirectory, '../src/post-bookmark-contract.ts'),
  'utf8',
);
const clientSource = readFileSync(
  resolve(testDirectory, '../src/post-bookmark-client.ts'),
  'utf8',
);

test('帖子收藏契约固定 PublicId、显式目标状态与结构化错误码', () => {
  assert.equal(
    PostBookmarkErrorCode.AuthenticationRequired,
    'PostBookmark.AuthenticationRequired',
  );
  assert.equal(PostBookmarkErrorCode.StateConflict, 'PostBookmark.StateConflict');
  assert.match(
    contractSource,
    /interface SetPostBookmarkStateRequest[\s\S]*postIdentifier: string;[\s\S]*isBookmarked: boolean;/,
  );
  assert.doesNotMatch(
    contractSource.match(/interface SetPostBookmarkStateRequest[\s\S]*?\n}/)?.[0] ?? '',
    /postId:/,
  );
  assert.match(contractSource, /bookmarkIdentifier: string;/);
  assert.match(contractSource, /voTargetStatus: PostBookmarkTargetStatus;/);
});

test('不可用收藏项的内容字段保持可空且附件 ID 使用字符串', () => {
  assert.match(contractSource, /voPostPublicId\?: string \| null;/);
  assert.match(contractSource, /voTitle\?: string \| null;/);
  assert.match(contractSource, /voAuthorPublicId\?: string \| null;/);
  assert.match(contractSource, /voCoverAttachmentId\?: string \| null;/);
});

test('帖子收藏客户端统一走认证 API 客户端', () => {
  assert.match(clientSource, /const authenticated = \{ withAuth: true \}/);
  assert.match(clientSource, /function setPostBookmarkState/);
  assert.match(clientSource, /function getMyPostBookmarks/);
  assert.match(clientSource, /function removePostBookmark/);
  assert.doesNotMatch(clientSource, /\bfetch\(/);
});
