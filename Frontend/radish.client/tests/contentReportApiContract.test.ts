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
