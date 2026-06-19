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

const normalizePostPublicId = (value: string | null | undefined) => {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  return /^pst_[a-f0-9]{32}$/.test(normalized) ? normalized : null;
};

const normalizeUserPublicId = (value: string | null | undefined) => {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  return /^usr_[0-9a-f]{32}$/.test(normalized) ? normalized : null;
};

export const resolvePublicProfileRouteIdentifier = (
  profile: PublicProfileRouteIdentifierSource | null,
  fallbackIdentifier: string,
): string => normalizeUserPublicId(profile?.voPublicId) ?? fallbackIdentifier;

export const resolvePublicProfilePostForumTarget = (
  post: PublicProfilePostForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizePostPublicId(post.voPublicId) ?? String(post.voId),
});

export const resolvePublicProfileCommentForumTarget = (
  comment: PublicProfileCommentForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizePostPublicId(comment.voPostPublicId) ?? String(comment.voPostId),
  commentId: String(comment.voId),
});
