import { configureApiClient } from '@radish/ui';
import { tokenService } from '../services/tokenService';
import { getApiBaseUrl } from '@/config/env';
import { log } from '@/utils/logger';

/**
 * 配置 API 客户端的 Token 自动刷新
 */
export function setupApiInterceptors() {
  configureApiClient({
    // 配置 API 基础 URL
    baseUrl: getApiBaseUrl(),

    // 获取 Token 的函数
    getToken: () => {
      return tokenService.getAccessToken();
    },

    // 请求拦截器
    onRequest: async (url: string, options: RequestInit) => {
      // 如果请求需要认证，确保使用有效的 Token
      const authHeader = (options.headers as Record<string, string>)?.Authorization;
      if (authHeader && authHeader.startsWith('Bearer ')) {
        try {
          const validToken = await tokenService.getValidAccessToken();
          if (validToken) {
            (options.headers as Record<string, string>).Authorization = `Bearer ${validToken}`;
          }
        } catch (error) {
          log.error('ApiInterceptor', '获取有效 Token 失败', error);
        }
      }

      log.debug('ApiInterceptor', '发送请求', {
        url,
        method: options.method,
        hasAuth: !!authHeader,
      });
    },

    // 响应拦截器
    onResponse: (response: Response) => {
      log.debug('ApiInterceptor', '收到响应', {
        url: response.url,
        status: response.status,
        statusText: response.statusText,
      });

      // 如果返回 401，可能是 Token 过期
      if (response.status === 401) {
        log.warn('ApiInterceptor', '收到 401 响应，可能需要重新登录');

        // 清除 Token 并跳转到登录页
        tokenService.clearTokens();

        // 如果当前不在登录页，跳转到登录页
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }
      }
    },

    // 错误拦截器
    onError: (error: Error) => {
      log.error('ApiInterceptor', 'API 请求错误', error);

      // 如果是网络错误或超时，可以在这里处理
      if (error.name === 'AbortError') {
        log.warn('ApiInterceptor', '请求超时');
      } else if (error.message.includes('Failed to fetch')) {
        log.warn('ApiInterceptor', '网络连接失败');
      }
    },
  });

  log.info('ApiInterceptor', 'API 拦截器已配置');
}

/**
 * 手动刷新 Token
 */
export async function refreshToken(): Promise<boolean> {
  try {
    await tokenService.refreshAccessToken();
    return true;
  } catch (error) {
    log.error('ApiInterceptor', '手动刷新 Token 失败', error);
    return false;
  }
}

/**
 * 检查 Token 状态
 */
export function getTokenStatus() {
  const hasToken = !!tokenService.getAccessToken();
  const hasRefreshToken = !!tokenService.getRefreshToken();
  const isExpiringSoon = tokenService.isTokenExpiringSoon();
  const isExpired = tokenService.isTokenExpired();
  const expiresAt = tokenService.getTokenExpiresAt();

  return {
    hasToken,
    hasRefreshToken,
    isExpiringSoon,
    isExpired,
    expiresAt: expiresAt ? new Date(expiresAt) : null,
  };
}