/**
 * API 响应类型 - 对应后端 MessageModel
 */
export interface ApiResponse<T = any> {
  /** 操作是否成功 */
  isSuccess: boolean;
  /** HTTP 状态码 */
  statusCode: number;
  /** 消息内容 */
  messageInfo: string;
  /** 开发环境消息（可选） */
  messageInfoDev?: string;
  /** 响应数据 */
  responseData?: T;
  /** 错误码（可选，用于客户端特殊处理） */
  code?: string;
  /** 国际化消息键（可选，用于 i18n） */
  messageKey?: string;
}

/**
 * 分页数据模型
 */
export interface PagedResponse<T> {
  /** 当前页码 */
  page: number;
  /** 每页数量 */
  pageSize: number;
  /** 总数据量 */
  dataCount: number;
  /** 总页数 */
  pageCount: number;
  /** 数据列表 */
  data: T[];
}

/**
 * API 请求配置
 */
export interface ApiRequestOptions extends RequestInit {
  /** 是否携带认证信息 */
  withAuth?: boolean;
  /** 基础 URL（可选，默认使用全局配置） */
  baseUrl?: string;
  /** 请求超时时间（毫秒） */
  timeout?: number;
}

/**
 * 解析后的 API 响应
 */
export interface ParsedApiResponse<T> {
  /** 请求是否成功 */
  ok: boolean;
  /** 响应数据 */
  data?: T;
  /** 错误消息 */
  message?: string;
  /** 错误码 */
  code?: string;
  /** HTTP 状态码 */
  statusCode?: number;
}