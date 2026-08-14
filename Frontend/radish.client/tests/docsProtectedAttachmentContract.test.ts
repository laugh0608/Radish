import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const readClientSource = (relativePath: string): string => fs.readFileSync(
  path.resolve(testDirectory, '../src', relativePath),
  'utf8',
);

const optionsSource = readClientSource('docs/docsProtectedAttachments.ts');
const authorSource = readClientSource('docs/DocsAuthorApp.tsx');
const editorSource = readClientSource('docs/DocsAuthorEditorPage.tsx');
const editorContextSource = readClientSource('docs/DocsAuthorEditorContext.tsx');
const publicDocsSource = readClientSource('public/docs/PublicDocsDetail.tsx');

test('Docs 宿主通过统一 HTTP Blob 契约注入认证附件能力', () => {
  assert.match(optionsSource, /loadAttachmentAssetBlob/);
  assert.match(optionsSource, /scopeKey/);
  assert.match(optionsSource, /wiki\.attachments\.loadFailed/);
  assert.doesNotMatch(optionsSource, /fetch\(|axios|createObjectURL/);
});

test('Author 编辑预览与 Revision 使用同一受保护附件契约', () => {
  assert.match(authorSource, /createDocsProtectedAttachmentOptions/);
  assert.match(authorSource, /protectedAttachments=\{protectedAttachments\}/);
  assert.match(editorSource, /<MarkdownEditor[\s\S]*protectedAttachments=\{protectedAttachments\}/);
  assert.match(editorSource, /<DocsAuthorEditorContext[\s\S]*protectedAttachments=\{protectedAttachments\}/);
  assert.match(editorContextSource, /wiki\.author\.form\.coverPreview/);
  assert.match(editorContextSource, /<MarkdownRenderer[\s\S]*protectedAttachments=\{protectedAttachments\}/);
});

test('公开 Docs 只对非 Public 文档启用认证资源加载', () => {
  assert.match(
    publicDocsSource,
    /documentDetail\.voVisibility === WikiDocumentVisibility\.Public[\s\S]*\? undefined[\s\S]*: protectedAttachments/,
  );
  assert.match(publicDocsSource, /voCoverAttachmentId/);
  assert.match(publicDocsSource, /resolveLinkHref=\{resolveArticleLinkHref\}/);
  assert.match(
    publicDocsSource,
    /documentDetail\.voVisibility !== WikiDocumentVisibility\.Public[\s\S]*return null/,
  );
});
