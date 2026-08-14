import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { ProductReviewErrorCode } from '../src/product-review-contract.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const contractSource = readFileSync(
  resolve(testDirectory, '../src/product-review-contract.ts'),
  'utf8',
);
const clientSource = readFileSync(
  resolve(testDirectory, '../src/product-review-client.ts'),
  'utf8',
);
const indexSource = readFileSync(resolve(testDirectory, '../src/index.ts'), 'utf8');

test('商品评价契约固定五星、已购标识、CAS 与结构化错误', () => {
  assert.equal(ProductReviewErrorCode.PurchaseRequired, 'ProductReview.PurchaseRequired');
  assert.equal(ProductReviewErrorCode.VersionConflict, 'ProductReview.VersionConflict');
  assert.equal(ProductReviewErrorCode.Moderated, 'ProductReview.Moderated');
  assert.match(contractSource, /voRating: number;/);
  assert.match(contractSource, /voVerifiedPurchase: boolean;/);
  assert.match(contractSource, /voVersion: number;/);
  assert.match(contractSource, /voExpectedVersion: number;/);
  assert.match(contractSource, /expectedVersion: number;/);
  assert.match(contractSource, /voFiveStarCount: number;/);
  assert.match(contractSource, /voOneStarCount: number;/);
});

test('商品评价 LongId 保持字符串且写入口统一认证', () => {
  assert.match(contractSource, /type ProductReviewLongId = string;/);
  assert.match(contractSource, /voId: ProductReviewLongId;/);
  assert.match(contractSource, /voProductId: ProductReviewLongId;/);
  assert.match(contractSource, /voUserId: ProductReviewLongId;/);
  assert.match(clientSource, /GetProductReviews\/\$\{encodeURIComponent\(productId\)\}/);
  assert.match(clientSource, /GetMyProductReview\/\$\{encodeURIComponent\(productId\)\}/);
  assert.match(clientSource, /UpsertProductReview\/\$\{encodeURIComponent\(productId\)\}/);
  assert.match(clientSource, /DeleteProductReview\/\$\{encodeURIComponent\(reviewId\)\}/);
  assert.match(clientSource, /const authenticated = \{ withAuth: true \}/);
  assert.doesNotMatch(clientSource, /\bfetch\(/);
});

test('共享入口导出公开读取、本人资格和 CAS 写入能力', () => {
  assert.match(indexSource, /ProductReviewPageVo,[\s\S]*from '\.\/product-review-contract';/);
  assert.match(indexSource, /ProductReviewErrorCode/);
  assert.match(indexSource, /getProductReviews,[\s\S]*getMyProductReview,[\s\S]*upsertProductReview,[\s\S]*deleteProductReview/);
});
