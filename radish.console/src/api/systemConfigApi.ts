/**
 * 系统配置 API 客户端
 * 使用后端 Vo 字段名
 */

import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';

/**
 * 系统配置数据类型（使用 Vo 前缀）
 */
export interface SystemConfigVo {
  voId: number;
  voCategory: string;
  voKey: string;
  voName: string;
  voValue: string;
  voDescription?: string;
  voType: 'string' | 'number' | 'boolean' | 'json';
  voIsEnabled: boolean;
  voCreateTime: string;
  voModifyTime?: string;
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
export async function getSystemConfigs(): Promise<SystemConfigVo[]> {
  const response = await apiGet<SystemConfigVo[]>('/api/v1/SystemConfig/GetSystemConfigs', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取系统配置失败');
  }

  return response.data;
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
export async function getConfigById(id: number): Promise<SystemConfigVo> {
  const response = await apiGet<SystemConfigVo>(`/api/v1/SystemConfig/GetConfigById?id=${id}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取配置详情失败');
  }

  return response.data;
}

/**
 * 创建系统配置
 */
export async function createConfig(configData: ConfigRequest & { category: string; key: string; name: string }): Promise<SystemConfigVo> {
  const response = await apiPost<SystemConfigVo>('/api/v1/SystemConfig/CreateConfig', configData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建配置失败');
  }

  return response.data;
}

/**
 * 更新系统配置
 */
export async function updateConfig(id: number, configData: ConfigRequest): Promise<SystemConfigVo> {
  const response = await apiPut<SystemConfigVo>(`/api/v1/SystemConfig/UpdateConfig?id=${id}`, configData, { withAuth: true });

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