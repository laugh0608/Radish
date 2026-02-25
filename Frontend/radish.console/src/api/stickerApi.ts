import { apiDelete, apiGet, apiPost, apiPut } from '@radish/http';

export interface StickerGroupVo {
  voId: number;
  voCode: string;
  voName: string;
  voDescription?: string | null;
  voCoverImageUrl?: string | null;
  voGroupType: number;
  voIsEnabled: boolean;
  voSort: number;
  voTenantId: number;
  voStickerCount: number;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface StickerVo {
  voId: number;
  voGroupId: number;
  voCode: string;
  voName: string;
  voImageUrl: string;
  voThumbnailUrl?: string | null;
  voIsAnimated: boolean;
  voAllowInline: boolean;
  voAttachmentId?: number | null;
  voUseCount: number;
  voSort: number;
  voIsEnabled: boolean;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface StickerGroupUpsertRequest {
  name: string;
  code?: string;
  description?: string;
  coverImageUrl?: string;
  groupType: number;
  isEnabled: boolean;
  sort: number;
}

export interface CreateStickerRequest {
  groupId: number;
  code: string;
  name: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: number;
  isEnabled: boolean;
  sort: number;
}

export interface UpdateStickerRequest {
  name: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: number;
  isEnabled: boolean;
  sort: number;
}

export interface BatchAddStickerItemRequest {
  attachmentId: number;
  code: string;
  name: string;
  allowInline: boolean;
}

export interface BatchAddStickersRequest {
  groupId: number;
  stickers: BatchAddStickerItemRequest[];
}

export interface StickerSortItemRequest {
  id: number;
  sort: number;
}

export interface BatchUpdateStickerSortRequest {
  items: StickerSortItemRequest[];
}

export interface StickerCodeCheckVo {
  voAvailable: boolean;
  voCode: string;
  voGroupId?: number;
}

export interface StickerNormalizeCodeVo {
  voOriginalFileName: string;
  voNormalizedCode: string;
  voIsChanged: boolean;
  voChangeReasons: string[];
}

export interface StickerBatchConflictVo {
  voRowIndex: number;
  voCode: string;
  voMessage: string;
}

export interface StickerBatchFailedItemVo {
  voRowIndex: number;
  voAttachmentId: number;
  voCode: string;
  voMessage: string;
}

export interface StickerBatchAddResultVo {
  voGroupId: number;
  voCreatedCount: number;
  voStickerIds: number[];
  voConflicts: StickerBatchConflictVo[];
  voFailedItems: StickerBatchFailedItemVo[];
}

export interface StickerBatchUpdateSortResultVo {
  voUpdatedCount: number;
}

function throwRequestError(message: string, fallback: string): never {
  throw new Error(message || fallback);
}

export async function getAdminStickerGroups(): Promise<StickerGroupVo[]> {
  const response = await apiGet<StickerGroupVo[]>('/api/v1/Sticker/GetAdminGroups', { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '获取表情包分组失败');
  }

  return response.data;
}

export async function createStickerGroup(request: StickerGroupUpsertRequest): Promise<number> {
  const response = await apiPost<number>('/api/v1/Sticker/CreateGroup', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throwRequestError(response.message, '创建表情包分组失败');
  }

  return response.data;
}

export async function updateStickerGroup(id: number, request: StickerGroupUpsertRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateGroup/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '更新表情包分组失败');
  }
}

export async function deleteStickerGroup(id: number): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Sticker/DeleteGroup/${id}`, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '删除表情包分组失败');
  }
}

export async function checkGroupCode(code: string): Promise<StickerCodeCheckVo> {
  const response = await apiGet<StickerCodeCheckVo>(
    `/api/v1/Sticker/CheckGroupCode?code=${encodeURIComponent(code)}`,
    { withAuth: true }
  );

  if (!response.data) {
    throwRequestError(response.message, '分组编码校验失败');
  }

  return response.data;
}

export async function checkStickerCode(groupId: number, code: string): Promise<StickerCodeCheckVo> {
  const response = await apiGet<StickerCodeCheckVo>(
    `/api/v1/Sticker/CheckStickerCode?groupId=${groupId}&code=${encodeURIComponent(code)}`,
    { withAuth: true }
  );

  if (!response.data) {
    throwRequestError(response.message, '表情编码校验失败');
  }

  return response.data;
}

export async function normalizeStickerCode(filename: string): Promise<StickerNormalizeCodeVo> {
  const response = await apiGet<StickerNormalizeCodeVo>(
    `/api/v1/Sticker/NormalizeCode?filename=${encodeURIComponent(filename)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '编码清洗预览失败');
  }

  return response.data;
}

export async function getGroupStickers(groupId: number): Promise<StickerVo[]> {
  const response = await apiGet<StickerVo[]>(`/api/v1/Sticker/GetGroupStickers/${groupId}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '获取分组表情失败');
  }

  return response.data;
}

export async function addSticker(request: CreateStickerRequest): Promise<number> {
  const response = await apiPost<number>('/api/v1/Sticker/AddSticker', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throwRequestError(response.message, '新增表情失败');
  }

  return response.data;
}

export async function updateSticker(id: number, request: UpdateStickerRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateSticker/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '更新表情失败');
  }
}

export async function deleteSticker(id: number): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Sticker/DeleteSticker/${id}`, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '删除表情失败');
  }
}

export async function batchAddStickers(request: BatchAddStickersRequest): Promise<StickerBatchAddResultVo> {
  const response = await apiPost<StickerBatchAddResultVo>('/api/v1/Sticker/BatchAddStickers', request, { withAuth: true });

  if (!response.data) {
    throwRequestError(response.message, '批量新增表情失败');
  }

  return response.data;
}

export async function batchUpdateStickerSort(request: BatchUpdateStickerSortRequest): Promise<StickerBatchUpdateSortResultVo> {
  const response = await apiPut<StickerBatchUpdateSortResultVo>('/api/v1/Sticker/BatchUpdateSort', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '批量更新排序失败');
  }

  return response.data;
}
