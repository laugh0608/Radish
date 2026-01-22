/**
 * 系统配置 API 客户端
 */

import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';

/**
 * 系统配置数据类型
 */
export interface SystemConfig {
  id: number;
  category: string;
  key: string;
  name: string;
  value: string;
  description?: string;
  type: 'string' | 'number' | 'boolean' | 'json';
  isEnabled: boolean;
  createTime: string;
  modifyTime?: string;
}

/**
 * 配置创建/更新请求类型
 */
export interface ConfigRequest {
  category?: string;
  key?: string;
  name?: string;
  value: string;
  description?: string;
  type?: 'string' | 'number' | 'boolean' | 'json';
  isEnabled?: boolean;
}

/**
 * 获取系统配置列表
 */
export async function getSystemConfigs(): Promise<SystemConfig[]> {
  const response = await apiGet<SystemConfig[]>('/api/v1/SystemConfig/GetSystemConfigs', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取系统配置失败');
  }

  return response.data.map((item: any) => ({
    id: item.id || 0,
    category: item.category || '',
    key: item.key || '',
    name: item.name || '',
    value: item.value || '',
    description: item.description || '',
    type: item.type || 'string',
    isEnabled: item.isEnabled !== undefined ? item.isEnabled : true,
    createTime: item.createTime || '',
    modifyTime: item.modifyTime,
  }));
}

/**
 * 获取配置分类列表
 */
export async function getConfigCategories(): Promise<string[]> {
  const response = await apiGet<string[]>('/api/v1/SystemConfig/GetConfigCategories', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取配置分类失败');
  }

  return response.data;
}

/**
 * 根据ID获取配置详情
 */
export async function getConfigById(id: number): Promise<SystemConfig> {
  const response = await apiGet<SystemConfig>(`/api/v1/SystemConfig/GetConfigById?id=${id}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取配置详情失败');
  }

  const item = response.data;
  return {
    id: item.id || 0,
    category: item.category || '',
    key: item.key || '',
    name: item.name || '',
    value: item.value || '',
    description: item.description || '',
    type: item.type || 'string',
    isEnabled: item.isEnabled !== undefined ? item.isEnabled : true,
    createTime: item.createTime || '',
    modifyTime: item.modifyTime,
  };
}

/**
 * 创建系统配置
 */
export async function createConfig(configData: ConfigRequest & { category: string; key: string; name: string }): Promise<SystemConfig> {
  const response = await apiPost<SystemConfig>('/api/v1/SystemConfig/CreateConfig', configData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建配置失败');
  }

  return response.data;
}

/**
 * 更新系统配置
 */
export async function updateConfig(id: number, configData: ConfigRequest): Promise<SystemConfig> {
  const response = await apiPut<SystemConfig>(`/api/v1/SystemConfig/UpdateConfig?id=${id}`, configData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '更新配置失败');
  }

  return response.data;
}

/**
 * 删除系统配置
 */
export async function deleteConfig(id: number): Promise<boolean> {
  const response = await apiDelete(`/api/v1/SystemConfig/DeleteConfig?id=${id}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除配置失败');
  }

  return true;
}