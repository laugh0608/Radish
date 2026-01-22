/**
 * 角色管理 API 客户端
 */

import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';

/**
 * 角色数据类型
 */
export interface Role {
  id: number;
  roleName: string;
  roleDescription: string;
  orderSort: number;
  departmentIds: string;
  authorityScope: number;
  isEnabled: boolean;
  isDeleted: boolean;
  createId: number;
  createBy: string;
  createTime: string;
  modifyId?: number;
  modifyBy?: string;
  modifyTime?: string;
}

/**
 * 角色创建/更新请求类型
 */
export interface RoleRequest {
  roleName: string;
  roleDescription?: string;
  orderSort?: number;
  departmentIds?: string;
  authorityScope?: number;
  isEnabled?: boolean;
}

/**
 * 获取角色列表
 */
export async function getRoleList(): Promise<Role[]> {
  const response = await apiGet<Role[]>('/api/v1/Role/GetRoleList', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色列表失败');
  }

  // 映射 Vo 字段
  return response.data.map((item: any) => ({
    id: item.voId || item.VoId || item.id || 0,
    roleName: item.voRoleName || item.VoRoleName || item.roleName || '',
    roleDescription: item.voRoleDescription || item.VoRoleDescription || item.roleDescription || '',
    orderSort: item.voOrderSort || item.VoOrderSort || item.orderSort || 0,
    departmentIds: item.voDepartmentIds || item.VoDepartmentIds || item.departmentIds || '',
    authorityScope: item.voAuthorityScope || item.VoAuthorityScope || item.authorityScope || 0,
    isEnabled: item.voIsEnabled || item.VoIsEnabled || item.isEnabled || false,
    isDeleted: item.voIsDeleted || item.VoIsDeleted || item.isDeleted || false,
    createId: item.voCreateId || item.VoCreateId || item.createId || 0,
    createBy: item.voCreateBy || item.VoCreateBy || item.createBy || '',
    createTime: item.voCreateTime || item.VoCreateTime || item.createTime || '',
    modifyId: item.voModifyId || item.VoModifyId || item.modifyId,
    modifyBy: item.voModifyBy || item.VoModifyBy || item.modifyBy,
    modifyTime: item.voModifyTime || item.VoModifyTime || item.modifyTime,
  }));
}

/**
 * 根据ID获取角色详情
 */
export async function getRoleById(id: number): Promise<Role> {
  const response = await apiGet<any>(`/api/v1/Role/GetRoleById?id=${id}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色详情失败');
  }

  const item = response.data;
  return {
    id: item.voId || item.VoId || item.id || 0,
    roleName: item.voRoleName || item.VoRoleName || item.roleName || '',
    roleDescription: item.voRoleDescription || item.VoRoleDescription || item.roleDescription || '',
    orderSort: item.voOrderSort || item.VoOrderSort || item.orderSort || 0,
    departmentIds: item.voDepartmentIds || item.VoDepartmentIds || item.departmentIds || '',
    authorityScope: item.voAuthorityScope || item.VoAuthorityScope || item.authorityScope || 0,
    isEnabled: item.voIsEnabled || item.VoIsEnabled || item.isEnabled || false,
    isDeleted: item.voIsDeleted || item.VoIsDeleted || item.isDeleted || false,
    createId: item.voCreateId || item.VoCreateId || item.createId || 0,
    createBy: item.voCreateBy || item.VoCreateBy || item.createBy || '',
    createTime: item.voCreateTime || item.VoCreateTime || item.createTime || '',
    modifyId: item.voModifyId || item.VoModifyId || item.modifyId,
    modifyBy: item.voModifyBy || item.VoModifyBy || item.modifyBy,
    modifyTime: item.voModifyTime || item.VoModifyTime || item.modifyTime,
  };
}

/**
 * 创建角色
 */
export async function createRole(roleData: RoleRequest): Promise<Role> {
  // 转换为 Vo 格式
  const voData = {
    voRoleName: roleData.roleName,
    voRoleDescription: roleData.roleDescription || '',
    voOrderSort: roleData.orderSort || 0,
    voDepartmentIds: roleData.departmentIds || '',
    voAuthorityScope: roleData.authorityScope || 0,
    voIsEnabled: roleData.isEnabled !== undefined ? roleData.isEnabled : true,
    voIsDeleted: false,
    voCreateBy: 'Admin', // TODO: 从当前用户获取
  };

  const response = await apiPost<Role>('/api/v1/Role/CreateRole', voData, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建角色失败');
  }

  return response.data;
}

/**
 * 更新角色
 */
export async function updateRole(id: number, roleData: RoleRequest): Promise<Role> {
  // 转换为 Vo 格式
  const voData = {
    voId: id,
    voRoleName: roleData.roleName,
    voRoleDescription: roleData.roleDescription || '',
    voOrderSort: roleData.orderSort || 0,
    voDepartmentIds: roleData.departmentIds || '',
    voAuthorityScope: roleData.authorityScope || 0,
    voIsEnabled: roleData.isEnabled !== undefined ? roleData.isEnabled : true,
    voModifyBy: 'Admin', // TODO: 从当前用户获取
  };

  const response = await apiPut<Role>(`/api/v1/Role/UpdateRole?id=${id}`, voData, { withAuth: true });

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
export async function toggleRoleStatus(id: number, enabled: boolean): Promise<Role> {
  const response = await apiPut<Role>(`/api/v1/Role/ToggleRoleStatus?id=${id}&enabled=${enabled}`, {}, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '操作失败');
  }

  return response.data;
}
