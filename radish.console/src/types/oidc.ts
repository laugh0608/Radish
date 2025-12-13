/**
 * OIDC 客户端（应用）类型定义
 */
export interface OidcClient {
  id: string;
  clientId: string;
  clientName?: string;
  displayName?: string;
  description?: string;
  logo?: string;
  developerName?: string;
  developerEmail?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  permissions?: string[];
  requirements?: string[];
  status?: 'Active' | 'Disabled';
  type?: 'Internal' | 'ThirdParty';
  createdAt?: string;
  createdBy?: number;
}

/**
 * 创建客户端请求
 */
export interface CreateClientRequest {
  clientId: string;
  displayName: string;
  description?: string;
  logo?: string;
  developerName?: string;
  developerEmail?: string;
  redirectUris: string[];
  postLogoutRedirectUris?: string[];
  permissions?: string[];
}

/**
 * 更新客户端请求
 */
export interface UpdateClientRequest {
  id: string;
  displayName?: string;
  description?: string;
  logo?: string;
  developerName?: string;
  developerEmail?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  permissions?: string[];
}

/**
 * 分页响应
 */
export interface PagedResponse<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}
