import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const clientRoot = path.resolve(testDirectory, '..');

const readSource = (relativePath: string): string => fs.readFileSync(
  path.resolve(clientRoot, relativePath),
  'utf8',
);

test('Author 列表筛选与翻页由后端查询契约驱动', () => {
  const apiSource = readSource('src/apps/wiki/api/wiki.ts');
  const appSource = readSource('src/docs/DocsAuthorApp.tsx');
  const mineSource = readSource('src/docs/DocsMinePage.tsx');
  const styles = readSource('src/docs/DocsAuthorApp.module.css');

  assert.match(apiSource, /new URLSearchParams\(\{/);
  assert.match(apiSource, /scope: query\.scope/);
  assert.match(apiSource, /draftStage: query\.draftStage/);
  assert.match(apiSource, /pageIndex: String\(query\.pageIndex\)/);
  assert.match(apiSource, /pageSize: String\(query\.pageSize\)/);
  assert.doesNotMatch(apiSource, /pageSize=100|pageSize:\s*100/);

  assert.match(appSource, /const authorListQueryRef = useRef<WikiAuthorListQuery>/);
  assert.match(appSource, /const authorListEpochRef = useRef\(0\)/);
  assert.match(appSource, /requestEpoch !== authorListEpochRef\.current/);
  assert.match(mineSource, /scope: event\.target\.value as WikiAuthorDocumentScope/);
  assert.match(mineSource, /draftStage: event\.target\.value as WikiAuthorDraftStage/);
  assert.match(mineSource, /updateQuery\(\{ pageIndex: state\.page - 1 \}\)/);
  assert.match(mineSource, /updateQuery\(\{ pageIndex: state\.page \+ 1 \}\)/);
  assert.match(mineSource, /getDocsAuthorRoleText\(previewDocument\.voAuthorRole, t\)/);
  assert.match(mineSource, /getDocsAuthorRoleText\(document\.voAuthorRole, t\)/);
  assert.match(mineSource, /state\.error && hasDocuments/);
  assert.match(mineSource, /state\.error && !hasDocuments/);
  assert.match(styles, /@media \(max-width: 860px\)[\s\S]*\.documentRow,[\s\S]*\.documentRowSelected \{[\s\S]*grid-template-columns: 1fr;/);
});

test('Revision 历史与详情分别隔离并发响应并保留可辨识失败状态', () => {
  const appSource = readSource('src/docs/DocsAuthorApp.tsx');
  const revisionsSource = readSource('src/docs/DocsRevisionsPage.tsx');

  assert.match(appSource, /const revisionHistoryEpochRef = useRef\(0\)/);
  assert.match(appSource, /const revisionDetailEpochRef = useRef\(0\)/);
  assert.match(appSource, /requestEpoch !== revisionHistoryEpochRef\.current/);
  assert.match(appSource, /requestEpoch !== revisionDetailEpochRef\.current/);
  assert.match(appSource, /historyError: getErrorMessage\(/);
  assert.match(appSource, /detailError: getErrorMessage\(/);
  assert.match(appSource, /detailStale: Boolean\(/);
  assert.match(revisionsSource, /state\.historyError && state\.revisions\.length === 0/);
  assert.match(revisionsSource, /state\.detailError && state\.selectedRevision/);
  assert.match(revisionsSource, /state\.detailError && !state\.selectedRevision/);
  assert.match(appSource, /const revisionComparisonEpochRef = useRef\(0\)/);
  assert.match(appSource, /requestEpoch !== revisionComparisonEpochRef\.current/);
  assert.match(appSource, /comparisonError: getErrorMessage\(/);
  assert.match(revisionsSource, /<ContentSnapshotDiff/);
  assert.match(revisionsSource, /comparisonMode === 'previous'/);
  assert.match(revisionsSource, /onComparisonModeChange\('current'\)/);
  assert.match(revisionsSource, /comparisonMissing/);
});
