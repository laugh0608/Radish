import { apiDelete, apiGet, apiPost, apiPut, createApiResponseError } from '@radish/http';

export interface StickerGroupVo {
  voId: string;
  voCode: string;
  voName: string;
  voDescription?: string | null;
  voCoverAttachmentId?: string | null;
  voCoverImageUrl?: string | null;
  voGroupType: number;
  voIsEnabled: boolean;
  voSort: number;
  voTenantId: string;
  voStickerCount: number;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface StickerVo {
  voId: string;
  voGroupId: string;
  voCode: string;
  voName: string;
  voImageUrl: string;
  voThumbnailUrl?: string | null;
  voIsAnimated: boolean;
  voAllowInline: boolean;
  voAttachmentId?: string | null;
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
  coverAttachmentId?: string | null;
  groupType: number;
  isEnabled: boolean;
  sort: number;
}

export interface CreateStickerRequest {
  groupId: string;
  code: string;
  name: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: string | null;
  isEnabled: boolean;
  sort: number;
}

export interface UpdateStickerRequest {
  name: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: string | null;
  isEnabled: boolean;
  sort: number;
}

export interface BatchAddStickerItemRequest {
  attachmentId: string;
  code: string;
  name: string;
  allowInline: boolean;
}

export interface BatchAddStickersRequest {
  groupId: string;
  stickers: BatchAddStickerItemRequest[];
}

export interface StickerSortItemRequest {
  id: string;
  sort: number;
}

export interface BatchUpdateStickerSortRequest {
  items: StickerSortItemRequest[];
}

export interface StickerCodeCheckVo {
  voAvailable: boolean;
  voCode: string;
  voGroupId?: string;
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
  voAttachmentId: string;
  voCode: string;
  voMessage: string;
}

export interface StickerBatchAddResultVo {
  voGroupId: string;
  voCreatedCount: number;
  voStickerIds: string[];
  voConflicts: StickerBatchConflictVo[];
  voFailedItems: StickerBatchFailedItemVo[];
}

export interface StickerBatchUpdateSortResultVo {
  voUpdatedCount: number;
}

export interface BatchAddStickersSubmitResponse {
  ok: boolean;
  statusCode?: number;
  httpStatus?: number;
  code?: string;
  message?: string;
  messageKey?: string;
  messageArguments?: unknown[];
  traceId?: string;
  data?: StickerBatchAddResultVo;
}

export async function getAdminStickerGroups(): Promise<StickerGroupVo[]> {
  const response = await apiGet<StickerGroupVo[]>('/api/v1/Sticker/GetAdminGroups', { withAuth: true });

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取表情包分组失败');
  }

  return response.data;
}

export async function createStickerGroup(request: StickerGroupUpsertRequest): Promise<string> {
  const response = await apiPost<string>('/api/v1/Sticker/CreateGroup', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, '创建表情包分组失败');
  }

  return response.data;
}

export async function updateStickerGroup(id: string, request: StickerGroupUpsertRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateGroup/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throw createApiResponseError(response, '更新表情包分组失败');
  }
}

export async function deleteStickerGroup(id: string): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Sticker/DeleteGroup/${id}`, { withAuth: true });

  if (!response.ok) {
    throw createApiResponseError(response, '删除表情包分组失败');
  }
}

export async function checkGroupCode(code: string): Promise<StickerCodeCheckVo> {
  const response = await apiGet<StickerCodeCheckVo>(
    `/api/v1/Sticker/CheckGroupCode?code=${encodeURIComponent(code)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '分组编码校验失败');
  }

  return response.data;
}

export async function checkStickerCode(groupId: string, code: string): Promise<StickerCodeCheckVo> {
  const response = await apiGet<StickerCodeCheckVo>(
    `/api/v1/Sticker/CheckStickerCode?groupId=${encodeURIComponent(groupId)}&code=${encodeURIComponent(code)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '表情编码校验失败');
  }

  return response.data;
}

export async function normalizeStickerCode(filename: string): Promise<StickerNormalizeCodeVo> {
  const response = await apiGet<StickerNormalizeCodeVo>(
    `/api/v1/Sticker/NormalizeCode?filename=${encodeURIComponent(filename)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '编码清洗预览失败');
  }

  return response.data;
}

export async function getGroupStickers(groupId: string): Promise<StickerVo[]> {
  const response = await apiGet<StickerVo[]>(`/api/v1/Sticker/GetGroupStickers/${groupId}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取分组表情失败');
  }

  return response.data;
}

export async function addSticker(request: CreateStickerRequest): Promise<string> {
  const response = await apiPost<string>('/api/v1/Sticker/AddSticker', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, '新增表情失败');
  }

  return response.data;
}

export async function updateSticker(id: string, request: UpdateStickerRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateSticker/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throw createApiResponseError(response, '更新表情失败');
  }
}

export async function deleteSticker(id: string): Promise<void> {
  const response = await apiDelete<boolean>(`/api/v1/Sticker/DeleteSticker/${id}`, { withAuth: true });

  if (!response.ok) {
    throw createApiResponseError(response, '删除表情失败');
  }
}

export async function batchAddStickers(request: BatchAddStickersRequest): Promise<StickerBatchAddResultVo> {
  const response = await apiPost<StickerBatchAddResultVo>('/api/v1/Sticker/BatchAddStickers', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '批量新增表情失败');
  }

  return response.data;
}

export async function batchAddStickersWithDetails(request: BatchAddStickersRequest): Promise<BatchAddStickersSubmitResponse> {
  const response = await apiPost<StickerBatchAddResultVo>(
    '/api/v1/Sticker/BatchAddStickers',
    request,
    { withAuth: true },
  );
  return {
    ok: response.ok,
    statusCode: response.statusCode,
    httpStatus: response.httpStatus,
    code: response.code,
    message: response.message,
    messageKey: response.messageKey,
    messageArguments: response.messageArguments,
    traceId: response.traceId,
    data: response.data,
  };
}

export async function batchUpdateStickerSort(request: BatchUpdateStickerSortRequest): Promise<StickerBatchUpdateSortResultVo> {
  const response = await apiPut<StickerBatchUpdateSortResultVo>('/api/v1/Sticker/BatchUpdateSort', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '批量更新排序失败');
  }

  return response.data;
}
