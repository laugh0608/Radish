interface PublicProfilePostForumTargetSource {
  voId: string;
  voPublicId?: string | null;
}

interface PublicProfileCommentForumTargetSource {
  voId: string;
  voPostId: string;
  voPostPublicId?: string | null;
}

interface PublicProfileForumTarget {
  postId: string;
  commentId?: string;
}

const normalizeRouteId = (value: string | null | undefined) => {
  if (typeof value !== 'string') {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

export const resolvePublicProfilePostForumTarget = (
  post: PublicProfilePostForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizeRouteId(post.voPublicId) ?? String(post.voId),
});

export const resolvePublicProfileCommentForumTarget = (
  comment: PublicProfileCommentForumTargetSource,
): PublicProfileForumTarget => ({
  postId: normalizeRouteId(comment.voPostPublicId) ?? String(comment.voPostId),
  commentId: String(comment.voId),
});
