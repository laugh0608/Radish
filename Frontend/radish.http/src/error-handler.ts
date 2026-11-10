import { writeHttpLog } from './browserLogger';

/**
 * 错误处理器类型
 */
export type ErrorHandler = (error: Error) => void;

/**
 * 错误处理配置
 */
interface ErrorHandlingConfig {
  /** 全局错误处理器 */
  onError?: ErrorHandler;
  /** 网络错误处理器 */
  onNetworkError?: ErrorHandler;
  /** HTTP 错误处理器 */
  onHttpError?: (status: number, message: string) => void;
  /** API 错误处理器 */
  onApiError?: (code: string, message: string) => void;
}

/**
 * 当前错误处理配置
 */
let errorConfig: ErrorHandlingConfig = {};

/**
 * 配置错误处理
 */
export function configureErrorHandling(config: ErrorHandlingConfig) {
  errorConfig = { ...errorConfig, ...config };
}

/**
 * 通用错误处理
 */
export function handleError(error: Error): void {
  writeHttpLog('http.operation_failed', error);
  errorConfig.onError?.(error);
}

/**
 * 处理 API 错误
 */
export function handleApiError(code: string, message: string): void {
  writeHttpLog('http.api_failed', { code });
  errorConfig.onApiError?.(code, message);
}

/**
 * 处理网络错误
 */
export function handleNetworkError(error: Error): void {
  writeHttpLog('http.network_failed', error);
  errorConfig.onNetworkError?.(error);
}

/**
 * 处理 HTTP 错误
 */
export function handleHttpError(status: number, message: string): void {
  writeHttpLog('http.response_failed', { status });
  errorConfig.onHttpError?.(status, message);
}

/**
 * 错误处理装饰器
 */
export function withErrorHandling<TArgs extends unknown[], TResult>(
  fn: (...args: TArgs) => Promise<TResult>,
  errorHandler?: ErrorHandler
): (...args: TArgs) => Promise<TResult> {
  return async (...args: TArgs) => {
    try {
      return await fn(...args);
    } catch (error) {
      const handler = errorHandler || errorConfig.onError;
      if (handler && error instanceof Error) {
        handler(error);
      } else {
        handleError(error instanceof Error ? error : new Error(String(error)));
      }
      throw error;
    }
  };
}
