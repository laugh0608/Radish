export type ProfileAuthorityState = 'loading' | 'ready' | 'unavailable' | 'stale';

export interface ProfileStats {
  voPostCount: number;
  voCommentCount: number;
  voTotalLikeCount: number;
  voPostLikeCount: number;
  voCommentLikeCount: number;
}

export function resolveFailedSnapshotState(hasSnapshot: boolean): ProfileAuthorityState {
  return hasSnapshot ? 'stale' : 'unavailable';
}
