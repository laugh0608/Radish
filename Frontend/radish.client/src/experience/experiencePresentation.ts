import type { TFunction } from 'i18next';
import type { ExperienceData, ExpTransactionData } from '@/api/experience';
import type {
  ExperienceBarPresentation,
  ExperienceData as ExperienceBarData,
  ExperienceNumericValue,
} from '@radish/ui/experience-bar';
import { getIntlLocale } from '../locales/language.ts';
import { formatDateTimeByTimeZone } from '../utils/dateTime.ts';

const experienceTypeTranslationKeys: Record<string, string> = {
  POST_CREATE: 'experience.type.POST_CREATE',
  POST_LIKED: 'experience.type.POST_LIKED',
  COMMENT_CREATE: 'experience.type.COMMENT_CREATE',
  COMMENT_LIKED: 'experience.type.COMMENT_LIKED',
  COMMENT_REPLIED: 'experience.type.COMMENT_REPLIED',
  LIKE_OTHERS: 'experience.type.LIKE_OTHERS',
  RECEIVE_LIKE: 'experience.type.RECEIVE_LIKE',
  GIVE_LIKE: 'experience.type.GIVE_LIKE',
  GOD_COMMENT: 'experience.type.GOD_COMMENT',
  SOFA_COMMENT: 'experience.type.SOFA_COMMENT',
  DAILY_LOGIN: 'experience.type.DAILY_LOGIN',
  WEEKLY_LOGIN: 'experience.type.WEEKLY_LOGIN',
  CONTINUOUS_LOGIN: 'experience.type.CONTINUOUS_LOGIN',
  PROFILE_COMPLETE: 'experience.type.PROFILE_COMPLETE',
  FIRST_POST: 'experience.type.FIRST_POST',
  FIRST_COMMENT: 'experience.type.FIRST_COMMENT',
  ADMIN_ADJUST: 'experience.type.ADMIN_ADJUST',
  PENALTY: 'experience.type.PENALTY',
};

export interface ExperienceDailyStat {
  date: string;
  exp: number;
  [key: string]: string | number;
}

export interface ExperienceSourceStat {
  type: string;
  name: string;
  value: number;
  [key: string]: string | number;
}

function normalizeExperienceType(expType: string | null | undefined): string {
  return expType?.trim() ?? '';
}

function normalizeExperienceInteger(value: ExperienceNumericValue): string | null {
  const normalized = String(value).trim();
  return /^-?\d+$/.test(normalized) ? normalized : null;
}

function getPluralCount(value: ExperienceNumericValue): number {
  return normalizeExperienceInteger(value) === '1' ? 1 : 2;
}

function getDateParts(value: Date, timeZone: string): { year: number; month: number; day: number } {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(value);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));

  return {
    year: Number(values.year),
    month: Number(values.month),
    day: Number(values.day),
  };
}

function formatDateKey(parts: { year: number; month: number; day: number }): string {
  return `${parts.year}-${String(parts.month).padStart(2, '0')}-${String(parts.day).padStart(2, '0')}`;
}

function shiftDateKey(dateKey: string, offsetDays: number): string {
  const [year, month, day] = dateKey.split('-').map(Number);
  const shifted = new Date(Date.UTC(year, month - 1, day + offsetDays, 12));
  return formatDateKey({
    year: shifted.getUTCFullYear(),
    month: shifted.getUTCMonth() + 1,
    day: shifted.getUTCDate(),
  });
}

function formatChartDate(dateKey: string, language?: string): string {
  const [year, month, day] = dateKey.split('-').map(Number);
  return new Intl.DateTimeFormat(getIntlLocale(language), {
    timeZone: 'UTC',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(Date.UTC(year, month - 1, day, 12)));
}

export function resolveExperienceTypeTranslationKey(expType: string): string | null {
  return experienceTypeTranslationKeys[normalizeExperienceType(expType)] ?? null;
}

export function formatExperienceType(expType: string, t: TFunction): string {
  const normalizedType = normalizeExperienceType(expType);
  const translationKey = resolveExperienceTypeTranslationKey(normalizedType);
  return translationKey ? t(translationKey) : normalizedType;
}

export function formatExperienceNumber(value: ExperienceNumericValue, language?: string): string {
  const integerValue = normalizeExperienceInteger(value);
  if (integerValue) {
    return new Intl.NumberFormat(getIntlLocale(language)).format(BigInt(integerValue));
  }

  const numericValue = Number(value);
  return Number.isFinite(numericValue)
    ? new Intl.NumberFormat(getIntlLocale(language)).format(numericValue)
    : '-';
}

export function formatExperienceSignedNumber(value: ExperienceNumericValue, language?: string): string {
  const formattedValue = formatExperienceNumber(value, language);
  return Number(value) > 0 ? `+${formattedValue}` : formattedValue;
}

export function formatExperiencePercent(value: number, language?: string): string {
  return new Intl.NumberFormat(getIntlLocale(language), {
    style: 'percent',
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value);
}

export function formatExperienceDateTime(
  value: string | number | Date | null | undefined,
  timeZone: string,
  language?: string,
  fallback = '-',
): string {
  return formatDateTimeByTimeZone(value, timeZone, fallback, getIntlLocale(language));
}

export function addExperienceValues(
  left: ExperienceNumericValue,
  right: ExperienceNumericValue,
): string {
  const leftInteger = normalizeExperienceInteger(left);
  const rightInteger = normalizeExperienceInteger(right);
  if (leftInteger && rightInteger) {
    return (BigInt(leftInteger) + BigInt(rightInteger)).toString();
  }

  return String(Number(left) + Number(right));
}

export function buildExperienceBarData(experience: ExperienceData): ExperienceBarData {
  return {
    ...experience,
    voNextLevelExp: addExperienceValues(experience.voCurrentExp, experience.voExpToNextLevel),
  };
}

export function createExperienceBarPresentation(
  t: TFunction,
  language: string,
  timeZone: string,
): ExperienceBarPresentation {
  return {
    labels: {
      rank: (rank) => t('experience.bar.rank', { rank }),
      currentProgress: t('experience.bar.currentProgress'),
      nextLevel: t('experience.bar.nextLevel'),
      remainingExperience: (value) => t('experience.bar.remainingExperience', {
        count: getPluralCount(value),
        value,
      }),
      totalExperience: t('experience.bar.totalExperience'),
      frozen: t('experience.bar.frozen'),
      frozenUntil: (value) => t('experience.bar.frozenUntil', { value }),
      frozenReason: (value) => t('experience.bar.frozenReason', { value }),
      lastLevelUp: (value) => t('experience.bar.lastLevelUp', { value }),
    },
    formatNumber: (value) => formatExperienceNumber(value, language),
    formatPercentage: (value) => formatExperiencePercent(value, language),
    formatDateTime: (value) => formatExperienceDateTime(value, timeZone, language),
  };
}

export function buildExperienceDailyStats(
  transactions: ExpTransactionData[],
  days: number,
  now: Date,
  language: string,
  timeZone: string,
): ExperienceDailyStat[] {
  const safeDays = Math.max(1, Math.floor(days));
  const todayKey = formatDateKey(getDateParts(now, timeZone));
  const stats = new Map<string, number>();

  for (let offset = safeDays - 1; offset >= 0; offset -= 1) {
    stats.set(shiftDateKey(todayKey, -offset), 0);
  }

  for (const transaction of transactions) {
    const transactionDate = new Date(transaction.voCreateTime);
    if (Number.isNaN(transactionDate.getTime())) {
      continue;
    }

    const transactionKey = formatDateKey(getDateParts(transactionDate, timeZone));
    if (stats.has(transactionKey)) {
      stats.set(transactionKey, (stats.get(transactionKey) ?? 0) + transaction.voExpAmount);
    }
  }

  return [...stats.entries()].map(([dateKey, exp]) => ({
    date: formatChartDate(dateKey, language),
    exp,
  }));
}

export function buildExperienceSourceStats(
  transactions: ExpTransactionData[],
  t: TFunction,
): ExperienceSourceStat[] {
  const sources = new Map<string, number>();

  for (const transaction of transactions) {
    const type = normalizeExperienceType(transaction.voExpType);
    if (!type) {
      continue;
    }

    sources.set(type, (sources.get(type) ?? 0) + transaction.voExpAmount);
  }

  return [...sources.entries()].map(([type, value]) => ({
    type,
    name: formatExperienceType(type, t),
    value,
  }));
}
