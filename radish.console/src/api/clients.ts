import { apiFetch, parseApiResponse, type ApiResponse } from './client';
import type {
  OidcClient,
  CreateClientRequest,
  UpdateClientRequest,
  PagedResponse,
} from '../types/oidc';

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
  }): Promise<{ ok: boolean; data?: PagedResponse<OidcClient>; message?: string }> {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.keyword) query.set('keyword', params.keyword);

    const response = await apiFetch(`/api/v1/Client/GetClients?${query}`, {
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<PagedResponse<OidcClient>>;
    return parseApiResponse(json);
  },

  /**
   * 获取客户端详情
   */
  async getClient(id: string): Promise<{ ok: boolean; data?: OidcClient; message?: string }> {
    const response = await apiFetch(`/api/v1/Client/GetClient/${id}`, {
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<OidcClient>;
    return parseApiResponse(json);
  },

  /**
   * 创建客户端
   */
  async createClient(data: CreateClientRequest): Promise<{ ok: boolean; data?: { clientId: string; clientSecret: string }; message?: string }> {
    const response = await apiFetch('/api/v1/Client/CreateClient', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<{ clientId: string; clientSecret: string }>;
    return parseApiResponse(json);
  },

  /**
   * 更新客户端
   */
  async updateClient(data: UpdateClientRequest): Promise<{ ok: boolean; message?: string }> {
    const response = await apiFetch('/api/v1/Client/UpdateClient', {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<void>;
    return parseApiResponse(json);
  },

  /**
   * 删除客户端
   */
  async deleteClient(id: string): Promise<{ ok: boolean; message?: string }> {
    const response = await apiFetch(`/api/v1/Client/DeleteClient/${id}`, {
      method: 'DELETE',
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<void>;
    return parseApiResponse(json);
  },

  /**
   * 重置客户端密钥
   */
  async resetClientSecret(id: string): Promise<{ ok: boolean; data?: { clientSecret: string }; message?: string }> {
    const response = await apiFetch(`/api/v1/Client/ResetClientSecret/${id}`, {
      method: 'POST',
      withAuth: true,
    });
    const json = await response.json() as ApiResponse<{ clientSecret: string }>;
    return parseApiResponse(json);
  },
};
