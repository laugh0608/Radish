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
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  grantTypes: string[];
  scopes: string[];
  consentType?: string;
  requirePkce: boolean;
  clientType: 'public' | 'confidential';
  status?: 'Active' | 'Disabled';
  type?: 'Internal' | 'ThirdParty';
  createdAt?: string;
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
  grantTypes: string[];
  scopes: string[];
  consentType: string;
  requirePkce: boolean;
  clientType: 'public' | 'confidential';
}

/**
 * 更新客户端请求
 */
export interface UpdateClientRequest {
  id: string;
  displayName?: string;
  description?: string;
  developerName?: string;
  developerEmail?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  grantTypes?: string[];
  scopes?: string[];
  consentType?: string;
  requirePkce?: boolean;
}

export interface ClientSecretResult {
  clientId: string;
  clientSecret: string | null;
  message: string;
}
