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
