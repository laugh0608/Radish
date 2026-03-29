import { apiDelete, apiGet, apiPost, apiPut } from '@radish/http';

export interface CategoryVo {
  voId: number;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIconAttachmentId?: string | null;
  voIcon?: string | null;
  voCoverAttachmentId?: string | null;
  voCoverImage?: string | null;
  voParentId?: number | null;
  voLevel: number;
  voOrderSort: number;
  voPostCount: number;
  voIsEnabled: boolean;
  voIsDeleted: boolean;
  voCreateTime: string;
  voModifyTime?: string | null;
  voCreateBy?: string | null;
}

export interface CategoryPageModel {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: CategoryVo[];
}

export interface CategoryUpsertRequest {
  name: string;
  slug?: string;
  description?: string;
  iconAttachmentId?: string | null;
  coverAttachmentId?: string | null;
  parentId?: number | null;
  orderSort: number;
  isEnabled: boolean;
}

export async function getCategoryPage(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  isEnabled?: boolean;
  includeDeleted?: boolean;
}): Promise<CategoryPageModel> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));

  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }

  if (params.isEnabled !== undefined) {
    searchParams.set('isEnabled', String(params.isEnabled));
  }

  if (params.includeDeleted !== undefined) {
    searchParams.set('includeDeleted', String(params.includeDeleted));
  }

  const response = await apiGet<CategoryPageModel>(`/api/v1/Category/GetPage?${searchParams.toString()}`, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取分类列表失败');
  }

  return response.data;
}

export async function createCategory(request: CategoryUpsertRequest): Promise<number> {
  const response = await apiPost<number>('/api/v1/Category/Create', request, { withAuth: true });
  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '创建分类失败');
  }

  return response.data;
}

export async function updateCategory(id: number, request: CategoryUpsertRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Category/Update/${id}`, request, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '更新分类失败');
  }
}

export async function toggleCategoryStatus(id: number, enabled: boolean): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Category/ToggleStatus/${id}/status?enabled=${enabled}`, {}, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '更新分类状态失败');
  }
}

export async function updateCategorySort(id: number, sortOrder: number): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Category/UpdateSort/${id}/sort?sortOrder=${sortOrder}`, {}, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '更新分类排序失败');
  }
}

export async function deleteCategory(id: number): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Category/Delete/${id}`, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '删除分类失败');
  }
}

export async function restoreCategory(id: number): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Category/Restore/${id}/restore`, {}, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '恢复分类失败');
  }
}
