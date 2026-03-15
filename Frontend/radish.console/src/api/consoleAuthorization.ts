import { apiGet, apiPost } from '@radish/http';

export interface ResourceApiBindingVo {
  voResourceId: number;
  voResourceKey: string;
  voApiModuleId: number;
  voApiModuleName: string;
  voLinkUrl: string;
  voRelationType: string;
}

export interface ConsoleResourceTreeNodeVo {
  voId: number;
  voTitle: string;
  voResourceKey: string;
  voResourceType: string;
  voChecked: boolean;
  voIndeterminate: boolean;
  voApiBindings: ResourceApiBindingVo[];
  voChildren: ConsoleResourceTreeNodeVo[];
}

export interface RoleAuthorizationSnapshotVo {
  voRoleId: number;
  voRoleName: string;
  voRoleDescription: string;
  voRoleIsEnabled: boolean;
  voLastModifyTime?: string;
  voGrantedResourceIds: number[];
  voGrantedPermissionKeys: string[];
  voDerivedApiModules: ResourceApiBindingVo[];
}

export interface SaveRoleAuthorizationRequest {
  roleId: number;
  resourceIds: number[];
  expectedModifyTime?: string;
}

export async function getResourceTree(): Promise<ConsoleResourceTreeNodeVo[]> {
  const response = await apiGet<ConsoleResourceTreeNodeVo[]>(
    '/api/v1/ConsoleAuthorization/GetResourceTree',
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取资源树失败');
  }

  return response.data;
}

export async function getRoleAuthorization(roleId: number): Promise<RoleAuthorizationSnapshotVo> {
  const response = await apiGet<RoleAuthorizationSnapshotVo>(
    `/api/v1/ConsoleAuthorization/GetRoleAuthorization?roleId=${roleId}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色授权失败');
  }

  return response.data;
}

export async function getRolePermissionPreview(roleId: number): Promise<ResourceApiBindingVo[]> {
  const response = await apiGet<ResourceApiBindingVo[]>(
    `/api/v1/ConsoleAuthorization/GetRolePermissionPreview?roleId=${roleId}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取权限预览失败');
  }

  return response.data;
}

export async function saveRoleAuthorization(payload: SaveRoleAuthorizationRequest): Promise<void> {
  const response = await apiPost(
    '/api/v1/ConsoleAuthorization/SaveRoleAuthorization',
    payload,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '保存角色授权失败');
  }
}
