import dayjs, { type Dayjs } from 'dayjs';
import {
  type UserExpAnomalyRuleSummaryVo,
  type UserExpDailyLimitSnapshotVo,
  type UserExpGovernanceRecommendationVo,
  type UserExperienceGovernanceActionVo,
} from '@/api/experienceAdminApi';

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

export const EXPERIENCE_TRANSACTION_TYPE_OPTIONS = [
  { label: '点赞相关', value: LIKE_TRANSACTION_FILTER },
  { label: '高亮相关', value: HIGHLIGHT_TRANSACTION_FILTER },
  { label: '管理员调整', value: 'ADMIN_ADJUST' },
  { label: '惩罚扣减', value: 'PENALTY' },
  { label: '发布帖子', value: 'POST_CREATE' },
  { label: '发布评论', value: 'COMMENT_CREATE' },
  { label: '点赞互动', value: 'LIKE_OTHERS' },
  { label: '被点赞', value: 'RECEIVE_LIKE' },
  { label: '高亮奖励', value: 'GOD_COMMENT' },
  { label: '登录奖励', value: 'DAILY_LOGIN' },
];

export function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

export function isFormValidationError(error: unknown): error is { errorFields: unknown[] } {
  return typeof error === 'object' && error !== null && 'errorFields' in error;
}

export function formatStatDate(value: string): string {
  return dayjs(value).format('MM-DD');
}

export function formatFullStatDate(value: string): string {
  return dayjs(value).format('YYYY-MM-DD');
}

export function formatDisplayTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  return dayjs(value).isValid() ? dayjs(value).format('YYYY-MM-DD HH:mm:ss') : value;
}

export function formatTransactionAmount(value: number): string {
  return `${value >= 0 ? '+' : ''}${value}`;
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

export function getRuleSeverityLabel(severity?: UserExpAnomalyRuleSummaryVo['voSeverity']): string {
  switch (severity) {
    case 'freeze-suggest':
      return '冻结建议';
    case 'review':
      return '人工复核';
    case 'observe':
      return '继续观察';
    default:
      return '继续观察';
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
  limits?: UserExpDailyLimitSnapshotVo | null
): string {
  if (!limits?.voDailyLimitEnabled || limit <= 0) {
    return String(value);
  }

  return `${value}/${limit}`;
}
