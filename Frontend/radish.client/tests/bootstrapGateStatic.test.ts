import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('所有浏览器入口应在 BrowserAppRouter 顶层统一经过初始化门禁', () => {
  const browserRouterSource = readFileSync(resolve(clientRoot, 'src/bootstrap/BrowserAppRouter.tsx'), 'utf8');

  assert.match(browserRouterSource, /import \{ BootstrapGate \} from '@\/bootstrap\/BootstrapGate';/);
  assert.match(
    browserRouterSource,
    /const Page = resolveEntryComponent\(entryKind\);\s*return \(\s*<BootstrapGate>\s*<BrowserNavigationLockContext\.Provider value=\{updateNavigationLock\}>\s*<Page \/>\s*<\/BrowserNavigationLockContext\.Provider>\s*<\/BootstrapGate>\s*\);/,
  );
});

test('入口组件不应重复承担初始化门禁', () => {
  const entryPaths = [
    'src/circle/CircleEntry.tsx',
    'src/desktop/RootEntry.tsx',
    'src/docs/DocsAuthorEntry.tsx',
    'src/me/MeEntry.tsx',
    'src/messages/MessagesEntry.tsx',
    'src/notifications/NotificationsEntry.tsx',
    'src/pet/PetEntry.tsx',
    'src/public/PublicEntry.tsx',
    'src/shop/ShopEntry.tsx',
    'src/workbench/WorkbenchEntry.tsx',
    'src/auth/OidcCallbackPage.tsx',
  ];

  for (const entryPath of entryPaths) {
    const source = readFileSync(resolve(clientRoot, entryPath), 'utf8');
    assert.doesNotMatch(source, /BootstrapGate/, entryPath);
  }
});
