import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDir, '..');

test('robots.txt 应开放公开入口并指向公开 sitemap', () => {
  const robots = readFileSync(resolve(clientRoot, 'public/robots.txt'), 'utf8');

  assert.match(robots, /^User-agent: \*/m);
  assert.match(robots, /^Allow: \/discover$/m);
  assert.match(robots, /^Allow: \/forum$/m);
  assert.match(robots, /^Allow: \/docs$/m);
  assert.match(robots, /^Allow: \/u\/$/m);
  assert.match(robots, /^Disallow: \/connect\/$/m);
  assert.match(robots, /^Sitemap: https:\/\/radishx\.com\/sitemap\.xml$/m);
});

test('sitemap.xml 应提供第一批公开入口 seed', () => {
  const sitemap = readFileSync(resolve(clientRoot, 'public/sitemap.xml'), 'utf8');

  assert.match(sitemap, /<urlset xmlns="http:\/\/www\.sitemaps\.org\/schemas\/sitemap\/0\.9">/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/discover<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/forum<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/docs<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/shop\/products<\/loc>/);
});

test('公开商城详情购买回流入口应指向保留工作台路径', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopApp.tsx'), 'utf8');

  assert.match(source, /import \{ buildDesktopShopProductReturnPath \} from '@\/services\/authReturnPath';/);
  assert.match(source, /const desktopProductEntryUrl = buildDesktopShopProductReturnPath\(selectedProduct\.voId\);/);
  assert.match(source, /href=\{desktopProductEntryUrl\}/);
  assert.doesNotMatch(source, /function buildDesktopProductEntryUrl/);
  assert.doesNotMatch(source, /className=\{styles\.primaryLink\} href="\/"/);
});
