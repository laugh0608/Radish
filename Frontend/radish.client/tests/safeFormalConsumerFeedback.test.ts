import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const avatarUploadModalSource = readFileSync(
  new URL('../src/apps/profile/components/AvatarUploadModal.tsx', import.meta.url),
  'utf8',
);
const publishPostModalSource = readFileSync(
  new URL('../src/apps/forum/components/PublishPostModal.tsx', import.meta.url),
  'utf8',
);
const userAttachmentListSource = readFileSync(
  new URL('../src/apps/profile/components/UserAttachmentList.tsx', import.meta.url),
  'utf8',
);

test('头像上传仅展示稳定附件错误或本地化 fallback', () => {
  assert.match(avatarUploadModalSource, /resolveAttachmentUploadErrorMessage\(error, failureFallback\)/);
  assert.match(avatarUploadModalSource, /setError\(t\('profile\.avatar\.removeFailed'\)\)/);
  assert.doesNotMatch(avatarUploadModalSource, /error instanceof Error \? error\.message/);
  assert.doesNotMatch(avatarUploadModalSource, /json\.message/);
  assert.match(avatarUploadModalSource, /type="button"[\s\S]*?onClick=\{handleRemoveAvatar\}/);
});

test('论坛发布仅展示稳定本地化错误或宿主 fallback', () => {
  assert.match(
    publishPostModalSource,
    /resolveForumPublishErrorMessage\(error, t, t\('forum\.publishFailed'\)\)/,
  );
  assert.doesNotMatch(publishPostModalSource, /error instanceof Error && error\.message/);
  assert.doesNotMatch(publishPostModalSource, /typeof error === 'string'/);
  assert.match(publishPostModalSource, /t\('forum\.publishDraftRetainedHint'\)/);
  assert.match(publishPostModalSource, /toast\.error\(t\('forum\.publishDraftRetainedToast'\)\)/);
});

test('附件列表未知加载与删除错误仅展示宿主 fallback', () => {
  assert.match(userAttachmentListSource, /setErrorKey\('profile\.attachments\.loadFailed'\)/);
  assert.match(userAttachmentListSource, /setErrorKey\('attachment\.api\.deleteFailed'\)/);
  assert.match(userAttachmentListSource, /\{t\(errorKey\)\}/);
  assert.doesNotMatch(userAttachmentListSource, /e instanceof Error \? e\.message|String\(e\)/);
});
