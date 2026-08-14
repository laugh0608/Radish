/**
 * @radish/http - Radish HTTP 客户端库
 *
 * 提供统一的 API 请求封装，包括：
 * - 请求/响应拦截器
 * - 认证 Token 管理
 * - 超时控制
 * - 错误处理
 */

// API 类型定义
export type {
  ApiResponse,
  PagedResponse,
  ApiRequestOptions,
  ParsedApiResponse,
} from './types';

export type {
  NotificationCategory,
  NotificationTargetKind,
  NotificationTargetVo,
  NotificationInboxSummaryVo,
  NotificationInboxGroupVo,
  NotificationInboxPageVo,
  NotificationInboxMutationVo,
  NotificationPreferenceVo,
  UpdateNotificationPreferenceDto,
  NotificationInboxChangedVo,
} from './notification-contract';

export type {
  UserBlockMutationRequest,
  DirectConversationBlockMutationRequest,
  UserInteractionCapabilityVo,
  UserBlockMutationVo,
  UserBlockListItemVo,
  UserBlockPageVo,
  UserInteractionChangedVo,
} from './user-block-contract';

export type {
  ChatMessageSearchScope,
  SearchChannelMessagesDto,
  ChannelMessageSearchItemVo,
  ChannelMessageSearchPageVo,
} from './chat-search-contract';
export { ChatMessageSearchScopes } from './chat-search-contract';

export type {
  ChatReactionEmojiType,
  ChatReactionSummaryVo,
  ChatMessageReactionStateVo,
  GetChatMessageReactionStatesDto,
  SetChatMessageReactionDto,
  ChatMessageReactionMutationVo,
} from './chat-reaction-contract';

export type {
  ChatPinnedMessageVo,
  ChatMessagePinVo,
  ChatMessagePinStateVo,
  SetChatMessagePinDto,
  ChatMessagePinMutationVo,
} from './chat-pin-contract';

export type {
  ChatReadReceiptMode,
  AdvanceChannelReadStateDto,
  ChannelReadStateVo,
  GetChatReadReceiptSummariesDto,
  ChatReadReceiptSummaryItemVo,
  ChatReadReceiptSummariesVo,
  ChatReadReceiptReaderVo,
  GetChatReadReceiptReadersQuery,
  ChatReadReceiptReaderPageVo,
  ReadReceiptsChangedVo,
} from './chat-read-receipt-contract';
export { ChatReadReceiptModes } from './chat-read-receipt-contract';

export type {
  WikiLongId,
  WikiDraftReviewStateValue,
  WikiCollaboratorStateValue,
  WikiReviewActionValue,
  WikiAuthorDocumentScope,
  WikiAuthorDraftStage,
  WikiAuthorListQuery,
  WikiAuthorDocumentVo,
  WikiAuthorRevisionItemVo,
  WikiAuthorRevisionDetailVo,
  WikiAuthorRevisionHistoryVo,
  WikiDocumentCollaboratorVo,
  WikiDocumentReviewEventVo,
  WikiAuthorDraftDetailVo,
  WikiReviewQueueItemVo,
  CreateWikiAuthorDraftRequest,
  SaveWikiAuthorDraftRequest,
  SubmitWikiDraftRequest,
  ReviewWikiDraftRequest,
} from './wiki-authoring-contract';
export {
  WikiDraftReviewState,
  WikiCollaboratorState,
  WikiReviewAction,
} from './wiki-authoring-contract';

export type { WikiAttachmentErrorCodeValue } from './wiki-attachment-contract';
export { WikiAttachmentErrorCode } from './wiki-attachment-contract';
export type { AttachmentAssetVariant } from './attachment-asset';
export { loadAttachmentAssetBlob } from './attachment-asset';

export type {
  ModerationLongId,
  ContentModerationTargetType,
  ContentModerationCaseStatus,
  ContentModerationDecision,
  ContentModerationTargetDisposition,
  ContentReportReceiptVo,
  ContentModerationCaseQueueItemVo,
  ContentModerationCaseReportVo,
  ContentModerationEvidenceVo,
  ContentModerationCaseEventVo,
  UserModerationStateVo,
  ContentModerationCaseDetailVo,
  CaptureContentModerationEvidenceRequest,
  ContentModerationCaseUserActionRequest,
  ReviewContentModerationCaseRequest,
  ContentModerationCaseReviewResultVo,
  ApplyContentModerationCorrectiveActionRequest,
  ContentModerationAppealStatus,
  ContentModerationAppealOutcome,
  ContentModerationReliefScope,
  ContentModerationDecisionNoticeVo,
  ContentModerationAppealEventVo,
  ContentModerationTargetActionVo,
  ContentModerationUserActionSummaryVo,
  ContentModerationAppealVo,
  ContentModerationAppealActionResultVo,
  SubmitContentModerationAppealRequest,
  ContentModerationAppealVersionedOperationRequest,
  ReviewContentModerationAppealRequest,
  CaptureContentModerationAppealEvidenceRequest,
} from './content-moderation-contract';

export type {
  ForumContentLongId,
  ForumContentRevisionSourceType,
  ForumContentRevisionIntegrityStatus,
  ForumContentRevisionErrorCodeValue,
  ForumContentRevisionTagVo,
  ForumContentRevisionSummaryVo,
  ForumContentRevisionListVo,
  PostContentRevisionSummaryVo,
  CommentContentRevisionSummaryVo,
  PostContentRevisionListVo,
  CommentContentRevisionListVo,
  PostContentRevisionDetailVo,
  CommentContentRevisionDetailVo,
  RestoreForumContentRevisionRequest,
  ForumContentRevisionWriteResult,
} from './forum-content-revision-contract';
export { ForumContentRevisionErrorCode } from './forum-content-revision-contract';

export type {
  ForumQuestionErrorCodeValue,
  PostAnswerSort,
  PostAnswerVo,
  GetPostAnswerPageRequest,
  CreatePostAnswerRequest,
  PostAnswerPageVo,
  UpdatePostAnswerRequest,
  DeletePostAnswerRequest,
  RestorePostAnswerRevisionRequest,
  ChangePostAnswerAcceptanceRequest,
  RevokePostAnswerAcceptanceRequest,
  PostAnswerMutationVo,
  PostAnswerAcceptanceMutationVo,
  PostAnswerRevisionSummaryVo,
  PostAnswerRevisionListVo,
  PostAnswerRevisionDetailVo,
} from './forum-question-contract';
export { ForumQuestionErrorCode } from './forum-question-contract';
export {
  getPostAnswerPage,
  createPostAnswer,
  updatePostAnswer,
  deletePostAnswer,
  getPostAnswerRevisions,
  getPostAnswerRevision,
  restorePostAnswerRevision,
  acceptPostAnswer,
  revokePostAnswerAcceptance,
} from './forum-question-client';

export type {
  PostBookmarkErrorCodeValue,
  PostBookmarkTargetStatus,
  SetPostBookmarkStateRequest,
  RemovePostBookmarkRequest,
  GetMyPostBookmarksRequest,
  PostBookmarkStateVo,
  PostBookmarkRemoveVo,
  UserPostBookmarkTagVo,
  UserPostBookmarkVo,
  UserPostBookmarkPageVo,
} from './post-bookmark-contract';
export { PostBookmarkErrorCode } from './post-bookmark-contract';
export {
  setPostBookmarkState,
  getMyPostBookmarks,
  removePostBookmark,
} from './post-bookmark-client';

export type {
  ProductReviewLongId,
  ProductReviewErrorCodeValue,
  ProductReviewVo,
  ProductReviewSummaryVo,
  ProductReviewPageVo,
  MyProductReviewVo,
  UpsertProductReviewRequest,
} from './product-review-contract';
export { ProductReviewErrorCode } from './product-review-contract';
export {
  getProductReviews,
  getMyProductReview,
  upsertProductReview,
  deleteProductReview,
} from './product-review-client';

export type {
  PublicDiscoverItemKind,
  PublicDiscoverTargetKind,
  PublicDiscoverMetricKind,
  PublicDiscoverActorVo,
  PublicDiscoverTargetVo,
  PublicDiscoverMetricVo,
  PublicDiscoverItemVo,
  PublicDiscoverPulseVo,
  PublicDiscoverFeedVo,
  GetPublicDiscoverFeedRequest,
} from './public-discover-contract';
export {
  PublicDiscoverItemKinds,
  PublicDiscoverTargetKinds,
  PublicDiscoverMetricKinds,
} from './public-discover-contract';
export { getPublicDiscoverFeed } from './public-discover-client';

export type {
  ContentRewardLongId,
  ContentRewardTargetType,
  ContentRewardReasonCode,
  ContentRewardErrorCodeValue,
  CreateContentRewardRequest,
  ContentRewardTargetRequest,
  GetContentRewardTargetStatesRequest,
  ContentRewardMutationVo,
  ContentRewardTargetStateVo,
  ContentRewardRecordVo,
  ContentRewardTargetPageVo,
} from './content-reward-contract';
export {
  ContentRewardTargetTypes,
  ContentRewardReasonCodes,
  ContentRewardErrorCode,
} from './content-reward-contract';

export type { ApiClientConfig } from './client';
export {
  ApiResponseError,
  createApiResponseError,
  isApiResponseNotFoundError,
} from './api-response-error';

// API 客户端
export {
  configureApiClient,
  getApiClientConfig,
  apiFetch,
  parseApiResponse,
  parseApiResponseWithI18n,
  parseHttpResponse,
  apiGet,
  apiPost,
  apiPut,
  apiDelete,
} from './client';

// 错误处理
export {
  configureErrorHandling,
  handleError,
  handleApiError,
  handleNetworkError,
  handleHttpError,
  withErrorHandling,
} from './error-handler';

export type { ErrorHandler } from './error-handler';

// Token 刷新
export {
  configureTokenRefresh,
  getTokenRefreshConfig,
  TokenRefreshErrorType,
} from './token-refresh';

export {
  createOidcAuthorizationUrl,
  redeemOidcAuthorizationCode,
  OidcCallbackError,
} from './oidc-callback';

export type {
  CreateOidcAuthorizationUrlOptions,
  OidcAuthorizationErrorDetails,
  OidcTokenResponse,
  OidcTokenRequestFailureDetails,
  RedeemOidcAuthorizationCodeOptions,
  OidcCallbackErrorCode,
} from './oidc-callback';
