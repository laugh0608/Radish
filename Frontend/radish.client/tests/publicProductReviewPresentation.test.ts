import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

function readClientSource(relativePath: string): string {
  return readFileSync(resolve(clientRoot, relativePath), 'utf8');
}

test('正式商品详情消费评价聚合、稳定分页与已购本人状态', () => {
  const hookSource = readClientSource('src/public/shop/usePublicProductReviews.ts');
  const viewSource = readClientSource('src/public/shop/PublicProductReviews.tsx');

  assert.match(hookSource, /getProductReviews\(productId, t, pageIndex, REVIEW_PAGE_SIZE\)/);
  assert.match(hookSource, /getMyProductReview\(productId, t\)/);
  assert.match(hookSource, /expectedVersion: myReviewState\.voExpectedVersion/);
  assert.match(hookSource, /ProductReviewErrorCode\.VersionConflict/);
  assert.match(hookSource, /ProductReviewErrorCode\.PurchaseRequired/);
  assert.match(hookSource, /ProductReviewErrorCode\.Moderated/);
  assert.match(hookSource, /setVersionConflict\(conflict\)/);
  assert.match(hookSource, /conflict\s*\? null/);
  assert.match(hookSource, /setMutationError/);
  assert.match(hookSource, /reviewPageRef\.current\?\.voPageIndex !== pageIndex/);
  assert.match(hookSource, /mutationBusy \|\| myReviewLoading/);
  assert.doesNotMatch(hookSource, /Number\(productId\)|Number\(review\.voId\)/);

  assert.match(viewSource, /summary\.voAverageRating\.toFixed\(1\)/);
  assert.match(viewSource, /summary\.voFiveStarCount/);
  assert.match(viewSource, /controller\.totalPages > 1/);
  assert.match(hookSource, /shop\.review\.moderated/);
  assert.match(hookSource, /shop\.review\.purchaseRequired/);
  assert.match(viewSource, /controller\.myReviewUnavailableMessage/);
  assert.match(viewSource, /buildShopProductReviewReturnPath\(productId\)/);
  assert.match(viewSource, /controller\.openEditor\(\)/);
  assert.doesNotMatch(viewSource, /controller\.myReviewState\.voUnavailableReason/);
  assert.match(viewSource, /onReportReview\(review\.voId\)/);
  assert.match(viewSource, /shop\.review\.zeroDescription/);
  assert.match(viewSource, /controller\.reviewsStale \? t\('shop\.review\.staleDescription'\) : t\('shop\.review\.loadFailed'\)/);
  assert.doesNotMatch(viewSource, /controller\.reviewsStale \? t\('shop\.review\.staleDescription'\) : controller\.reviewsError/);
});

test('评价编辑保留 dirty 与 CAS 草稿并由 Public 路由确认离开', () => {
  const hookSource = readClientSource('src/public/shop/usePublicProductReviews.ts');
  const entrySource = readClientSource('src/public/PublicEntry.tsx');
  const appSource = readClientSource('src/public/shop/PublicShopApp.tsx');

  assert.match(hookSource, /const dirty = editorOpen && !draftsEqual\(draft, baselineDraft\)/);
  assert.match(hookSource, /window\.confirm\(t\('shop\.review\.leaveConfirm'\)\)/);
  assert.match(hookSource, /onDirtyChange\?\.\(dirty\)/);
  assert.match(appSource, /onNavigationConfirmChange\(dirty \? t\('shop\.review\.leaveConfirm'\) : null\)/);
  assert.match(entrySource, /publicNavigationConfirmMessage && !window\.confirm\(publicNavigationConfirmMessage\)/);
  assert.match(entrySource, /publicHistoryRestoreRef\.current = \{/);
  assert.match(entrySource, /window\.addEventListener\('beforeunload', handleBeforeUnload\)/);
});

test('商品 Mobile 顺序保持图片、信息、购买、评价、详情且价格使用萝卜图标', () => {
  const source = readClientSource('src/public/shop/PublicShopViews.tsx');
  const styles = readClientSource('src/public/shop/PublicShopApp.module.css');
  const imageIndex = source.indexOf('className={styles.detailImageShell}');
  const bodyIndex = source.indexOf('className={styles.detailBody}');
  const purchaseIndex = source.indexOf('className={styles.purchasePanel}');
  const reviewsIndex = source.indexOf('<PublicProductReviews');
  const detailIndex = source.indexOf('className={styles.detailSection}');

  assert.ok(imageIndex >= 0 && imageIndex < bodyIndex);
  assert.ok(bodyIndex < purchaseIndex);
  assert.ok(purchaseIndex < reviewsIndex);
  assert.ok(reviewsIndex < detailIndex);
  assert.match(source, /icon="mdi:carrot"/);
  assert.doesNotMatch(styles, /\.detailBody\s*\{[^}]*order:\s*-1/s);
});

test('公开主页按安全投影展示身份、等级与权威统计', () => {
  const source = readClientSource('src/public/profile/PublicProfileApp.tsx');
  const styles = readClientSource('src/public/profile/PublicProfileApp.module.css');

  assert.match(source, /profile\.public\.bioEmpty/);
  assert.match(source, /profile\?\.voCurrentLevel/);
  assert.match(source, /profile\?\.voCurrentLevelName/);
  assert.match(source, /profile\.public\.statsLoading/);
  assert.match(source, /statsError && !loadingStats/);
  assert.doesNotMatch(source, /getExperience|voTotalExp|voLevelProgress|voRank/);
  assert.match(styles, /\.profileIdentityGrid[\s\S]*grid-template-columns:/);
  assert.match(styles, /\.levelNameBadge/);
  assert.match(styles, /\.profileCover/);
});
