import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export interface ModerationPathInput {
  view?: 'cases' | 'appeals';
  appealPublicId?: string | null;
  keyword?: string;
  returnTo?: string | null;
}

export function buildModerationSearchParams(params: ModerationPathInput): URLSearchParams {
  const searchParams = new URLSearchParams();
  const keyword = params.keyword?.trim();
  const returnTo = normalizeConsoleReturnTo(params.returnTo);
  const appealPublicId = params.appealPublicId?.trim();

  if (params.view === 'appeals') {
    searchParams.set('view', 'appeals');
  }

  if (params.view === 'appeals' && appealPublicId) {
    searchParams.set('appeal', appealPublicId);
  }

  if (keyword) {
    searchParams.set('keyword', keyword);
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
