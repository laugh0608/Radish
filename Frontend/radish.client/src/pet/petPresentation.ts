import type { PetCareActionState, PetProfile, PetStatLog } from '@/api/pet';
import { getIntlLocale } from '../locales/language.ts';
import { formatDateTimeByTimeZone } from '../utils/dateTime.ts';

export type PetStatKey = 'voSatiety' | 'voCleanliness' | 'voEnergy';
export type PetStatusIntent = 'thriving' | 'hungry' | 'messy' | 'tired' | 'steady';
export type PetStatLevel = 'good' | 'watch' | 'critical';
export type PetActionAvailabilityKind = 'ready' | 'cooldown' | 'usedUp' | 'unavailable';
export type CooldownDisplayUnit = 'minute' | 'hour';

export interface PetStatusInsight {
  intent: PetStatusIntent;
  icon: string;
  titleKey: string;
  descriptionKey: string;
  focusStatKey?: PetStatKey;
}

export interface PetActionAvailability {
  kind: PetActionAvailabilityKind;
  cooldownMs: number;
}

export interface CooldownDisplayParts {
  value: number;
  unit: CooldownDisplayUnit;
}

export interface PetStatDelta {
  statKey: PetStatKey;
  value: number;
}

const actionTranslationKeys: Record<string, string> = {
  feed: 'pet.care.action.feed',
  clean: 'pet.care.action.clean',
  play: 'pet.care.action.play',
  rest: 'pet.care.action.rest',
};

const moodTranslationKeys: Record<string, string> = {
  happy: 'pet.mood.happy',
  calm: 'pet.mood.calm',
  tired: 'pet.mood.tired',
  hungry: 'pet.mood.hungry',
  messy: 'pet.mood.messy',
};

const speciesTranslationKeys: Record<string, string> = {
  radish: 'pet.species.radish',
};

const shapeTranslationKeys: Record<string, string> = {
  sprout: 'pet.shape.sprout',
};

export function formatPetNumber(value: number | string, language?: string): string {
  const locale = getIntlLocale(language);
  if (typeof value === 'number') {
    return Number.isFinite(value) ? new Intl.NumberFormat(locale).format(value) : '-';
  }

  const normalized = value.trim();
  if (!normalized) {
    return '-';
  }

  if (/^-?\d+$/.test(normalized)) {
    return new Intl.NumberFormat(locale).format(BigInt(normalized));
  }

  const numericValue = Number(normalized);
  return Number.isFinite(numericValue) ? new Intl.NumberFormat(locale).format(numericValue) : '-';
}

export function formatPetSignedNumber(value: number | string, language?: string): string {
  const formattedValue = formatPetNumber(value, language);
  const numericValue = typeof value === 'number' ? value : Number(value);
  return numericValue > 0 ? `+${formattedValue}` : formattedValue;
}

export function formatPetDateTime(
  value: string | Date | number | null | undefined,
  timeZoneId: string,
  language?: string,
  fallback: string = '-',
): string {
  return formatDateTimeByTimeZone(value, timeZoneId, fallback, getIntlLocale(language));
}

export function resolvePetGrowthStageTranslationKey(growthStage: number): string {
  if (growthStage <= 1) {
    return 'pet.growthStage.sprout';
  }

  if (growthStage === 2) {
    return 'pet.growthStage.unfurling';
  }

  if (growthStage === 3) {
    return 'pet.growthStage.growing';
  }

  return 'pet.growthStage.mature';
}

export function resolvePetMoodTranslationKey(mood: string): string {
  return moodTranslationKeys[mood] ?? 'pet.mood.calm';
}

export function resolvePetSpeciesTranslationKey(speciesKey: string): string {
  return speciesTranslationKeys[speciesKey] ?? 'pet.species.unknown';
}

export function resolvePetShapeTranslationKey(shapeKey: string): string {
  return shapeTranslationKeys[shapeKey] ?? 'pet.shape.unknown';
}

export function resolvePetActionTranslationKey(actionType: string): string {
  return actionTranslationKeys[actionType] ?? 'pet.care.action.unknown';
}

export function resolvePetLogMessageTranslationKey(actionType: string): string {
  return actionTranslationKeys[actionType]
    ? `pet.logs.message.${actionType}`
    : 'pet.logs.message.unknown';
}

const moodInsightMap: Record<string, PetStatusInsight> = {
  hungry: {
    intent: 'hungry',
    icon: 'mdi:food-apple-outline',
    titleKey: 'pet.insight.hungry.title',
    descriptionKey: 'pet.insight.hungry.description',
    focusStatKey: 'voSatiety',
  },
  messy: {
    intent: 'messy',
    icon: 'mdi:sparkles',
    titleKey: 'pet.insight.messy.title',
    descriptionKey: 'pet.insight.messy.description',
    focusStatKey: 'voCleanliness',
  },
  tired: {
    intent: 'tired',
    icon: 'mdi:sleep',
    titleKey: 'pet.insight.tired.title',
    descriptionKey: 'pet.insight.tired.description',
    focusStatKey: 'voEnergy',
  },
  happy: {
    intent: 'thriving',
    icon: 'mdi:emoticon-happy-outline',
    titleKey: 'pet.insight.thriving.title',
    descriptionKey: 'pet.insight.thriving.description',
  },
  calm: {
    intent: 'steady',
    icon: 'mdi:leaf',
    titleKey: 'pet.insight.steady.title',
    descriptionKey: 'pet.insight.steady.description',
  },
};

export function resolvePetStatusInsight(pet: Pick<PetProfile, 'voMood' | 'voSatiety' | 'voCleanliness' | 'voEnergy'>): PetStatusInsight {
  const moodInsight = moodInsightMap[pet.voMood];
  if (moodInsight) {
    return moodInsight;
  }

  const lowestStat = getLowestPetStat(pet);
  if (lowestStat.value < 30) {
    return lowestStat.key === 'voSatiety'
      ? moodInsightMap.hungry
      : lowestStat.key === 'voCleanliness'
        ? moodInsightMap.messy
        : moodInsightMap.tired;
  }

  return pet.voSatiety >= 75 && pet.voCleanliness >= 75 && pet.voEnergy >= 60
    ? moodInsightMap.happy
    : moodInsightMap.calm;
}

export function resolvePetStatLevel(value: number): PetStatLevel {
  if (value < 30) {
    return 'critical';
  }

  if (value < 60) {
    return 'watch';
  }

  return 'good';
}

export function resolvePetActionAvailability(action: PetCareActionState, nowMs: number): PetActionAvailability {
  if (action.voRemainingToday <= 0) {
    return { kind: 'usedUp', cooldownMs: 0 };
  }

  const nextAvailableAt = parseTime(action.voNextAvailableAt);
  if (nextAvailableAt != null && nextAvailableAt > nowMs) {
    return { kind: 'cooldown', cooldownMs: nextAvailableAt - nowMs };
  }

  if (!action.voCanUse) {
    return { kind: 'unavailable', cooldownMs: 0 };
  }

  return { kind: 'ready', cooldownMs: 0 };
}

export function getCooldownDisplayParts(cooldownMs: number): CooldownDisplayParts | null {
  if (cooldownMs <= 0) {
    return null;
  }

  const minutes = Math.max(1, Math.ceil(cooldownMs / 60000));
  if (minutes < 60) {
    return { value: minutes, unit: 'minute' };
  }

  return { value: Math.ceil(minutes / 60), unit: 'hour' };
}

export function getPetLogStatDeltas(log: PetStatLog): PetStatDelta[] {
  const deltas: PetStatDelta[] = [
    { statKey: 'voSatiety', value: log.voAfterSatiety - log.voBeforeSatiety },
    { statKey: 'voCleanliness', value: log.voAfterCleanliness - log.voBeforeCleanliness },
    { statKey: 'voEnergy', value: log.voAfterEnergy - log.voBeforeEnergy },
  ];
  return deltas.filter(item => item.value !== 0);
}

function getLowestPetStat(pet: Pick<PetProfile, 'voSatiety' | 'voCleanliness' | 'voEnergy'>): { key: PetStatKey; value: number } {
  const stats: Array<{ key: PetStatKey; value: number }> = [
    { key: 'voSatiety', value: pet.voSatiety },
    { key: 'voCleanliness', value: pet.voCleanliness },
    { key: 'voEnergy', value: pet.voEnergy },
  ];
  return stats.reduce((lowest, current) => current.value < lowest.value ? current : lowest);
}

function parseTime(value: string | null | undefined): number | null {
  if (!value) {
    return null;
  }

  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : null;
}
