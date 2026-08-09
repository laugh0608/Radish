import { apiDelete, apiGet, apiPut } from './client';
import type { ParsedApiResponse } from './types';
import type {
  MyProductReviewVo,
  ProductReviewLongId,
  ProductReviewPageVo,
  ProductReviewVo,
  UpsertProductReviewRequest,
} from './product-review-contract';

const shopApiBase = '/api/v1/Shop';
const authenticated = { withAuth: true } as const;

export function getProductReviews(
  productId: ProductReviewLongId,
  pageIndex = 1,
  pageSize = 10,
): Promise<ParsedApiResponse<ProductReviewPageVo>> {
  const query = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  return apiGet<ProductReviewPageVo>(
    `${shopApiBase}/GetProductReviews/${encodeURIComponent(productId)}?${query}`,
  );
}

export function getMyProductReview(
  productId: ProductReviewLongId,
): Promise<ParsedApiResponse<MyProductReviewVo>> {
  return apiGet<MyProductReviewVo>(
    `${shopApiBase}/GetMyProductReview/${encodeURIComponent(productId)}`,
    authenticated,
  );
}

export function upsertProductReview(
  productId: ProductReviewLongId,
  request: UpsertProductReviewRequest,
): Promise<ParsedApiResponse<ProductReviewVo>> {
  return apiPut<ProductReviewVo>(
    `${shopApiBase}/UpsertProductReview/${encodeURIComponent(productId)}`,
    request,
    authenticated,
  );
}

export function deleteProductReview(
  reviewId: ProductReviewLongId,
  expectedVersion: number,
): Promise<ParsedApiResponse<ProductReviewVo>> {
  const query = new URLSearchParams({ expectedVersion: String(expectedVersion) });
  return apiDelete<ProductReviewVo>(
    `${shopApiBase}/DeleteProductReview/${encodeURIComponent(reviewId)}?${query}`,
    authenticated,
  );
}
