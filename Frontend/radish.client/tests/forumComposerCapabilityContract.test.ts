import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const readSource = (relativePath: string) => readFileSync(resolve(clientRoot, relativePath), 'utf8');

test('正式 Forum Compose 使用页面内共享发布器，WebOS 保留 Bottom Sheet 外壳', () => {
  const composer = readSource('src/apps/forum/components/ForumPostComposer.tsx');
  const modal = readSource('src/apps/forum/components/PublishPostModal.tsx');
  const publicCompose = readSource('src/public/forum/PublicForumCompose.tsx');

  assert.match(modal, /BottomSheet/);
  assert.match(modal, /ForumPostComposer/);
  assert.match(publicCompose, /<ForumPostComposer/);
  assert.match(publicCompose, /surface="page"/);
  assert.doesNotMatch(publicCompose, /PublishPostModal/);
  assert.doesNotMatch(composer, /BottomSheet/);
});

test('共享发布器只消费账号草稿 helper、双语资源和单一结构化失败反馈', () => {
  const composer = readSource('src/apps/forum/components/ForumPostComposer.tsx');
  const publicCompose = readSource('src/public/forum/PublicForumCompose.tsx');
  const workbench = readSource('src/workbench/WorkbenchApp.tsx');
  const compatibilityForm = readSource('src/apps/forum/components/PublishPostForm.tsx');

  assert.match(composer, /loadForumPostDraft/);
  assert.match(composer, /resolveForumPublishErrorMessage/);
  assert.doesNotMatch(composer, /forum_post_draft/);
  assert.doesNotMatch(composer, /[\u3400-\u9fff]/);
  assert.doesNotMatch(publicCompose, /toast\.error|error\.message/);
  assert.doesNotMatch(workbench, /forum_post_draft/);
  assert.doesNotMatch(compatibilityForm, /forum_post_draft/);
});
