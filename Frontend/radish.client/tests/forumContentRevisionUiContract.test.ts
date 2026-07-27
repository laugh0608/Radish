import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildContentRevisionRestoreFingerprint } from '../src/apps/forum/utils/forumSubmissionFingerprint.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientSource = (relativePath: string): string => (
  readFileSync(resolve(clientRoot, relativePath), 'utf8')
);

test('版本恢复指纹应隔离目标类型、目标版本与当前 CAS 版本', () => {
  const base = buildContentRevisionRestoreFingerprint('post', '10', '20', 4);

  assert.equal(base, buildContentRevisionRestoreFingerprint('post', 10, 20, 4));
  assert.notEqual(base, buildContentRevisionRestoreFingerprint('comment', '10', '20', 4));
  assert.notEqual(base, buildContentRevisionRestoreFingerprint('post', '10', '21', 4));
  assert.notEqual(base, buildContentRevisionRestoreFingerprint('post', '10', '20', 5));
});

test('论坛版本 API 应保留结构化错误以驱动冲突与失权状态', () => {
  const apiSource = readClientSource('src/api/forum.ts');

  for (const functionName of [
    'updatePost',
    'updateComment',
    'getPostRevisionList',
    'getPostRevisionDetail',
    'restorePostRevision',
    'getCommentRevisionList',
    'getCommentRevisionDetail',
    'restoreCommentRevision',
  ]) {
    const functionStart = apiSource.indexOf(`export async function ${functionName}`);
    const functionSource = apiSource.slice(
      functionStart,
      apiSource.indexOf('\n}', functionStart) + 2,
    );
    assert.match(functionSource, /createApiResponseError/, functionName);
    assert.doesNotMatch(functionSource, /throw new Error/, functionName);
  }
});

test('版本页面应覆盖 PC mobile、CAS 冲突、失权清理与旧历史只读入口', () => {
  const modalSource = readClientSource('src/apps/forum/components/ContentRevisionModal.tsx');
  const modalStyles = readClientSource('src/apps/forum/components/ContentRevisionModal.module.css');

  assert.match(modalSource, /<Modal/);
  assert.match(modalSource, /<BottomSheet/);
  assert.match(modalSource, /ForumContentRevisionErrorCode\.Conflict/);
  assert.match(modalSource, /clearSensitiveState/);
  assert.match(modalSource, /sessionKey/);
  assert.match(modalSource, /requestEpochRef/);
  assert.match(modalSource, /getPostEditHistory/);
  assert.match(modalSource, /getCommentEditHistory/);
  assert.match(modalSource, /buildContentRevisionRestoreFingerprint/);
  assert.match(modalSource, /closeOnOverlayClick=\{!restoring && !confirmingRestore\}/);
  assert.doesNotMatch(modalSource, /localStorage|sessionStorage/);
  assert.match(modalStyles, /@media \(max-width: 768px\)/);
  assert.match(modalStyles, /prefers-reduced-motion/);
  assert.doesNotMatch(modalStyles, /#[0-9a-f]{3,8}/i);
});

test('正式论坛应同时接入帖子、根评论与子评论版本入口和编辑器回填', () => {
  const publicDetailSource = readClientSource('src/public/forum/PublicForumDetail.tsx');
  const forumAppSource = readClientSource('src/apps/forum/ForumApp.tsx');
  const commentNodeSource = readClientSource('src/apps/forum/components/CommentNode.tsx');
  const postModalSource = readClientSource('src/apps/forum/components/EditPostModal.tsx');

  assert.match(publicDetailSource, /postRevisionTarget/);
  assert.match(publicDetailSource, /commentRevisionTarget/);
  assert.match(publicDetailSource, /commentRevisionDraft/);
  assert.match(publicDetailSource, /handleEditComment/);
  assert.match(forumAppSource, /ContentRevisionModal/);
  assert.match(forumAppSource, /commentRevisionDraft/);
  assert.match(commentNodeSource, /onViewHistory=\{onViewHistory\}/);
  assert.match(commentNodeSource, /revisionDraft\.voContent/);
  assert.match(postModalSource, /matchingRevisionDraft\?\.voTitle/);
  assert.match(postModalSource, /matchingRevisionDraft\.voTags/);
});

test('正式个人中心应提供统一登出入口以支持版本验收账号切换', () => {
  const meSource = readClientSource('src/me/MeApp.tsx');

  assert.match(meSource, /import \{ logout, redirectToLogin \} from '@\/services\/auth';/);
  assert.match(meSource, /onClick=\{logout\}/);
  assert.match(meSource, /t\('auth\.logout'\)/);
});
