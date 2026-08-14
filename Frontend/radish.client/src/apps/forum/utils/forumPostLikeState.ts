import type { LongId } from '@/api/user';

const STORAGE_KEY = 'forum_liked_posts';

const readLikedPostIds = (): Set<string> => {
  if (typeof window === 'undefined') {
    return new Set();
  }
  try {
    const storedPostIds: unknown = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || '[]');
    return new Set(Array.isArray(storedPostIds) ? storedPostIds.map(String) : []);
  } catch {
    return new Set();
  }
};

export const isForumPostLiked = (postId: LongId): boolean => (
  readLikedPostIds().has(String(postId))
);

export const writeForumPostLikedState = (postId: LongId, isLiked: boolean): void => {
  if (typeof window === 'undefined') {
    return;
  }
  const likedPostIds = readLikedPostIds();
  if (isLiked) {
    likedPostIds.add(String(postId));
  } else {
    likedPostIds.delete(String(postId));
  }
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify([...likedPostIds]));
};
