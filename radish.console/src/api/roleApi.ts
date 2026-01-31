/**
 * 角色管理 API 客户端
 * 直接使用后端 Vo 字段名，不做映射
 */

import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';

/**
 * 角色数据类型（使用 vo 前缀，与后端 VO 保持一致）
 */
export interface RoleVo {
  voId: number;
  voRoleName: string;
  voRoleDescription: string;
  voOrderSort: number;
  voDepartmentIds: string;
  voAuthorityScope: number;
  voIsEnabled: boolean;
  voIsDeleted: boolean;
  voCreateId: number;
  voCreateBy: string;
  voCreateTime: string;
  voModifyId?: number;
  voModifyBy?: string;
  voModifyTime?: string;
}

/**
 * 角色创建请求类型（使用 vo 前缀）
 */
export interface CreateRoleRequest {
  voRoleName: string;
  voRoleDescription?: string;
  voOrderSort?: number;
  voDepartmentIds?: string;
  voAuthorityScope?: number;
  voIsEnabled?: boolean;
}

/**
 * 角色更新请求类型（使用 vo 前缀）
 */
export interface UpdateRoleRequest extends CreateRoleRequest {
  voId: number;
}

/**
 * 获取角色列表
 */
export async function getRoleList(): Promise<RoleVo[]> {
  const response = await apiGet<RoleVo[]>('/api/v1/Role/GetRoleList', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色列表失败');
  }

  return response.data;
}

/**
 * 根据ID获取角色详情
 */
export async function getRoleById(id: number): Promise<RoleVo> {
  const response = await apiGet<RoleVo>(`/api/v1/Role/GetRoleById?id=${id}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色详情失败');
  }

  return response.data;
}

/**
 * 创建角色
 */
export async function createRole(roleData: CreateRoleRequest): Promise<RoleVo> {
  const response = await apiPost<RoleVo>('/api/v1/Role/CreateRole', roleData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建角色失败');
  }

  return response.data;
}

/**
 * 更新角色
 */
export async function updateRole(id: number, roleData: CreateRoleRequest): Promise<RoleVo> {
  const voData: UpdateRoleRequest = {
    voId: id,
    ...roleData
  };

  const response = await apiPut<RoleVo>(`/api/v1/Role/UpdateRole?id=${id}`, voData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '更新角色失败');
  }

  return response.data;
}

/**
 * 删除角色
 */
export async function deleteRole(id: number): Promise<boolean> {
  const response = await apiDelete(`/api/v1/Role/DeleteRole?id=${id}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除角色失败');
  }

  return true;
}

/**
 * 启用/禁用角色
 */
export async function toggleRoleStatus(id: number, enabled: boolean): Promise<RoleVo> {
  const response = await apiPut<RoleVo>(`/api/v1/Role/ToggleRoleStatus?id=${id}&enabled=${enabled}`, {}, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '操作失败');
  }

  return response.data;
}
