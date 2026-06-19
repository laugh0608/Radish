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
  assert.match(source, /const desktopProductEntryUrl = buildDesktopShopProductReturnPath\(selectedProduct\.voId, \{ intent: 'purchase' \}\);/);
  assert.match(source, /href=\{desktopProductEntryUrl\}/);
  assert.doesNotMatch(source, /function buildDesktopProductEntryUrl/);
  assert.doesNotMatch(source, /className=\{styles\.primaryLink\} href="\/"/);
});

test('公开个人页应保持只读边界，不直接暴露关注写操作', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');

  assert.doesNotMatch(source, /from '@\/api\/userFollow'/);
  assert.doesNotMatch(source, /followUser\(/);
  assert.doesNotMatch(source, /unfollowUser\(/);
  assert.doesNotMatch(source, /getFollowStatus\(/);
});

test('公开个人页内容查询不应依赖内部用户 ID', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');

  assert.doesNotMatch(source, /voUserId/);
  assert.doesNotMatch(source, /GetUserStats/);
  assert.doesNotMatch(source, /GetUserPosts/);
  assert.doesNotMatch(source, /GetUserComments/);
  assert.match(source, /getPublicUserStats\(route\.userId\)/);
  assert.match(source, /getPublicUserPosts\(profileRouteIdentifier, route\.page, 10\)/);
  assert.match(source, /getPublicUserComments\(profileRouteIdentifier, route\.page, 10\)/);
});

test('公开社区发现页应使用统一公开分享入口', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');

  assert.match(source, /usePublicShareLink/);
  assert.match(source, /buildPublicShareUrl\(buildPublicDiscoverPath\(route\)\)/);
  assert.match(source, /discover\.public\.shareAction/);
});

test('公开入口应为所有公开路由应用通用 JSON-LD', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');

  assert.match(source, /applyPublicStructuredData/);
  assert.match(source, /buildPublicRouteStructuredData\(route\)/);
  assert.match(source, /return removePublicStructuredData/);
});

test('公开论坛详情加载后应刷新详情 head 并复用同一个 canonical', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');

  assert.match(source, /applyPublicHead/);
  assert.match(source, /buildForumPostPublicHead/);
  assert.match(source, /const postHead = buildForumPostPublicHead\(post, commentId, coverImageUrl\);/);
  assert.match(source, /applyPublicHead\(postHead\);/);
  assert.match(source, /canonicalPath: postHead\.canonicalPath/);
});

test('登录态私域入口生成公开链接前应复用 PublicId 校验', () => {
  const circleSource = readFileSync(resolve(clientRoot, 'src/circle/CircleApp.tsx'), 'utf8');
  const meSource = readFileSync(resolve(clientRoot, 'src/me/MeApp.tsx'), 'utf8');
  const leaderboardSource = readFileSync(resolve(clientRoot, 'src/public/leaderboard/PublicLeaderboardApp.tsx'), 'utf8');

  assert.match(circleSource, /resolvePublicPostRouteIdentifier/);
  assert.match(circleSource, /resolvePublicUserRouteIdentifier/);
  assert.doesNotMatch(circleSource, /voPublicId\?\.trim\(\)/);
  assert.match(meSource, /resolvePublicUserRouteIdentifier/);
  assert.match(meSource, /normalizePublicUserId/);
  assert.doesNotMatch(meSource, /voPublicId\?\.trim\(\)/);
  assert.match(leaderboardSource, /resolvePublicUserRouteIdentifier/);
  assert.doesNotMatch(leaderboardSource, /voUserPublicId\?\.trim\(\)/);
});
