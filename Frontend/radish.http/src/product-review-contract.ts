export type ProductReviewLongId = string;

export const ProductReviewErrorCode = {
  AuthenticationRequired: 'ProductReview.AuthenticationRequired',
  ProductNotFound: 'ProductReview.ProductNotFound',
  NotFound: 'ProductReview.NotFound',
  PurchaseRequired: 'ProductReview.PurchaseRequired',
  VersionConflict: 'ProductReview.VersionConflict',
  Moderated: 'ProductReview.Moderated',
} as const;

export type ProductReviewErrorCodeValue =
  typeof ProductReviewErrorCode[keyof typeof ProductReviewErrorCode];

export interface ProductReviewVo {
  voId: ProductReviewLongId;
  voProductId: ProductReviewLongId;
  voUserId: ProductReviewLongId;
  voAuthorPublicId?: string | null;
  voAuthorPublicIndex?: string | number | null;
  voAuthorDisplayName: string;
  voAuthorDisplayHandle?: string | null;
  voRating: number;
  voComment?: string | null;
  voVerifiedPurchase: boolean;
  voVersion: number;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface ProductReviewSummaryVo {
  voAverageRating: number;
  voReviewCount: number;
  voFiveStarCount: number;
  voFourStarCount: number;
  voThreeStarCount: number;
  voTwoStarCount: number;
  voOneStarCount: number;
}

export interface ProductReviewPageVo {
  voSummary: ProductReviewSummaryVo;
  voItems: ProductReviewVo[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export interface MyProductReviewVo {
  voCanReview: boolean;
  voUnavailableReason?: string | null;
  voExpectedVersion: number;
  voReview?: ProductReviewVo | null;
}

export interface UpsertProductReviewRequest {
  rating: number;
  comment?: string | null;
  expectedVersion: number;
}
