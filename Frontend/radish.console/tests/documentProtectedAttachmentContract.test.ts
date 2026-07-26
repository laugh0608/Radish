import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const pageSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/pages/Documents/DocumentGovernancePage.tsx'),
  'utf8',
);

test('Console 文档详情、审核和 Revision 共用认证附件加载', () => {
  assert.match(pageSource, /loadAttachmentAssetBlob/);
  assert.match(pageSource, /scopeKey:\s*\[[\s\S]*'console-reviewer'[\s\S]*\]\.join\(':'\)/);
  assert.ok((pageSource.match(/protectedAttachments=\{protectedAttachments\}/g) ?? []).length >= 5);
  assert.doesNotMatch(pageSource, /fetch\(|axios|createObjectURL\(.*attachment/);
});

test('Console 审核同时渲染正式正文、草稿与封面受保护资源', () => {
  assert.match(pageSource, /reviewOfficialDocument\?\.voCoverAttachmentId/);
  assert.match(pageSource, /reviewDraft\.voCoverAttachmentId/);
  assert.match(pageSource, /reviewOfficialDocument\?\.voMarkdownContent/);
  assert.match(pageSource, /reviewDraft\.voMarkdownContent/);
});
