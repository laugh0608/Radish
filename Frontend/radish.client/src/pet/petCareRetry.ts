import type { ApiResponseError } from '@radish/http';
import type { PetCareActionState, PetCareActionType, PetProfile, PetStatLog } from '@/api/pet';

function isApiResponseError(error: unknown): error is ApiResponseError {
  return error instanceof Error && error.name === 'ApiResponseError';
}

function findCareAction(
  pet: PetProfile | null,
  actionType: PetCareActionType,
): PetCareActionState | null {
  return pet?.voCareActions.find((action) => action.voActionType === actionType) ?? null;
}

export function isAmbiguousPetCareError(error: unknown): boolean {
  if (!isApiResponseError(error)) {
    return true;
  }

  const status = error.httpStatus ?? error.statusCode;
  return status === undefined || status === 0 || status >= 500 || [408, 425, 429].includes(status);
}

export function hasAuthoritativePetCareAdvance(
  beforePet: PetProfile | null,
  beforeLogs: PetStatLog[],
  afterPet: PetProfile | null,
  afterLogs: PetStatLog[],
  actionType: PetCareActionType,
): boolean {
  const previousLogIds = new Set(beforeLogs.map((log) => String(log.voId)));
  if (afterLogs.some((log) => (
    log.voActionType === actionType && !previousLogIds.has(String(log.voId))
  ))) {
    return true;
  }

  const beforeAction = findCareAction(beforePet, actionType);
  const afterAction = findCareAction(afterPet, actionType);
  if (!beforeAction || !afterAction) {
    return false;
  }

  return afterAction.voUsedToday > beforeAction.voUsedToday
    || afterAction.voRemainingToday < beforeAction.voRemainingToday
    || afterAction.voNextAvailableAt !== beforeAction.voNextAvailableAt
    || afterAction.voCanUse !== beforeAction.voCanUse;
}
