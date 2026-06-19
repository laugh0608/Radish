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

const normalizePostPublicId = (value: string | null | undefined) => {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  return /^pst_[a-f0-9]{32}$/.test(normalized) ? normalized : null;
};

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
