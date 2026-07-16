import { useCallback } from 'react';

interface PublicForumDetailNavigationGuardOptions {
  navigationLocked: boolean;
  onBack: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

export function usePublicForumDetailNavigationGuard({
  navigationLocked,
  onBack,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery,
}: PublicForumDetailNavigationGuardOptions) {
  const handleBackWhileEditorIdle = useCallback(() => {
    if (!navigationLocked) {
      onBack();
    }
  }, [navigationLocked, onBack]);

  const handleOpenAuthorProfileWhileEditorIdle = useCallback((userId: string) => {
    if (!navigationLocked) {
      onOpenAuthorProfile?.(userId);
    }
  }, [navigationLocked, onOpenAuthorProfile]);

  const handleOpenTagWhileEditorIdle = useCallback((tagSlug: string) => {
    if (!navigationLocked) {
      onOpenTag?.(tagSlug);
    }
  }, [navigationLocked, onOpenTag]);

  const handleOpenQuestionWhileEditorIdle = useCallback(() => {
    if (!navigationLocked) {
      onOpenQuestion?.();
    }
  }, [navigationLocked, onOpenQuestion]);

  const handleOpenPollWhileEditorIdle = useCallback(() => {
    if (!navigationLocked) {
      onOpenPoll?.();
    }
  }, [navigationLocked, onOpenPoll]);

  const handleOpenLotteryWhileEditorIdle = useCallback(() => {
    if (!navigationLocked) {
      onOpenLottery?.();
    }
  }, [navigationLocked, onOpenLottery]);

  return {
    handleBackWhileEditorIdle,
    handleOpenAuthorProfileWhileEditorIdle,
    handleOpenTagWhileEditorIdle,
    handleOpenQuestionWhileEditorIdle,
    handleOpenPollWhileEditorIdle,
    handleOpenLotteryWhileEditorIdle,
  };
}
