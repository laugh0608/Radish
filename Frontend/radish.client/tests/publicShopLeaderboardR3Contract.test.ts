import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientFile = (relativePath: string): string => readFileSync(resolve(clientRoot, relativePath), 'utf8');

const shopAppSource = readClientFile('src/public/shop/PublicShopApp.tsx');
const shopViewsSource = readClientFile('src/public/shop/PublicShopViews.tsx');
const shopStylesSource = readClientFile('src/public/shop/PublicShopApp.module.css');
const leaderboardSource = readClientFile('src/public/leaderboard/PublicLeaderboardApp.tsx');
const leaderboardStylesSource = readClientFile('src/public/leaderboard/PublicLeaderboardApp.module.css');
const themeTokensSource = readClientFile('src/theme/theme-tokens.css');
const zhCommerceSource = readClientFile('src/locales/zh/commerce.ts');
const enCommerceSource = readClientFile('src/locales/en/commerce.ts');

function sliceSource(source: string, startMarker: string, endMarker?: string): string {
  const start = source.indexOf(startMarker);
  assert.ok(start >= 0, `missing source marker: ${startMarker}`);
  const end = endMarker ? source.indexOf(endMarker, start + startMarker.length) : source.length;
  assert.ok(end >= 0, `missing source marker: ${endMarker}`);
  return source.slice(start, end);
}

test('R3 Shop 首页与列表应让商品主轴先于从属阅读区', () => {
  const homeSource = sliceSource(
    shopViewsSource,
    'export function PublicShopHomeView',
    'export function PublicShopProductsView',
  );
  const productsSource = sliceSource(
    shopViewsSource,
    'export function PublicShopProductsView',
    'export function PublicShopDetailView',
  );

  for (const source of [homeSource, productsSource]) {
    assert.ok(source.indexOf('className={styles.shopMain}') < source.indexOf('<PublicShopBrowseRail'));
  }

  assert.doesNotMatch(shopAppSource, /styles\.sectionActions|styles\.ghostButton/);
  assert.match(
    shopStylesSource,
    /@media \(max-width: 720px\)[\s\S]*?\.shopRail\s*\{[\s\S]*?display: flex;/,
  );
});

test('R3 Shop 浏览 CTA 应准确表达先进入详情再购买', () => {
  assert.match(zhCommerceSource, /'shop\.public\.rowDetailActionGuest': '查看详情后购买'/);
  assert.match(zhCommerceSource, /'shop\.public\.rowDetailActionSignedIn': '查看详情并购买'/);
  assert.match(enCommerceSource, /'shop\.public\.rowDetailActionGuest': 'View, then purchase'/);
  assert.match(enCommerceSource, /'shop\.public\.rowDetailActionSignedIn': 'View and purchase'/);
  assert.match(shopViewsSource, /getProductHref\(product\.voId\)/);
  assert.doesNotMatch(shopViewsSource, /getProductHref\(product\.voId\)[\s\S]{0,120}intent.*purchase/);
});

test('R3 Leaderboard 应只保留一处类型切换并让榜单主轴先于辅助栏', () => {
  const railSource = sliceSource(
    leaderboardSource,
    'function PublicLeaderboardRail',
    'export const PublicLeaderboardApp',
  );
  const appSource = sliceSource(leaderboardSource, 'export const PublicLeaderboardApp');

  assert.doesNotMatch(railSource, /types\.map|railJumpTitle|railRouteTitle/);
  assert.match(railSource, /railStateTitle/);
  assert.match(railSource, /<PublicReadingGuide/);
  assert.ok(appSource.indexOf('<section className={styles.sectionCard}>') < appSource.lastIndexOf('<PublicLeaderboardRail'));
  assert.equal(appSource.match(/types\.map/g)?.length, 1);
  assert.match(
    leaderboardStylesSource,
    /@media \(max-width: 720px\)[\s\S]*?\.leaderboardRail\s*\{[\s\S]*?display: flex;/,
  );
});

test('R3 Leaderboard 应以本地语言覆盖后端类型展示文案', () => {
  const localizeTypeSource = sliceSource(
    leaderboardSource,
    'function localizePublicLeaderboardType',
    'function buildVisiblePages',
  );

  assert.match(localizeTypeSource, /voName: localizedType\.voName/);
  assert.match(localizeTypeSource, /voDescription: localizedType\.voDescription/);
  assert.match(localizeTypeSource, /voPrimaryLabel: localizedType\.voPrimaryLabel/);
  assert.match(leaderboardSource, /const localizedType = localizePublicLeaderboardType\(type, fallbackTypes\)/);
  assert.equal(leaderboardSource.match(/item\.voPrimaryLabel \|\| activeTypeConfig\.voPrimaryLabel/g)?.length ?? 0, 0);
});

test('R3 榜单奖牌应消费四主题语义 surface 且页面样式不再硬编码颜色', () => {
  const tokenNames = [
    '--theme-rank-first-surface',
    '--theme-rank-second-surface',
    '--theme-rank-third-surface',
  ];

  for (const tokenName of tokenNames) {
    assert.equal(themeTokensSource.match(new RegExp(`${tokenName}:`, 'g'))?.length, 4);
    assert.match(leaderboardStylesSource, new RegExp(`background: var\\(${tokenName}\\);`));
  }

  assert.doesNotMatch(`${shopStylesSource}\n${leaderboardStylesSource}`, /#[0-9a-fA-F]{3,8}\b/);
});
