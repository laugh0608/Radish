import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readSource = (relativePath: string) => readFileSync(resolve(clientRoot, relativePath), 'utf8');

test('未知 Client 路由应进入独立 Not Found，只有明确 desktop 地址进入历史桌面', () => {
  const entryRouteSource = readSource('src/bootstrap/entryRoute.ts');
  const routerSource = readSource('src/bootstrap/BrowserAppRouter.tsx');
  const notFoundSource = readSource('src/boundary/NotFoundEntry.tsx');

  assert.match(entryRouteSource, /pathname === TAURI_DESKTOP_ENTRY_PATH/);
  assert.match(entryRouteSource, /return 'not-found';/);
  assert.match(routerSource, /case 'desktop':\s*return RootEntry;/);
  assert.match(routerSource, /case 'not-found':\s*return NotFoundEntry;/);
  assert.match(notFoundSource, /tone="notFound"/);
  assert.match(notFoundSource, /href: '\/discover'/);
  assert.match(notFoundSource, /href: '\/workbench'/);
  assert.match(notFoundSource, /window\.location\.pathname/);
  assert.doesNotMatch(notFoundSource, /window\.location\.(search|hash)/);
});

test('Client 根级运行时错误应提供诊断编号和恢复动作', () => {
  const mainSource = readSource('src/main.tsx');
  const errorBoundarySource = readSource('src/boundary/ClientErrorBoundary.tsx');
  const stateSlotSource = readSource('src/components/web-shell/WebStateSlot.tsx');

  assert.match(mainSource, /<ClientErrorBoundary>/);
  assert.match(mainSource, /<BrowserAppRouter \/>/);
  assert.match(errorBoundarySource, /componentDidCatch/);
  assert.match(errorBoundarySource, /diagnosticId/);
  assert.match(errorBoundarySource, /tone="error"/);
  assert.match(errorBoundarySource, /boundary\.runtime\.retry/);
  assert.match(errorBoundarySource, /window\.location\.reload\(\)/);
  assert.match(errorBoundarySource, /window\.location\.assign\('\/discover'\)/);
  assert.match(stateSlotSource, /role=\{tone === 'error' \? 'alert' : undefined\}/);
});

test('Client 错误与 Not Found 文案应同时覆盖中英文', () => {
  for (const localePath of ['src/locales/zh/shell.ts', 'src/locales/en/shell.ts']) {
    const source = readSource(localePath);
    assert.match(source, /'boundary\.notFound\.title'/);
    assert.match(source, /'boundary\.notFound\.path'/);
    assert.match(source, /'boundary\.runtime\.title'/);
    assert.match(source, /'boundary\.runtime\.diagnostic'/);
  }
});
