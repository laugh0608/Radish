import { apiDelete, apiFetch, apiGet, apiPost, apiPut } from '@radish/http';

export interface StickerGroupVo {
  voId: string;
  voCode: string;
  voName: string;
  voDescription?: string | null;
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
  coverImageUrl?: string;
  groupType: number;
  isEnabled: boolean;
  sort: number;
}

export interface CreateStickerRequest {
  groupId: string;
  code: string;
  name: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: string;
  isEnabled: boolean;
  sort: number;
}

export interface UpdateStickerRequest {
  name: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  isAnimated: boolean;
  allowInline: boolean;
  attachmentId?: string;
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
  code?: string;
  message?: string;
  data?: StickerBatchAddResultVo;
}

interface RawMessageModel<T> {
  isSuccess?: boolean;
  IsSuccess?: boolean;
  statusCode?: number;
  StatusCode?: number;
  code?: string;
  Code?: string;
  messageInfo?: string;
  MessageInfo?: string;
  responseData?: T;
  ResponseData?: T;
}

function throwRequestError(message: string, fallback: string): never {
  throw new Error(message || fallback);
}

function readRawField<T>(payload: RawMessageModel<unknown> | null, camelKey: string, pascalKey: string): T | undefined {
  if (!payload) {
    return undefined;
  }

  const camelValue = (payload as Record<string, unknown>)[camelKey];
  if (camelValue !== undefined) {
    return camelValue as T;
  }

  const pascalValue = (payload as Record<string, unknown>)[pascalKey];
  if (pascalValue !== undefined) {
    return pascalValue as T;
  }

  return undefined;
}

export async function getAdminStickerGroups(): Promise<StickerGroupVo[]> {
  const response = await apiGet<StickerGroupVo[]>('/api/v1/Sticker/GetAdminGroups', { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '获取表情包分组失败');
  }

  return response.data;
}

export async function createStickerGroup(request: StickerGroupUpsertRequest): Promise<string> {
  const response = await apiPost<string>('/api/v1/Sticker/CreateGroup', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throwRequestError(response.message, '创建表情包分组失败');
  }

  return response.data;
}

export async function updateStickerGroup(id: string, request: StickerGroupUpsertRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateGroup/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '更新表情包分组失败');
  }
}

export async function deleteStickerGroup(id: string): Promise<void> {
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

export async function checkStickerCode(groupId: string, code: string): Promise<StickerCodeCheckVo> {
  const response = await apiGet<StickerCodeCheckVo>(
    `/api/v1/Sticker/CheckStickerCode?groupId=${encodeURIComponent(groupId)}&code=${encodeURIComponent(code)}`,
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

export async function getGroupStickers(groupId: string): Promise<StickerVo[]> {
  const response = await apiGet<StickerVo[]>(`/api/v1/Sticker/GetGroupStickers/${groupId}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '获取分组表情失败');
  }

  return response.data;
}

export async function addSticker(request: CreateStickerRequest): Promise<string> {
  const response = await apiPost<string>('/api/v1/Sticker/AddSticker', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throwRequestError(response.message, '新增表情失败');
  }

  return response.data;
}

export async function updateSticker(id: string, request: UpdateStickerRequest): Promise<void> {
  const response = await apiPut<boolean>(`/api/v1/Sticker/UpdateSticker/${id}`, request, { withAuth: true });

  if (!response.ok) {
    throwRequestError(response.message, '更新表情失败');
  }
}

export async function deleteSticker(id: string): Promise<void> {
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

export async function batchAddStickersWithDetails(request: BatchAddStickersRequest): Promise<BatchAddStickersSubmitResponse> {
  const response = await apiFetch('/api/v1/Sticker/BatchAddStickers', {
    method: 'POST',
    withAuth: true,
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  });

  let payload: RawMessageModel<StickerBatchAddResultVo> | null = null;
  try {
    payload = (await response.json()) as RawMessageModel<StickerBatchAddResultVo>;
  } catch {
    return {
      ok: false,
      statusCode: response.status,
      message: `HTTP ${response.status} ${response.statusText}`,
    };
  }

  const ok = Boolean(readRawField<boolean>(payload, 'isSuccess', 'IsSuccess'));
  const data = readRawField<StickerBatchAddResultVo>(payload, 'responseData', 'ResponseData');
  const code = readRawField<string>(payload, 'code', 'Code');
  const message = readRawField<string>(payload, 'messageInfo', 'MessageInfo')
    || `HTTP ${response.status} ${response.statusText}`;
  const statusCode = readRawField<number>(payload, 'statusCode', 'StatusCode') ?? response.status;

  return {
    ok,
    statusCode,
    code,
    message,
    data,
  };
}

export async function batchUpdateStickerSort(request: BatchUpdateStickerSortRequest): Promise<StickerBatchUpdateSortResultVo> {
  const response = await apiPut<StickerBatchUpdateSortResultVo>('/api/v1/Sticker/BatchUpdateSort', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throwRequestError(response.message, '批量更新排序失败');
  }

  return response.data;
}
