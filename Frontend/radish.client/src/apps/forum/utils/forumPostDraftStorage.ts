import type { LongId } from '@/api/user';

export interface ForumPostDraft {
  title?: string;
  content?: string;
  tags?: string[];
  categoryId?: LongId | null;
  categoryName?: string | null;
  composerMode?: 'markdown' | 'rich';
  isQuestion?: boolean;
  poll?: {
    enabled?: boolean;
    question?: string;
    endTime?: string;
    options?: string[];
  };
  lottery?: {
    enabled?: boolean;
    prizeName?: string;
    prizeDescription?: string;
    drawTime?: string;
    winnerCount?: string;
  };
}

interface ForumPostDraftEnvelope {
  version: 2;
  ownerUserId: string;
  savedAt: number;
  draft: ForumPostDraft;
}

const FORUM_POST_DRAFT_VERSION = 2;
const FORUM_POST_DRAFT_STORAGE_PREFIX = 'radish:forum-post-draft:v2:';

export function getForumPostDraftStorageKey(userId: string): string | null {
  const normalizedUserId = userId.trim();
  return normalizedUserId
    ? `${FORUM_POST_DRAFT_STORAGE_PREFIX}${encodeURIComponent(normalizedUserId)}`
    : null;
}

export function loadForumPostDraft(userId: string): ForumPostDraft | null {
  const storageKey = getForumPostDraftStorageKey(userId);
  if (!storageKey || typeof window === 'undefined') {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return null;
    }

    const envelope = JSON.parse(raw) as Partial<ForumPostDraftEnvelope>;
    if (envelope.version !== FORUM_POST_DRAFT_VERSION ||
        envelope.ownerUserId !== userId.trim() ||
        !envelope.draft ||
        typeof envelope.draft !== 'object') {
      return null;
    }

    return envelope.draft;
  } catch {
    return null;
  }
}

export function saveForumPostDraft(userId: string, draft: ForumPostDraft): void {
  const storageKey = getForumPostDraftStorageKey(userId);
  if (!storageKey || typeof window === 'undefined') {
    return;
  }

  const envelope: ForumPostDraftEnvelope = {
    version: FORUM_POST_DRAFT_VERSION,
    ownerUserId: userId.trim(),
    savedAt: Date.now(),
    draft,
  };
  window.localStorage.setItem(storageKey, JSON.stringify(envelope));
}

export function removeForumPostDraft(userId: string): void {
  const storageKey = getForumPostDraftStorageKey(userId);
  if (!storageKey || typeof window === 'undefined') {
    return;
  }

  window.localStorage.removeItem(storageKey);
}

export function hasMeaningfulForumPostDraft(draft: ForumPostDraft | null): boolean {
  if (!draft) {
    return false;
  }

  return Boolean(
    draft.title?.trim() ||
    draft.content?.trim() ||
    draft.tags?.some((tag) => tag.trim()) ||
    draft.poll?.question?.trim() ||
    draft.poll?.options?.some((option) => option.trim()) ||
    draft.lottery?.prizeName?.trim() ||
    draft.lottery?.prizeDescription?.trim(),
  );
}
