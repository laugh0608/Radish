import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDirectory, '..');
const readSource = (relativePath: string) => readFileSync(resolve(consoleRoot, relativePath), 'utf8');

test('Console 页面缺权应保留目标地址并明确提供可用入口', () => {
  const source = readSource('src/components/PermissionGuard/RouteGuard.tsx');

  assert.match(source, /function RoutePermissionDenied/);
  assert.match(source, /routeTitle=\{routeTitle\}/);
  assert.match(source, /navigate\(fallbackPath, \{ replace: true \}\)/);
  assert.match(source, /console\.guard\.deniedDescription/);
  assert.doesNotMatch(source, /<Navigate to=\{fallbackPath\} replace \/>/);
});

test('Console Not Found 应按身份拆分动作，匿名状态不得展示后台搜索', () => {
  const source = readSource('src/components/NotFound/NotFound.tsx');

  assert.match(source, /user && !canEnterConsole\(user\)/);
  assert.match(source, /return <ConsoleAccessDenied \/>/);
  assert.match(source, /const authorized = canEnterConsole\(user\)/);
  assert.match(source, /console\.notFound\.anonymousDescription/);
  assert.match(source, /console\.notFound\.authorizedDescription/);
  assert.match(source, /authorized\s*\? <GlobalSearch/);
  assert.match(source, /console\.notFound\.login/);
});

test('Console 路由错误和根级错误都应由显式运行时边界承接', () => {
  const routerSource = readSource('src/router/index.tsx');
  const routeErrorSource = readSource('src/components/RouteRuntimeError/RouteRuntimeError.tsx');
  const rootErrorSource = readSource('src/components/ErrorBoundary/ErrorBoundary.tsx');

  assert.equal(routerSource.match(/errorElement: createElement\(RouteRuntimeError\)/g)?.length, 4);
  assert.match(routeErrorSource, /useRouteError\(\)/);
  assert.match(routeErrorSource, /diagnosticId/);
  assert.match(routeErrorSource, /role="alert"/);
  assert.match(routeErrorSource, /window\.location\.reload\(\)/);
  assert.match(rootErrorSource, /componentDidCatch/);
  assert.match(rootErrorSource, /console-error-boundary/);
});

test('Console 权限、Not Found 与错误文案应同时覆盖中英文', () => {
  for (const localePath of ['src/locales/zh/shell.ts', 'src/locales/en/shell.ts']) {
    const source = readSource(localePath);
    assert.match(source, /'console\.guard\.deniedTitle'/);
    assert.match(source, /'console\.notFound\.anonymousDescription'/);
    assert.match(source, /'console\.notFound\.authorizedDescription'/);
    assert.match(source, /'console\.errorBoundary\.descriptionWithId'/);
  }
});
