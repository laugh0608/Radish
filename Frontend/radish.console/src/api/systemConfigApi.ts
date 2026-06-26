/**
 * 系统设置 API 客户端
 * 使用后端 Vo 字段名
 */

import { apiGet, apiPost, apiPut, apiDelete } from '@radish/http';

/**
 * 系统设置数据类型（使用 Vo 前缀）
 */
export interface SystemConfigVo {
  voId: number;
  voCategory: string;
  voKey: string;
  voName: string;
  voValue: string;
  voDefaultValue: string;
  voEffectiveValue: string;
  voDescription?: string;
  voImpactSummary?: string;
  voType: 'string' | 'number' | 'boolean' | 'json';
  voMinNumberValue?: number | null;
  voMaxNumberValue?: number | null;
  voRequiresInteger: boolean;
  voIsEnabled: boolean;
  voIsOverridden: boolean;
  voRiskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  voEffectiveMode: 'Immediate' | 'RestartRequired' | string;
  voIsEditable: boolean;
  voIsSensitive: boolean;
  voVersion: number;
  voCreateTime?: string;
  voModifyTime?: string;
}

/**
 * 系统设置变更审计数据类型
 */
export interface SystemConfigChangeLogVo {
  voId: number;
  voCategory: string;
  voKey: string;
  voName: string;
  voActionType: 'UpdateOverride' | 'RestoreDefault' | string;
  voOldValue?: string;
  voNewValue?: string;
  voDefaultValue: string;
  voReason: string;
  voRiskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  voEffectiveMode: 'Immediate' | 'RestartRequired' | string;
  voConfirmRiskLevel?: string;
  voConfirmKey?: string;
  voOperatorUserId?: number;
  voOperatorUserName?: string;
  voRequestIp?: string;
  voUserAgent?: string;
  voCreateTime: string;
}

/**
 * 设置覆盖值请求类型
 */
export interface ConfigRequest {
  category?: string;
  key?: string;
  name?: string;
  value: string;
  description?: string;
  type?: 'string' | 'number' | 'boolean' | 'json';
  isEnabled?: boolean;
  reason?: string;
  confirmRiskLevel?: string;
  confirmKey?: string;
  expectedVersion: number;
}

/**
 * 获取系统设置列表
 */
export async function getSystemConfigs(): Promise<SystemConfigVo[]> {
  const response = await apiGet<SystemConfigVo[]>('/api/v1/SystemConfig/GetSystemConfigs', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取系统设置失败');
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
 * 兼容旧创建接口，后端会拒绝未知设置创建
 */
export async function createConfig(configData: ConfigRequest & { category: string; key: string; name: string }): Promise<SystemConfigVo> {
  const response = await apiPost<SystemConfigVo>('/api/v1/SystemConfig/CreateConfig', configData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '系统设置不支持通过 Console 新增');
  }

  return response.data;
}

/**
 * 更新系统设置覆盖值
 */
export async function updateConfig(id: number, configData: ConfigRequest): Promise<SystemConfigVo> {
  const response = await apiPut<SystemConfigVo>(`/api/v1/SystemConfig/UpdateConfig?id=${id}`, configData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '更新系统设置失败');
  }

  return response.data;
}

/**
 * 恢复系统设置默认值
 */
export async function restoreConfigDefault(
  id: number,
  request: Pick<ConfigRequest, 'expectedVersion'> & Partial<Pick<ConfigRequest, 'reason' | 'confirmRiskLevel' | 'confirmKey'>>
): Promise<SystemConfigVo> {
  const response = await apiPut<SystemConfigVo>(
    `/api/v1/SystemConfig/RestoreConfigDefault?id=${id}`,
    {
      reason: request?.reason || '恢复默认值',
      confirmRiskLevel: request?.confirmRiskLevel,
      confirmKey: request?.confirmKey,
      expectedVersion: request.expectedVersion,
    },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '恢复默认失败');
  }

  return response.data;
}

/**
 * 获取系统设置变更历史
 */
export async function getConfigChangeLogs(id: number, take = 20): Promise<SystemConfigChangeLogVo[]> {
  const response = await apiGet<SystemConfigChangeLogVo[]>(
    `/api/v1/SystemConfig/GetConfigChangeLogs?id=${id}&take=${take}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取系统设置变更历史失败');
  }

  return response.data;
}

/**
 * 兼容旧删除接口，后端语义为恢复默认
 */
export async function deleteConfig(id: number): Promise<boolean> {
  const response = await apiDelete(`/api/v1/SystemConfig/DeleteConfig?id=${id}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '恢复默认失败');
  }

  return true;
}
