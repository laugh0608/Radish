import {
  apiGet,
  apiPost,
  apiPut,
  configureApiClient,
  createApiResponseError,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';
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

function ensureOk<T>(response: ParsedApiResponse<T>, fallbackMessage: string): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

export async function getMyPet(t: TFunction): Promise<PetProfile | null> {
  const response = await apiGet<PetProfile | null>('/api/v1/Pet/GetMy', { withAuth: true });
  return ensureOk(response, t('pet.error.load'));
}

export async function claimPet(request: ClaimPetRequest, t: TFunction): Promise<PetProfile> {
  const response = await apiPost<PetProfile>('/api/v1/Pet/Claim', request, { withAuth: true });
  return ensureOk(response, t('pet.error.claim'));
}

export async function updatePetProfile(request: UpdatePetProfileRequest, t: TFunction): Promise<PetProfile> {
  const response = await apiPut<PetProfile>('/api/v1/Pet/UpdateProfile', request, { withAuth: true });
  return ensureOk(response, t('pet.error.save'));
}

export async function carePet(request: CarePetRequest, t: TFunction): Promise<PetCareResult> {
  const response = await apiPost<PetCareResult>('/api/v1/Pet/Care', request, { withAuth: true });
  return ensureOk(response, t('pet.error.care'));
}

export async function getPetLogs(pageIndex: number, pageSize: number, t: TFunction): Promise<VoPagedResult<PetStatLog>> {
  const response = await apiGet<VoPagedResult<PetStatLog>>(
    `/api/v1/Pet/GetLogs?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );
  return ensureOk(response, t('pet.error.logs'));
}
