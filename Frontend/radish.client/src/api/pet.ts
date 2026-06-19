import { apiGet, apiPost, apiPut, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type { LongId, VoPagedResult } from '@/api/user';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type PetCareActionType = 'feed' | 'clean' | 'play' | 'rest' | string;

export interface PetCareActionState {
  voActionType: PetCareActionType;
  voActionName: string;
  voDailyLimit: number;
  voUsedToday: number;
  voRemainingToday: number;
  voNextAvailableAt?: string | null;
  voCanUse: boolean;
}

export interface PetProfile {
  voId: LongId;
  voPublicId: string;
  voUserId: LongId;
  voName: string;
  voSpeciesKey: string;
  voShapeKey: string;
  voGrowthStage: number;
  voGrowthStageName: string;
  voMood: string;
  voMoodDisplay: string;
  voSatiety: number;
  voCleanliness: number;
  voEnergy: number;
  voGrowthValue: number | string;
  voEquippedBackgroundKey?: string | null;
  voEquippedToyKey?: string | null;
  voIsPublic: boolean;
  voLastCareTime?: string | null;
  voCreateTime: string;
  voCareActions: PetCareActionState[];
}

export interface PetStatLog {
  voId: LongId;
  voPetProfileId: LongId;
  voPetPublicId: string;
  voActionType: PetCareActionType;
  voActionName: string;
  voBeforeSatiety: number;
  voAfterSatiety: number;
  voBeforeCleanliness: number;
  voAfterCleanliness: number;
  voBeforeEnergy: number;
  voAfterEnergy: number;
  voGrowthDelta: number | string;
  voMessage: string;
  voCreateTime: string;
}

export interface PetCareResult {
  voPet: PetProfile;
  voLog: PetStatLog;
  voMessage: string;
}

export interface ClaimPetRequest {
  name?: string;
}

export interface UpdatePetProfileRequest {
  name?: string;
  isPublic?: boolean;
}

export interface CarePetRequest {
  actionType: PetCareActionType;
  idempotencyKey?: string;
}

function createRequestError(message: string | undefined, fallback: string): Error {
  return new Error(message?.trim() || fallback);
}

export async function getMyPet(): Promise<PetProfile | null> {
  const response = await apiGet<PetProfile | null>('/api/v1/Pet/GetMy', { withAuth: true });
  if (!response.ok) {
    throw createRequestError(response.message, '加载宠物状态失败');
  }

  return response.data ?? null;
}

export async function claimPet(request: ClaimPetRequest): Promise<PetProfile> {
  const response = await apiPost<PetProfile>('/api/v1/Pet/Claim', request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw createRequestError(response.message, '领取宠物失败');
  }

  return response.data;
}

export async function updatePetProfile(request: UpdatePetProfileRequest): Promise<PetProfile> {
  const response = await apiPut<PetProfile>('/api/v1/Pet/UpdateProfile', request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw createRequestError(response.message, '更新宠物资料失败');
  }

  return response.data;
}

export async function carePet(request: CarePetRequest): Promise<PetCareResult> {
  const response = await apiPost<PetCareResult>('/api/v1/Pet/Care', request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw createRequestError(response.message, '照顾宠物失败');
  }

  return response.data;
}

export async function getPetLogs(pageIndex = 1, pageSize = 10): Promise<VoPagedResult<PetStatLog>> {
  const response = await apiGet<VoPagedResult<PetStatLog>>(
    `/api/v1/Pet/GetLogs?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createRequestError(response.message, '加载宠物流水失败');
  }

  return response.data;
}
