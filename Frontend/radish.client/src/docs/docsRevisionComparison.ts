import type {
  WikiAuthorRevisionDetailVo,
  WikiAuthorRevisionHistoryVo,
  WikiAuthorRevisionItemVo,
} from '@radish/http';
import type { LongId } from '@/api/user';

export type RevisionComparisonMode = 'previous' | 'current';

export interface RevisionState {
  history: WikiAuthorRevisionHistoryVo | null;
  revisions: WikiAuthorRevisionItemVo[];
  selectedRevision: WikiAuthorRevisionDetailVo | null;
  selectedRevisionId: LongId | null;
  comparisonMode: RevisionComparisonMode;
  comparisonRevision: WikiAuthorRevisionDetailVo | null;
  comparisonRevisionId: LongId | null;
  loading: boolean;
  loadingDetail: boolean;
  loadingComparison: boolean;
  historyError: string | null;
  detailError: string | null;
  detailStale: boolean;
  comparisonError: string | null;
  comparisonStale: boolean;
  comparisonMissing: boolean;
}

export function isSameLongId(left: LongId | null | undefined, right: LongId | null | undefined): boolean {
  return left != null && right != null && String(left) === String(right);
}

export function findPreviousRevision(
  revisions: WikiAuthorRevisionItemVo[],
  selectedRevisionId: LongId,
): WikiAuthorRevisionItemVo | null {
  const selectedRevision = revisions.find((revision) => isSameLongId(revision.voId, selectedRevisionId));
  if (!selectedRevision) {
    return null;
  }

  return revisions
    .filter((revision) => revision.voVersion < selectedRevision.voVersion)
    .sort((left, right) => right.voVersion - left.voVersion)[0] ?? null;
}
