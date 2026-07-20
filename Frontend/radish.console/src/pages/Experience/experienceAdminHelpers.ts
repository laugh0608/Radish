import type { Dayjs } from 'dayjs';
import type { TFunction } from 'i18next';
import {
  type UserExpAnomalyRuleSummaryVo,
  type UserExpDailyLimitSnapshotVo,
  type UserExpGovernanceRecommendationVo,
  type UserExperienceGovernanceActionVo,
} from '@/api/experienceAdminApi';
import {
  formatConsoleDate,
  formatConsoleDateTime,
  formatConsoleInteger,
  formatConsoleMonthDay,
  formatConsoleSignedInteger,
  formatConsoleWeekday,
} from '@/utils/localeFormatters';

export interface FreezeFormValues {
  userId: string;
  reason: string;
  frozenUntil?: Dayjs;
}

export interface AdjustFormValues {
  userId: string;
  deltaExp: number;
  reason?: string;
}

export type GovernanceReviewResult = 'NoIssue' | 'Observe' | 'FreezeSuggest';
export type StatsWindowDays = 7 | 30;

export interface GovernanceReviewFormValues {
  reviewResult: GovernanceReviewResult;
  remark: string;
}

export interface GovernanceReviewDraftContext {
  windowDays?: number;
  statDate?: string | null;
  ruleCodes: string[];
  ruleLabels: string[];
  recommendationLevel?: UserExpGovernanceRecommendationVo['voLevel'];
  recommendationReason?: string | null;
  hint: string;
}

const LIKE_TRANSACTION_FILTER = 'RECEIVE_LIKE,GIVE_LIKE';
const HIGHLIGHT_TRANSACTION_FILTER = 'GOD_COMMENT,SOFA_COMMENT';

const LIKE_RELATED_OBSERVATION_RULE_CODES = new Set([
  'LIKE_LIMIT_PRESSURE',
  'LIKE_LIMIT_NEAR',
  'LIKE_LIMIT_HIT',
  'LIKE_SHARE_HEAVY',
]);

const HIGHLIGHT_RELATED_OBSERVATION_RULE_CODES = new Set([
  'HIGHLIGHT_LIMIT_PRESSURE',
  'HIGHLIGHT_LIMIT_NEAR',
  'HIGHLIGHT_LIMIT_HIT',
  'HIGHLIGHT_REWARD_CLUSTERED',
]);

export const EXPERIENCE_TRANSACTION_TYPES = [
  LIKE_TRANSACTION_FILTER,
  HIGHLIGHT_TRANSACTION_FILTER,
  'ADMIN_ADJUST',
  'PENALTY',
  'POST_CREATE',
  'COMMENT_CREATE',
  'LIKE_OTHERS',
  'RECEIVE_LIKE',
  'GOD_COMMENT',
  'DAILY_LOGIN',
] as const;

export function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

export function isFormValidationError(error: unknown): error is { errorFields: unknown[] } {
  return typeof error === 'object' && error !== null && 'errorFields' in error;
}

export function formatStatDate(value: string, language?: string): string {
  return formatConsoleMonthDay(value, language);
}

export function formatStatWeekday(value: string, language?: string): string {
  return formatConsoleWeekday(value, language);
}

export function formatFullStatDate(value: string, language?: string): string {
  return formatConsoleDate(value, language);
}

export function formatDisplayTime(value?: string | null, language?: string): string {
  return formatConsoleDateTime(value, language);
}

export function formatTransactionAmount(value: number, language?: string): string {
  return formatConsoleSignedInteger(value, language);
}

export function getRecommendationTagColor(level?: UserExpGovernanceRecommendationVo['voLevel']): 'success' | 'warning' | 'error' | 'default' {
  switch (level) {
    case 'freeze-suggest':
      return 'error';
    case 'review':
      return 'warning';
    case 'normal':
      return 'success';
    default:
      return 'default';
  }
}

export function getRuleSeverityTagColor(severity?: UserExpAnomalyRuleSummaryVo['voSeverity']): 'success' | 'warning' | 'error' | 'default' {
  switch (severity) {
    case 'freeze-suggest':
      return 'error';
    case 'review':
      return 'warning';
    case 'observe':
      return 'default';
    default:
      return 'default';
  }
}

export function getRuleSeverityLabel(t: TFunction, severity?: UserExpAnomalyRuleSummaryVo['voSeverity']): string {
  switch (severity) {
    case 'freeze-suggest':
      return t('experience.severity.freezeSuggest');
    case 'review':
      return t('experience.severity.review');
    case 'observe':
      return t('experience.severity.observe');
    default:
      return t('experience.severity.observe');
  }
}

export function getGovernanceReviewResultTagColor(result?: UserExperienceGovernanceActionVo['voReviewResult']): 'success' | 'warning' | 'error' | 'default' {
  switch (result) {
    case 'NoIssue':
      return 'success';
    case 'FreezeSuggest':
      return 'error';
    case 'Observe':
      return 'warning';
    default:
      return 'default';
  }
}

export function getGovernanceActionTagColor(actionType?: UserExperienceGovernanceActionVo['voActionType']): 'success' | 'warning' | 'error' | 'processing' | 'default' {
  switch (actionType) {
    case 'Review':
      return 'processing';
    case 'Freeze':
      return 'error';
    case 'Unfreeze':
      return 'success';
    default:
      return 'default';
  }
}

export function getGovernanceReviewResultForRecommendationLevel(
  level?: UserExpGovernanceRecommendationVo['voLevel']
): GovernanceReviewResult {
  switch (level) {
    case 'freeze-suggest':
      return 'FreezeSuggest';
    case 'normal':
      return 'NoIssue';
    case 'review':
    default:
      return 'Observe';
  }
}

export function getGovernanceReviewResultForRuleSeverity(
  severity?: UserExpAnomalyRuleSummaryVo['voSeverity']
): GovernanceReviewResult {
  return severity === 'freeze-suggest' ? 'FreezeSuggest' : 'Observe';
}

export function getTransactionExpTypePresetForRuleCodes(ruleCodes: string[]): string | undefined {
  const hasLikeRule = ruleCodes.some((ruleCode) => LIKE_RELATED_OBSERVATION_RULE_CODES.has(ruleCode));
  const hasHighlightRule = ruleCodes.some((ruleCode) => HIGHLIGHT_RELATED_OBSERVATION_RULE_CODES.has(ruleCode));

  if (hasLikeRule && !hasHighlightRule) {
    return LIKE_TRANSACTION_FILTER;
  }

  if (!hasLikeRule && hasHighlightRule) {
    return HIGHLIGHT_TRANSACTION_FILTER;
  }

  return undefined;
}

export function formatLimitValue(
  value: number,
  limit: number,
  limits?: UserExpDailyLimitSnapshotVo | null,
  language?: string,
): string {
  if (!limits?.voDailyLimitEnabled || limit <= 0) {
    return formatConsoleInteger(value, language);
  }

  return `${formatConsoleInteger(value, language)}/${formatConsoleInteger(limit, language)}`;
}
