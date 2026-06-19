import { normalizePublicPostId, normalizePublicUserId } from '../publicId.ts';

interface PublicProfilePostForumTargetSource {
  voId: string;
  voPublicId?: string | null;
}

interface PublicProfileCommentForumTargetSource {
  voId: string;
  voPostId: string;
  voPostPublicId?: string | null;
}

interface PublicProfileRouteIdentifierSource {
  voPublicId?: string | null;
}

interface PublicProfileForumTarget {
  postId: string;
  commentId?: string;
}

export const resolvePublicProfileRouteIdentifier = (
  profile: PublicProfileRouteIdentifierSource | null,
  fallbackIdentifier: string,
): string => normalizePublicUserId(profile?.voPublicId) ?? fallbackIdentifier;

export const resolvePublicProfilePostForumTarget = (
  post: PublicProfilePostForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizePublicPostId(post.voPublicId) ?? String(post.voId),
});

export const resolvePublicProfileCommentForumTarget = (
  comment: PublicProfileCommentForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizePublicPostId(comment.voPostPublicId) ?? String(comment.voPostId),
  commentId: String(comment.voId),
});
