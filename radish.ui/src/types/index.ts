// 通用类型定义

/**
 * API 响应通用结构
 */
export interface ApiResponse<T = unknown> {
  isSuccess: boolean;
  message?: string;
  responseData?: T;
  statusCode?: number;
}

/**
 * 分页参数
 */
export interface PaginationParams {
  page: number;
  pageSize: number;
}

/**
 * 分页响应
 */
export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
