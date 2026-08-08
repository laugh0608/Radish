import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('共享举报反馈应保留结构化错误，并且未知错误只展示宿主 fallback', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/contentModeration.ts'), 'utf8');
  const modalSource = readFileSync(resolve(clientRoot, 'src/components/ContentReportModal.tsx'), 'utf8');

  assert.match(apiSource, /createApiResponseError\(response, fallbackMessage\)/);
  assert.doesNotMatch(apiSource, /throw new Error/);
  assert.match(modalSource, /toast\.error\(t\('report\.submitFailed'\)\)/);
  assert.doesNotMatch(modalSource, /error instanceof Error \? error\.message/);
  assert.match(modalSource, /closeOnEscape=\{!submitting\}/);
  assert.match(modalSource, /closeOnOverlayClick=\{!submitting\}/);
});

test('聊天消息举报应把 Snowflake LongId 作为字符串传递到共享举报契约', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/contentModeration.ts'), 'utf8');
  const modalSource = readFileSync(resolve(clientRoot, 'src/components/ContentReportModal.tsx'), 'utf8');
  const chatSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const messageListSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatMessageList.tsx'), 'utf8');

  assert.doesNotMatch(messageListSource, /toNumericId|Number\(message\.voId\)/);
  assert.match(messageListSource, /onOpenReport\('ChatMessage', messageIdKey\)/);
  assert.match(chatSource, /reportTarget[\s\S]{0,120}targetId: string/);
  assert.match(chatSource, /handleOpenReport = useCallback\(\(targetType: ContentReportTargetType, targetId: string\)/);
  assert.match(modalSource, /targetId: number \| string/);
  assert.match(apiSource, /targetContentId: string/);
});

test('我的举报应只读取本人收件与精简结果，不暴露治理写动作', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/contentModeration.ts'), 'utf8');
  const reportsSource = readFileSync(resolve(clientRoot, 'src/me/MeReportsPage.tsx'), 'utf8');
  const zhAccountSource = readFileSync(resolve(clientRoot, 'src/locales/zh/account.ts'), 'utf8');
  const enAccountSource = readFileSync(resolve(clientRoot, 'src/locales/en/account.ts'), 'utf8');

  assert.match(apiSource, /ContentModeration\/GetMyReports/);
  assert.match(apiSource, /withAuth: true/);
  assert.match(reportsSource, /voReporterState/);
  assert.match(reportsSource, /voPublicResultCode/);
  assert.match(reportsSource, /voTargetNavigationStatus/);
  assert.doesNotMatch(reportsSource, /ReviewCase|CaptureEvidence|ApplyCorrectiveAction/);
  assert.doesNotMatch(reportsSource, /withdraw|appeal|attachment|moderatorChat/i);
  for (const reasonType of ['Spam', 'Abuse', 'Pornography', 'Illegal', 'Fraud', 'Other']) {
    assert.match(zhAccountSource, new RegExp(`me\\.reports\\.reasonType\\.${reasonType}`));
    assert.match(enAccountSource, new RegExp(`me\\.reports\\.reasonType\\.${reasonType}`));
  }
});

test('我的申诉应使用本人契约、受控深链与内存草稿，不暴露 Console 复核动作', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/contentModeration.ts'), 'utf8');
  const appealsSource = readFileSync(resolve(clientRoot, 'src/me/MeAppealsPage.tsx'), 'utf8');
  const navigationSource = readFileSync(resolve(clientRoot, 'src/utils/notificationNavigation.ts'), 'utf8');
  const zhAccountSource = readFileSync(resolve(clientRoot, 'src/locales/zh/account.ts'), 'utf8');
  const enAccountSource = readFileSync(resolve(clientRoot, 'src/locales/en/account.ts'), 'utf8');

  assert.match(apiSource, /ContentModeration\/GetMyAppealableDecisions/);
  assert.match(apiSource, /ContentModeration\/GetMyAppeals/);
  assert.match(apiSource, /ContentModeration\/SubmitAppeal/);
  assert.match(apiSource, /ContentModeration\/WithdrawAppeal/);
  assert.match(appealsSource, /useState\(''\)/);
  assert.match(appealsSource, /beforeunload/);
  assert.match(appealsSource, /voCanAppeal/);
  assert.match(appealsSource, /voTargetSnapshotSummary/);
  assert.match(appealsSource, /voUserActionSummaries/);
  assert.match(
    appealsSource,
    /voActionType === 'Restrict' && action\.voStatus === 'Succeeded'[\s\S]*OriginalRestrictionSucceeded/,
  );
  assert.doesNotMatch(appealsSource, /Date\.now\(\)/);
  assert.doesNotMatch(appealsSource, /voTargetContentId/);
  assert.match(navigationSource, /GovernanceDecision[\s\S]*\/me\/appeals/);
  assert.match(navigationSource, /GovernanceAppeal[\s\S]*\/me\/appeals/);
  assert.doesNotMatch(appealsSource, /ReviewAppeal|ExecuteAppealRelief|CaptureAppealEvidence/);
  for (const eventType of ['ReliefRequested', 'ReliefApplied', 'ReliefNoEffect']) {
    assert.match(zhAccountSource, new RegExp(`me\\.appeals\\.event\\.${eventType}`));
    assert.match(enAccountSource, new RegExp(`me\\.appeals\\.event\\.${eventType}`));
  }
});
