import { apiGet, apiPost, apiPut, apiDelete } from '@radish/http';

export interface TagVo {
  voId: number;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voColor?: string | null;
  voSortOrder: number;
  voPostCount: number;
  voIsEnabled: boolean;
  voIsFixed: boolean;
  voIsDeleted: boolean;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface TagPageModel {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: TagVo[];
}

export interface TagUpsertRequest {
  name: string;
  slug?: string;
  description?: string;
  color?: string;
  sortOrder: number;
  isEnabled: boolean;
  isFixed: boolean;
}

export async function getTagPage(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  isEnabled?: boolean;
  isFixed?: boolean;
  includeDeleted?: boolean;
}): Promise<TagPageModel> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));

  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }

  if (params.isEnabled !== undefined) {
    searchParams.set('isEnabled', String(params.isEnabled));
  }

  if (params.isFixed !== undefined) {
    searchParams.set('isFixed', String(params.isFixed));
  }

  if (params.includeDeleted !== undefined) {
    searchParams.set('includeDeleted', String(params.includeDeleted));
  }

  const response = await apiGet<TagPageModel>(`/api/v1/Tag/GetPage?${searchParams.toString()}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取标签列表失败');
  }

  return response.data;
}

export async function createTag(request: TagUpsertRequest): Promise<number> {
  const response = await apiPost<number>('/api/v1/Tag/Create', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '创建标签失败');
  }

  return response.data;
}

export async function updateTag(id: number, request: TagUpsertRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Tag/Update/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '更新标签失败');
  }
}

export async function toggleTagStatus(id: number, enabled: boolean): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Tag/ToggleStatus/${id}/status?enabled=${enabled}`, {}, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '更新标签状态失败');
  }
}

export async function updateTagSort(id: number, sortOrder: number): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Tag/UpdateSort/${id}/sort?sortOrder=${sortOrder}`, {}, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '更新排序失败');
  }
}

export async function deleteTag(id: number): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Tag/Delete/${id}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除标签失败');
  }
}

export async function restoreTag(id: number): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Tag/Restore/${id}/restore`, {}, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '恢复标签失败');
  }
}
