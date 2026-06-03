import { apiGet, apiPost } from '@radish/http';

export interface ResourceApiBindingVo {
  voResourceId: string;
  voResourceKey: string;
  voApiModuleId: string;
  voApiModuleName: string;
  voLinkUrl: string;
  voRelationType: string;
}

export interface ConsoleResourceTreeNodeVo {
  voId: string;
  voTitle: string;
  voResourceKey: string;
  voResourceType: string;
  voChecked: boolean;
  voIndeterminate: boolean;
  voApiBindings: ResourceApiBindingVo[];
  voChildren: ConsoleResourceTreeNodeVo[];
}

export interface RoleAuthorizationSnapshotVo {
  voRoleId: string;
  voRoleName: string;
  voRoleDescription: string;
  voRoleIsEnabled: boolean;
  voLastModifyTime?: string;
  voGrantedResourceIds: string[];
  voGrantedPermissionKeys: string[];
  voDerivedApiModules: ResourceApiBindingVo[];
}

export interface SaveRoleAuthorizationRequest {
  roleId: string;
  resourceIds: string[];
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

export async function getRoleAuthorization(roleId: string): Promise<RoleAuthorizationSnapshotVo> {
  const response = await apiGet<RoleAuthorizationSnapshotVo>(
    `/api/v1/ConsoleAuthorization/GetRoleAuthorization?roleId=${encodeURIComponent(roleId)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取角色授权失败');
  }

  return response.data;
}

export async function getRolePermissionPreview(roleId: string): Promise<ResourceApiBindingVo[]> {
  const response = await apiGet<ResourceApiBindingVo[]>(
    `/api/v1/ConsoleAuthorization/GetRolePermissionPreview?roleId=${encodeURIComponent(roleId)}`,
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
