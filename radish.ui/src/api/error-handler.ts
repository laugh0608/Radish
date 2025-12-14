import type { ParsedApiResponse } from './types';

/**
 * 错误处理器类型
 */
export type ErrorHandler = (error: Error | string, context?: any) => void;

/**
 * 错误处理配置
 */
interface ErrorHandlingConfig {
  /** 是否自动显示错误消息 */
  autoShowMessage: boolean;
  /** 自定义错误处理器 */
  onError?: ErrorHandler;
  /** 消息显示函数（例如 antd 的 message.error） */
  showMessage?: (message: string) => void;
}

/**
 * 默认错误处理配置
 */
let errorConfig: ErrorHandlingConfig = {
  autoShowMessage: true,
  showMessage: (msg) => {
    console.error('[API Error]', msg);
  },
};

/**
 * 配置错误处理
 */
export function configureErrorHandling(config: Partial<ErrorHandlingConfig>) {
  errorConfig = { ...errorConfig, ...config };
}

/**
 * 获取错误消息文本
 */
function getErrorMessage(error: unknown): string {
  if (typeof error === 'string') {
    return error;
  }

  if (error instanceof Error) {
    return error.message;
  }

  if (error && typeof error === 'object') {
    if ('message' in error && typeof error.message === 'string') {
      return error.message;
    }
    if ('messageInfo' in error && typeof error.messageInfo === 'string') {
      return error.messageInfo;
    }
  }

  return '未知错误';
}

/**
 * 统一的错误处理函数
 */
export function handleError(error: unknown, context?: any): void {
  const message = getErrorMessage(error);

  // 调用自定义错误处理器
  if (errorConfig.onError) {
    errorConfig.onError(
      error instanceof Error ? error : new Error(message),
      context
    );
  }

  // 自动显示错误消息
  if (errorConfig.autoShowMessage && errorConfig.showMessage) {
    errorConfig.showMessage(message);
  }

  // 开发环境下打印详细错误
  if (import.meta.env.DEV) {
    console.error('[Error Handler]', {
      error,
      message,
      context,
    });
  }
}

/**
 * 处理 API 响应错误
 */
export function handleApiError<T>(
  response: ParsedApiResponse<T>,
  context?: any
): void {
  if (!response.ok && response.message) {
    handleError(response.message, {
      ...context,
      statusCode: response.statusCode,
      code: response.code,
    });
  }
}

/**
 * API 请求的错误处理装饰器
 */
export function withErrorHandling<T extends (...args: any[]) => Promise<any>>(
  fn: T,
  context?: any
): T {
  return (async (...args: any[]) => {
    try {
      return await fn(...args);
    } catch (error) {
      handleError(error, context);
      throw error;
    }
  }) as T;
}

/**
 * 网络错误处理
 */
export function handleNetworkError(error: unknown): void {
  let message = '网络请求失败';

  if (error instanceof Error) {
    if (error.name === 'AbortError') {
      message = '请求超时';
    } else if (error.message.includes('Failed to fetch')) {
      message = '网络连接失败，请检查您的网络';
    } else {
      message = error.message;
    }
  }

  handleError(message);
}

/**
 * HTTP 状态码错误处理
 */
export function handleHttpError(statusCode: number, message?: string): void {
  const errorMessages: Record<number, string> = {
    400: '请求参数错误',
    401: '未授权，请重新登录',
    403: '拒绝访问',
    404: '请求的资源不存在',
    408: '请求超时',
    500: '服务器内部错误',
    502: '网关错误',
    503: '服务不可用',
    504: '网关超时',
  };

  const defaultMessage = errorMessages[statusCode] || `请求失败 (${statusCode})`;
  handleError(message || defaultMessage);
}
