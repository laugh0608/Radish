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
  ChatMessageSearchScope,
  SearchChannelMessagesDto,
  ChannelMessageSearchItemVo,
  ChannelMessageSearchPageVo,
} from './chat-search-contract';
export { ChatMessageSearchScopes } from './chat-search-contract';

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
  redeemOidcAuthorizationCode,
  OidcCallbackError,
} from './oidc-callback';

export type {
  OidcTokenResponse,
  OidcTokenRequestFailureDetails,
  RedeemOidcAuthorizationCodeOptions,
  OidcCallbackErrorCode,
} from './oidc-callback';
