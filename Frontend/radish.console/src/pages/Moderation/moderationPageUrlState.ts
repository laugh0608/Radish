import type { ContentModerationTargetType } from '@radish/http';
import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export const DEFAULT_MODERATION_PAGE_INDEX = 1;
export const DEFAULT_MODERATION_PAGE_SIZE = 20;
export const MODERATION_TARGET_TYPES = [
  'Post',
  'Comment',
  'PostAnswer',
  'PostQuickReply',
  'ChatMessage',
  'Product',
  'ProductReview',
] as const satisfies readonly ContentModerationTargetType[];

export function parseModerationCasePublicId(value: string | null): string | undefined {
  const trimmed = value?.trim() ?? '';
  return /^mod_[a-zA-Z0-9_-]{1,156}$/u.test(trimmed) ? trimmed : undefined;
}

export function parseModerationAppealPublicId(value: string | null): string | undefined {
  const trimmed = value?.trim() ?? '';
  return /^apl_[a-zA-Z0-9_-]{1,156}$/u.test(trimmed) ? trimmed : undefined;
}

export function parseModerationCaseStatusQuery(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 && parsed <= 2 ? parsed : undefined;
}

export function parseModerationTargetTypeQuery(
  value: string | null,
): ContentModerationTargetType | undefined {
  const normalized = value?.trim() ?? '';
  return MODERATION_TARGET_TYPES.find((targetType) => targetType === normalized);
}

export function parseModerationPageIndexQuery(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

export function parseModerationPageSizeQuery(value: string | null): number | undefined {
  const parsed = parseModerationPageIndexQuery(value);
  return parsed !== undefined && parsed <= 100 ? parsed : undefined;
}

export interface ModerationPathInput {
  view?: 'cases' | 'appeals';
  casePublicId?: string | null;
  appealPublicId?: string | null;
  status?: number;
  targetType?: ContentModerationTargetType;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  returnTo?: string | null;
}

export function buildModerationSearchParams(params: ModerationPathInput): URLSearchParams {
  const searchParams = new URLSearchParams();
  const keyword = params.keyword?.trim();
  const returnTo = normalizeConsoleReturnTo(params.returnTo);
  const casePublicId = params.casePublicId?.trim();
  const appealPublicId = params.appealPublicId?.trim();

  if (params.view === 'appeals') {
    searchParams.set('view', 'appeals');
  }

  if (params.view !== 'appeals' && casePublicId) {
    searchParams.set('case', casePublicId);
  }

  if (params.view === 'appeals' && appealPublicId) {
    searchParams.set('appeal', appealPublicId);
  }

  if (params.view !== 'appeals' && params.status !== undefined && params.status >= 0) {
    searchParams.set('status', String(params.status));
  }

  if (params.view !== 'appeals' && params.targetType) {
    searchParams.set('targetType', params.targetType);
  }

  if (keyword) {
    searchParams.set('keyword', keyword);
  }

  if (
    params.view !== 'appeals'
    && (params.pageIndex ?? DEFAULT_MODERATION_PAGE_INDEX) !== DEFAULT_MODERATION_PAGE_INDEX
  ) {
    searchParams.set('pageIndex', String(params.pageIndex));
  }

  if (
    params.view !== 'appeals'
    && (params.pageSize ?? DEFAULT_MODERATION_PAGE_SIZE) !== DEFAULT_MODERATION_PAGE_SIZE
  ) {
    searchParams.set('pageSize', String(params.pageSize));
  }

  if (returnTo) {
    searchParams.set('returnTo', returnTo);
  }

  return searchParams;
}

export function buildModerationPath(params: ModerationPathInput): string {
  const searchParams = buildModerationSearchParams(params);
  const query = searchParams.toString();

  return query ? `/moderation?${query}` : '/moderation';
}
