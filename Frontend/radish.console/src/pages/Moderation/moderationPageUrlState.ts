import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export interface ModerationPathInput {
  keyword?: string;
  returnTo?: string | null;
}

export function buildModerationSearchParams(params: ModerationPathInput): URLSearchParams {
  const searchParams = new URLSearchParams();
  const keyword = params.keyword?.trim();
  const returnTo = normalizeConsoleReturnTo(params.returnTo);

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
