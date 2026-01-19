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
  isEnabled: boolean;
  createTime: string;
  createBy: string;
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
    id: item.id || item.Id,
    roleName: item.voRoleName || item.VoRoleName || item.roleName || '',
    roleDescription: item.voRoleDescription || item.VoRoleDescription || item.roleDescription || '',
    orderSort: item.voOrderSort || item.VoOrderSort || item.orderSort || 0,
    isEnabled: item.voIsEnabled || item.VoIsEnabled || item.isEnabled || false,
    createTime: item.voCreateTime || item.VoCreateTime || item.createTime || '',
    createBy: item.voCreateBy || item.VoCreateBy || item.createBy || '',
  }));
}
