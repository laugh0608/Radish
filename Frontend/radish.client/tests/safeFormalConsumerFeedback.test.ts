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
  assert.match(avatarUploadModalSource, /setError\(resolveAttachmentUploadErrorMessage\(error, failureFallback\)\)/);
  assert.doesNotMatch(avatarUploadModalSource, /error instanceof Error \? error\.message/);
  assert.doesNotMatch(avatarUploadModalSource, /json\.message/);
  assert.doesNotMatch(avatarUploadModalSource, /\bfetch\s*\(/);
  assert.match(avatarUploadModalSource, /await setMyAvatar\(String\(attachmentId\), t, requestController\.signal\)/);
  assert.match(avatarUploadModalSource, /await setMyAvatar\(null, t, requestController\.signal\)/);
  assert.match(avatarUploadModalSource, /type="button"[\s\S]*?onClick=\{handleRemoveAvatar\}/);
});

test('头像裁切与上传以同一生命周期锁定关闭并丢弃失效任务结果', () => {
  assert.match(avatarUploadModalSource, /const operationGenerationRef = useRef\(0\)/);
  assert.match(avatarUploadModalSource, /const uploadingRef = useRef\(false\)/);
  assert.match(avatarUploadModalSource, /const cropperProcessingRef = useRef\(false\)/);
  assert.match(
    avatarUploadModalSource,
    /const activeRequestControllerRef = useRef<AbortController \| null>\(null\)/,
  );
  assert.match(avatarUploadModalSource, /activeRequestControllerRef\.current\?\.abort\(\)/);
  assert.match(avatarUploadModalSource, /operationGenerationRef\.current \+= 1;[\s\S]*?activeRequestControllerRef\.current\?\.abort\(\)/);
  assert.match(avatarUploadModalSource, /mountedRef\.current = false;[\s\S]*?invalidateActiveOperation\(\)/);
  assert.match(avatarUploadModalSource, /const interactionLocked = uploading \|\| cropperProcessing;/);
  assert.match(avatarUploadModalSource, /closeDisabled=\{interactionLocked\}/);
  assert.match(avatarUploadModalSource, /closeOnOverlayClick=\{!interactionLocked\}/);
  assert.match(avatarUploadModalSource, /closeOnEscape=\{!interactionLocked\}/);
  assert.match(avatarUploadModalSource, /onProcessingChange=\{handleCropperProcessingChange\}/);
  assert.match(avatarUploadModalSource, /signal: requestController\.signal/);
  assert.match(
    avatarUploadModalSource,
    /if \(uploadingRef\.current \|\| cropperProcessingRef\.current\) \{[\s\S]*?return;[\s\S]*?\}/,
  );
  assert.match(
    avatarUploadModalSource,
    /await uploadImage\([\s\S]*?if \(!isOperationCurrent\(operationGeneration\)\) \{[\s\S]*?return;/,
  );
  assert.match(
    avatarUploadModalSource,
    /await setMyAvatar\([\s\S]*?if \(!isOperationCurrent\(operationGeneration\)\) \{[\s\S]*?return;/,
  );
  assert.match(
    avatarUploadModalSource,
    /if \(succeeded && isOperationCurrent\(operationGeneration\)\) \{[\s\S]*?onSuccess\(\);/,
  );
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
