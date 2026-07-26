import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const rendererSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/MarkdownRenderer/MarkdownRenderer.tsx'),
  'utf8',
);
const lifecycleSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/MarkdownRenderer/useProtectedMarkdownAttachments.ts'),
  'utf8',
);
const editorSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/MarkdownEditor/MarkdownEditor.tsx'),
  'utf8',
);

test('MarkdownRenderer 只通过宿主注入契约加载受保护附件', () => {
  assert.match(rendererSource, /protectedAttachments\?: ProtectedMarkdownAttachmentOptions/);
  assert.match(lifecycleSource, /loadBlob: ProtectedMarkdownAttachmentBlobLoader/);
  assert.doesNotMatch(rendererSource, /apiFetch|fetch\(|Authorization|access_token/);
  assert.doesNotMatch(lifecycleSource, /apiFetch|fetch\(|Authorization|access_token/);
});

test('受保护附件生命周期覆盖 Abort、代次隔离、重试和 object URL 回收', () => {
  assert.match(lifecycleSource, /scopeKey/);
  assert.match(lifecycleSource, /new AbortController\(\)/);
  assert.match(lifecycleSource, /current\.signature !== signature/);
  assert.match(lifecycleSource, /URL\.createObjectURL\(blob\)/);
  assert.match(lifecycleSource, /URL\.revokeObjectURL\(objectUrl\)/);
  assert.match(rendererSource, /setAttachmentReloadToken\(\(current\) => current \+ 1\)/);
});

test('图片、灯箱和普通文件均消费认证结果并保留键盘语义', () => {
  assert.match(rendererSource, /download=""/);
  assert.match(rendererSource, /role=\{enableImageLightbox \? 'button' : undefined\}/);
  assert.match(rendererSource, /event\.key !== 'Enter' && event\.key !== ' '/);
  assert.ok((rendererSource.match(/onKeyDown=/g) ?? []).length >= 2);
  assert.match(rendererSource, /labels\.lightboxClose/);
  assert.match(editorSource, /protectedAttachments=\{protectedAttachments\}/);
});
