export type PublicForumDetailLoadState =
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'notFound' }
  | { kind: 'error'; message: string };

export type PublicForumCategoryLoadState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'notFound' }
  | { kind: 'error'; message: string };

export type PublicForumTagLoadState =
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'notFound' }
  | { kind: 'error'; message: string };

export type PublicForumReadSectionState = 'loading' | 'ready' | 'empty' | 'error';

export function resolvePublicForumDetailLoadState(input: {
  loadingPost: boolean;
  hasPost: boolean;
  postError: string | null;
  postNotFound: boolean;
}): PublicForumDetailLoadState {
  const { loadingPost, hasPost, postError, postNotFound } = input;

  if (loadingPost && !hasPost) {
    return { kind: 'loading' };
  }

  if (hasPost) {
    return { kind: 'ready' };
  }

  if (postNotFound) {
    return { kind: 'notFound' };
  }

  if (postError) {
    return {
      kind: 'error',
      message: postError
    };
  }

  return { kind: 'loading' };
}

export function resolvePublicForumCategoryLoadState(input: {
  categoryId: string | null;
  loadingCategory: boolean;
  hasCategory: boolean;
  categoryError: string | null;
  categoryNotFound: boolean;
}): PublicForumCategoryLoadState {
  const { categoryId, loadingCategory, hasCategory, categoryError, categoryNotFound } = input;

  if (!categoryId) {
    return { kind: 'idle' };
  }

  if (loadingCategory && !hasCategory) {
    return { kind: 'loading' };
  }

  if (hasCategory) {
    return { kind: 'ready' };
  }

  if (categoryNotFound) {
    return { kind: 'notFound' };
  }

  if (categoryError) {
    return {
      kind: 'error',
      message: categoryError
    };
  }

  return { kind: 'loading' };
}

export function resolvePublicForumTagLoadState(input: {
  loadingTag: boolean;
  hasTag: boolean;
  tagError: string | null;
  tagNotFound: boolean;
}): PublicForumTagLoadState {
  const { loadingTag, hasTag, tagError, tagNotFound } = input;

  if (loadingTag && !hasTag) {
    return { kind: 'loading' };
  }

  if (hasTag) {
    return { kind: 'ready' };
  }

  if (tagNotFound) {
    return { kind: 'notFound' };
  }

  if (tagError) {
    return {
      kind: 'error',
      message: tagError
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
