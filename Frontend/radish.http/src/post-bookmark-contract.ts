export const PostBookmarkErrorCode = {
  AuthenticationRequired: 'PostBookmark.AuthenticationRequired',
  PostNotFound: 'PostBookmark.PostNotFound',
  PostUnavailable: 'PostBookmark.PostUnavailable',
  UserUnavailable: 'PostBookmark.UserUnavailable',
  StateConflict: 'PostBookmark.StateConflict',
} as const;

export type PostBookmarkErrorCodeValue =
  typeof PostBookmarkErrorCode[keyof typeof PostBookmarkErrorCode];

export type PostBookmarkTargetStatus = 'Available' | 'Unavailable';

export interface SetPostBookmarkStateRequest {
  postIdentifier: string;
  isBookmarked: boolean;
}

export interface RemovePostBookmarkRequest {
  bookmarkIdentifier: string;
}

export interface GetMyPostBookmarksRequest {
  pageIndex?: number;
  pageSize?: number;
}

export interface PostBookmarkStateVo {
  voBookmarkPublicId?: string | null;
  voPostPublicId: string;
  voIsBookmarked: boolean;
  voCollectCount: number;
  voBookmarkedAt?: string | null;
}

export interface PostBookmarkRemoveVo {
  voBookmarkPublicId: string;
  voRemoved: boolean;
}

export interface UserPostBookmarkTagVo {
  voName: string;
  voSlug: string;
}

export interface UserPostBookmarkVo {
  voBookmarkPublicId: string;
  voBookmarkedAt: string;
  voTargetStatus: PostBookmarkTargetStatus;
  voPostPublicId?: string | null;
  voTitle?: string | null;
  voSummary?: string | null;
  voAuthorPublicId?: string | null;
  voAuthorName?: string | null;
  voPublishTime?: string | null;
  voCategoryName?: string | null;
  voTags: UserPostBookmarkTagVo[];
  voCoverAttachmentId?: string | null;
  voViewCount?: number | null;
  voLikeCount?: number | null;
  voCommentCount?: number | null;
  voCollectCount?: number | null;
}

export interface UserPostBookmarkPageVo {
  voItems: UserPostBookmarkVo[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}
