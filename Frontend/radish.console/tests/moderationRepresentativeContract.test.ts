import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDirectory, '..');

function readConsoleSource(relativePath: string): string {
  return readFileSync(resolve(consoleRoot, relativePath), 'utf8');
}

test('R1-C02 应完整承接 PostAnswer 筛选、双语与 Revision 前置校验', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const urlStateSource = readConsoleSource('src/pages/Moderation/moderationPageUrlState.ts');
  const zhSource = readConsoleSource('src/locales/zh/moderation.ts');
  const enSource = readConsoleSource('src/locales/en/moderation.ts');

  assert.match(urlStateSource, /'PostAnswer'/);
  assert.match(pageSource, /options=\{MODERATION_TARGET_TYPES\.map/);
  assert.match(pageSource, /\['Post', 'Comment', 'PostAnswer', 'Product'\]/);
  assert.match(zhSource, /'moderation\.targetType\.PostAnswer': '回答'/);
  assert.match(enSource, /'moderation\.targetType\.PostAnswer': 'Answer'/);
  assert.match(zhSource, /限制帖子、评论、回答或商品前/);
  assert.match(enSource, /restricting a post, comment, answer, or product/);
});

test('R1-C02 案件选择应以 URL 为真相源并驱动 Mobile 全屏任务', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');
  const layoutSource = readConsoleSource('src/components/AdminLayout/AdminLayout.tsx');
  const styles = readConsoleSource('src/pages/Moderation/index.css');

  assert.match(pageSource, /parseModerationCasePublicId\(searchParams\.get\('case'\)\)/);
  assert.match(pageSource, /buildModerationSearchParams/);
  assert.match(pageSource, /data-console-fullscreen-task=\{selectedCaseId \? 'moderation-case'/);
  assert.match(appealSource, /data-console-fullscreen-task=\{selectedAppealId \? 'moderation-appeal'/);
  assert.match(layoutSource, /function isModerationDetailTask/);
  assert.match(layoutSource, /isConsoleMobileTask\(location\.search, activeMenuKey\)/);
  assert.match(styles, /moderation-case-page\[data-task-active='true'\][\s\S]*height: 100dvh/);
  assert.match(styles, /moderation-case-task-header[\s\S]*position: sticky/);
});

test('R1-C02 权威读取失败应区分 stale 与 unavailable 并冻结写入', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');

  for (const source of [pageSource, appealSource]) {
    assert.match(source, /isApiResponseNotFoundError/);
    assert.match(source, /setQueueReadState\('stale'\)/);
    assert.match(source, /setDetailReadState\('unavailable'\)/);
    assert.match(source, /setDetailReadState\('stale'\)/);
    assert.match(source, /actionsAreAuthoritative/);
  }

  assert.match(pageSource, /queueReadState === 'ready' && detailReadState === 'ready'/);
  assert.match(appealSource, /queueReadState === 'ready'[\s\S]*detailReadState === 'ready'/);
});

test('R1-C02 View-only Operator 不应渲染可编辑案件决定表面', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');

  assert.match(pageSource, /!canReview && !canAction/);
  assert.match(pageSource, /moderation\.case\.readOnlyDescription/);
  assert.match(
    pageSource,
    /canReview && actionsAreAuthoritative && detail\.voCase\.voStatus !== 'Resolved'/,
  );
  assert.match(
    pageSource,
    /detail\.voCase\.voStatus === 'Resolved' && canAction && actionsAreAuthoritative/,
  );
  assert.match(appealSource, /!canAppeal \? \(/);
  assert.match(appealSource, /moderation\.appeal\.redactedDescription/);
  assert.match(
    appealSource,
    /!canAppeal[\s\S]*selectedAppealId[\s\S]*queueReadState !== 'loading'[\s\S]*!selectedSummary/,
  );
});
