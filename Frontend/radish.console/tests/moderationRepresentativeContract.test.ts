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

test('R1-C02 应完整承接回答与商品评价筛选、双语及 Revision 前置校验', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const urlStateSource = readConsoleSource('src/pages/Moderation/moderationPageUrlState.ts');
  const zhSource = readConsoleSource('src/locales/zh/moderation.ts');
  const enSource = readConsoleSource('src/locales/en/moderation.ts');

  assert.match(urlStateSource, /'PostAnswer'/);
  assert.match(pageSource, /options=\{MODERATION_TARGET_TYPES\.map/);
  assert.match(pageSource, /\['Post', 'Comment', 'PostAnswer', 'Product', 'ProductReview'\]/);
  assert.match(zhSource, /'moderation\.targetType\.PostAnswer': '回答'/);
  assert.match(enSource, /'moderation\.targetType\.PostAnswer': 'Answer'/);
  assert.match(zhSource, /限制帖子、评论、回答、商品或商品评价前/);
  assert.match(enSource, /restricting a post, comment, answer, product, or product review/);
  assert.match(zhSource, /'moderation\.targetType\.ProductReview': '商品评价'/);
  assert.match(enSource, /'moderation\.targetType\.ProductReview': 'Product review'/);
});

test('R1-C02 案件选择应以 URL 为真相源并驱动 Mobile 全屏任务', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');
  const layoutSource = readConsoleSource('src/components/AdminLayout/AdminLayout.tsx');
  const layoutStyles = readConsoleSource('src/components/AdminLayout/AdminLayout.css');
  const styles = readConsoleSource('src/pages/Moderation/index.css');

  assert.match(pageSource, /parseModerationCasePublicId\(searchParams\.get\('case'\)\)/);
  assert.match(pageSource, /buildModerationSearchParams/);
  assert.match(pageSource, /data-console-fullscreen-task=\{selectedCaseId \? 'moderation-case'/);
  assert.match(appealSource, /data-console-fullscreen-task=\{selectedAppealId \? 'moderation-appeal'/);
  assert.match(layoutSource, /function isModerationDetailTask/);
  assert.match(layoutSource, /isConsoleMobileTask\(location\.search, activeMenuKey\)/);
  assert.match(layoutSource, /mobileTaskKeepsHeader/);
  assert.match(layoutSource, /mobileModerationHeader/);
  assert.match(layoutSource, /admin-layout--mobile-task-with-header/);
  assert.match(layoutSource, /admin-mobile-task-brand/);
  assert.match(layoutSource, /!mobileTaskActive && !mobileModerationHeader/);
  assert.match(
    layoutStyles,
    /admin-content--mobile-task-with-header[\s\S]*height: calc\(100dvh - 60px\)/,
  );
  assert.match(styles, /moderation-case-page\[data-task-active='true'\][\s\S]*height: 100%/);
  assert.match(styles, /moderation-case-task-header[\s\S]*position: sticky[\s\S]*min-height: 50px/);
});

test('R1-C02 正式实现应承接 PC 三段工作台与 Mobile 按需任务结构', () => {
  const pageSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');
  const styles = readConsoleSource('src/pages/Moderation/index.css');

  for (const source of [pageSource, appealSource]) {
    assert.match(source, /moderation-workspace-switch/);
    assert.match(source, /moderation-metric-strip/);
    assert.match(source, /moderation-mobile-toolbar/);
    assert.match(source, /<BottomSheet/);
  }

  assert.match(pageSource, /moderation-case-detail-grid/);
  assert.match(pageSource, /moderation-case-evidence-pane/);
  assert.match(pageSource, /moderation-case-decision-pane/);
  assert.match(pageSource, /moderation-case-mobile-actions/);
  assert.match(
    styles,
    /moderation-case-workbench[\s\S]*grid-template-columns: minmax\(280px, 330px\) minmax\(0, 1fr\)/,
  );
  assert.match(
    styles,
    /moderation-case-detail-grid[\s\S]*grid-template-columns: minmax\(360px, 1\.35fr\) minmax\(300px, 0\.95fr\)/,
  );
  assert.match(
    styles,
    /@media \(max-width: 768px\)[\s\S]*moderation-case-queue-item[\s\S]*grid-template-columns: minmax\(0, 1\.02fr\) minmax\(0, 1\.1fr\) minmax\(72px, 0\.72fr\)/,
  );
  assert.match(styles, /moderation-desktop-filter-bar[\s\S]*display: none/);
  assert.match(styles, /moderation-mobile-toolbar[\s\S]*display: flex/);
  assert.match(styles, /moderation-mobile-toolbar button\[aria-label\] svg[\s\S]*width: 14px[\s\S]*height: 14px/);
  assert.match(styles, /moderation-case-workbench[\s\S]*display: block[\s\S]*moderation-case-detail[\s\S]*display: none/);
  assert.match(
    styles,
    /moderation-case-page\[data-task-active='true'\] \.moderation-case-detail-grid[\s\S]*overflow: visible/,
  );
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
