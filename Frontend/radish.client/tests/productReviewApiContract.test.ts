import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('Client 商品 API 复用共享评价客户端且不转换 LongId', () => {
  const shopSource = readFileSync(resolve(clientRoot, 'src/api/shop.ts'), 'utf8');

  assert.match(shopSource, /getProductReviewsRequest\(String\(productId\), pageIndex, pageSize\)/);
  assert.match(shopSource, /getMyProductReviewRequest\(String\(productId\)\)/);
  assert.match(shopSource, /upsertProductReviewRequest\(String\(productId\), request\)/);
  assert.match(shopSource, /deleteProductReviewRequest\(String\(reviewId\), expectedVersion\)/);
  assert.doesNotMatch(shopSource, /Number\((productId|reviewId)\)/);
});

test('商品评价沿用现有治理目标与父商品深链', () => {
  const moderationSource = readFileSync(
    resolve(clientRoot, 'src/api/contentModeration.ts'),
    'utf8',
  );
  const reportModalSource = readFileSync(
    resolve(clientRoot, 'src/components/ContentReportModal.tsx'),
    'utf8',
  );
  const reportsSource = readFileSync(resolve(clientRoot, 'src/me/MeReportsPage.tsx'), 'utf8');

  assert.match(moderationSource, /'ProductReview'/);
  assert.match(reportModalSource, /case 'ProductReview'/);
  assert.match(reportsSource, /ProductReview[\s\S]*voTargetProductId[\s\S]*#reviews/);
});

test('公开资料只新增等级与等级名，不复用私有经验对象', () => {
  const userSource = readFileSync(resolve(clientRoot, 'src/api/user.ts'), 'utf8');
  const profileMatch = userSource.match(/export interface PublicUserProfile \{([\s\S]*?)\n\}/);

  assert.ok(profileMatch);
  assert.match(profileMatch[1], /voCurrentLevel: number;/);
  assert.match(profileMatch[1], /voCurrentLevelName: string;/);
  assert.doesNotMatch(profileMatch[1], /TotalExp|LevelProgress|Rank|Frozen/);
});
