import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('初始化门禁应由宿主词元驱动并保留用户输入原文', () => {
  const gateSource = readFileSync(resolve(clientRoot, 'src/bootstrap/BootstrapGate.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/bootstrap/BootstrapGate.module.css'), 'utf8');

  assert.match(gateSource, /const \{ t, i18n \} = useTranslation\(\);/);
  assert.match(gateSource, /getBootstrapStatus\(i18n\.t\('bootstrap\.error\.statusFallback'\)\)/);
  assert.match(gateSource, /\}, \[i18n\]\);/);
  assert.match(gateSource, /\{t\('bootstrap\.error\.statusFallback'\)\}/);
  assert.match(gateSource, /err\.messageKey\?\.startsWith\('error\.bootstrap\.'\)/);
  assert.match(gateSource, /i18n\.exists\(err\.messageKey\)/);
  assert.match(gateSource, /\{messageKey && <p className=\{styles\.error\}>\{t\(messageKey\)\}<\/p>\}/);
  assert.doesNotMatch(gateSource, /err instanceof Error \? err\.message/);
  assert.match(gateSource, /bootstrap\.documentTitle\.required/);
  assert.match(gateSource, /bootstrap\.completed\.description/);
  assert.match(gateSource, /displayName: createdAdmin\.displayName/);
  assert.match(gateSource, /email: createdAdmin\.email/);
  assert.match(stylesSource, /overflow-wrap: anywhere;/);
});

test('初始化 API 应抛出保留状态与诊断字段的 ApiResponseError', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/bootstrap.ts'), 'utf8');

  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /fallbackMessage: string/);
  assert.doesNotMatch(apiSource, /throw new Error/);
});
