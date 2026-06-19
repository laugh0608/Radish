import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export type ModerationConsoleSection = 'queue' | 'manual' | 'logs';

const MODERATION_SECTIONS = new Set<ModerationConsoleSection>(['queue', 'manual', 'logs']);

export interface ModerationPathInput {
  section?: ModerationConsoleSection;
  targetUserId?: string;
  sourceReportId?: string;
  returnTo?: string | null;
}

export function parseModerationLongIdQuery(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/u.test(trimmed) ? trimmed : undefined;
}

export function parseModerationSectionQuery(value: string | null): ModerationConsoleSection | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim();
  return MODERATION_SECTIONS.has(normalized as ModerationConsoleSection)
    ? normalized as ModerationConsoleSection
    : undefined;
}

export function buildModerationSearchParams(params: ModerationPathInput): URLSearchParams {
  const searchParams = new URLSearchParams();
  const targetUserId = parseModerationLongIdQuery(params.targetUserId ?? null);
  const sourceReportId = parseModerationLongIdQuery(params.sourceReportId ?? null);
  const returnTo = normalizeConsoleReturnTo(params.returnTo);

  if (params.section) {
    searchParams.set('section', params.section);
  }

  if (targetUserId) {
    searchParams.set('targetUserId', targetUserId);
  }

  if (sourceReportId) {
    searchParams.set('sourceReportId', sourceReportId);
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
