export type PublicForumDetailLoadState =
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'notFound'; message: string }
  | { kind: 'error'; message: string };

export type PublicForumCategoryLoadState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'notFound'; message: string }
  | { kind: 'error'; message: string };

export type PublicForumReadSectionState = 'loading' | 'ready' | 'empty' | 'error';

const PUBLIC_FORUM_NOT_FOUND_PATTERNS = [
  /帖子不存在/i,
  /已被删除/i,
  /not\s+found/i,
  /\b404\b/,
  /\b410\b/
];

const PUBLIC_FORUM_CATEGORY_NOT_FOUND_PATTERNS = [
  /分类不存在/i,
  /分类.*不可用/i,
  /not\s+found/i,
  /\b404\b/,
  /\b410\b/
];

export function isPublicForumPostNotFoundError(message: string | null | undefined): boolean {
  if (!message) {
    return false;
  }

  return PUBLIC_FORUM_NOT_FOUND_PATTERNS.some((pattern) => pattern.test(message));
}

export function resolvePublicForumDetailLoadState(input: {
  loadingPost: boolean;
  hasPost: boolean;
  postError: string | null;
}): PublicForumDetailLoadState {
  const { loadingPost, hasPost, postError } = input;

  if (loadingPost && !hasPost) {
    return { kind: 'loading' };
  }

  if (hasPost) {
    return { kind: 'ready' };
  }

  if (isPublicForumPostNotFoundError(postError)) {
    return {
      kind: 'notFound',
      message: postError || '帖子不存在或已被删除'
    };
  }

  if (postError) {
    return {
      kind: 'error',
      message: postError
    };
  }

  return { kind: 'loading' };
}

export function isPublicForumCategoryNotFoundError(message: string | null | undefined): boolean {
  if (!message) {
    return false;
  }

  return PUBLIC_FORUM_CATEGORY_NOT_FOUND_PATTERNS.some((pattern) => pattern.test(message));
}

export function resolvePublicForumCategoryLoadState(input: {
  categoryId: number | null;
  loadingCategory: boolean;
  hasCategory: boolean;
  categoryError: string | null;
}): PublicForumCategoryLoadState {
  const { categoryId, loadingCategory, hasCategory, categoryError } = input;

  if (!categoryId) {
    return { kind: 'idle' };
  }

  if (loadingCategory && !hasCategory) {
    return { kind: 'loading' };
  }

  if (hasCategory) {
    return { kind: 'ready' };
  }

  if (isPublicForumCategoryNotFoundError(categoryError)) {
    return {
      kind: 'notFound',
      message: categoryError || '分类不存在或已不可用'
    };
  }

  if (categoryError) {
    return {
      kind: 'error',
      message: categoryError
    };
  }

  return { kind: 'loading' };
}

export function resolvePublicForumReadSectionState(input: {
  loading: boolean;
  error: string | null;
  itemCount: number;
  totalCount?: number;
}): PublicForumReadSectionState {
  const { loading, error, itemCount, totalCount = itemCount } = input;

  if (loading && itemCount === 0) {
    return 'loading';
  }

  if (error && itemCount === 0) {
    return 'error';
  }

  if (!loading && itemCount === 0 && totalCount === 0) {
    return 'empty';
  }

  return 'ready';
}
