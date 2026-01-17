import {
  configureApiClient,
  configureErrorHandling,
  apiGet,
  apiPost,
  apiPut,
  apiDelete,
  message,
} from '@radish/ui';
import type {
  PagedResponse,
  ParsedApiResponse,
} from '@radish/ui';
import type {
  OidcClient,
  CreateClientRequest,
  UpdateClientRequest,
} from '../types/oidc';
import { autoRefreshTokenInterceptor } from '../utils/tokenRefresh';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl,
  timeout: 30000,
  getToken: () => {
    if (typeof window !== 'undefined') {
      return window.localStorage.getItem('access_token');
    }
    return null;
  },
  onRequest: async () => {
    // 在每次请求前检查并刷新 Token
    await autoRefreshTokenInterceptor();
  },
});

// 配置错误处理
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => {
    message.error(msg);
  },
});

/**
 * OIDC 客户端 API
 */
export const clientApi = {
  /**
   * 获取客户端列表
   */
  async getClients(params?: {
    page?: number;
    pageSize?: number;
    keyword?: string;
  }): Promise<ParsedApiResponse<PagedResponse<OidcClient>>> {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.keyword) query.set('keyword', params.keyword);

    return apiGet<PagedResponse<OidcClient>>(
      `/api/v1/Client/GetClients?${query}`,
      { withAuth: true }
    );
  },

  /**
   * 获取客户端详情
   */
  async getClient(id: string): Promise<ParsedApiResponse<OidcClient>> {
    return apiGet<OidcClient>(
      `/api/v1/Client/GetClient/${id}`,
      { withAuth: true }
    );
  },

  /**
   * 创建客户端
   */
  async createClient(
    data: CreateClientRequest
  ): Promise<ParsedApiResponse<{ clientId: string; clientSecret: string }>> {
    return apiPost<{ clientId: string; clientSecret: string }>(
      '/api/v1/Client/CreateClient',
      data,
      { withAuth: true }
    );
  },

  /**
   * 更新客户端
   */
  async updateClient(
    id: string,
    data: Omit<UpdateClientRequest, 'id'>
  ): Promise<ParsedApiResponse<string>> {
    return apiPut<string>(
      `/api/v1/Client/UpdateClient/${id}`,
      data,
      { withAuth: true }
    );
  },

  /**
   * 删除客户端
   */
  async deleteClient(id: string): Promise<ParsedApiResponse<string>> {
    return apiDelete<string>(
      `/api/v1/Client/DeleteClient/${id}`,
      { withAuth: true }
    );
  },

  /**
   * 重置客户端密钥
   */
  async resetClientSecret(
    id: string
  ): Promise<ParsedApiResponse<{ clientId: string; clientSecret: string }>> {
    return apiPost<{ clientId: string; clientSecret: string }>(
      `/api/v1/Client/ResetClientSecret/${id}`,
      undefined,
      { withAuth: true }
    );
  },
};
