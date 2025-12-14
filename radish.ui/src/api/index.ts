// API 类型定义
export type {
  ApiResponse,
  PagedResponse,
  ApiRequestOptions,
  ParsedApiResponse,
} from './types';

// API 客户端
export {
  configureApiClient,
  getApiClientConfig,
  apiFetch,
  parseApiResponse,
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
