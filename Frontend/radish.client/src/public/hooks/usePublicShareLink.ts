import { useCallback, useEffect, useState } from 'react';
import { copyToClipboard } from '@/utils/clipboard';

export type PublicShareState = 'idle' | 'success' | 'error';

interface UsePublicShareLinkOptions {
  buildShareUrl: () => string;
  resetDelayMs?: number;
}

export function usePublicShareLink({
  buildShareUrl,
  resetDelayMs = 2200,
}: UsePublicShareLinkOptions) {
  const [shareBusy, setShareBusy] = useState(false);
  const [shareState, setShareState] = useState<PublicShareState>('idle');

  useEffect(() => {
    if (shareState === 'idle' || typeof window === 'undefined') {
      return;
    }

    const timerId = window.setTimeout(() => {
      setShareState('idle');
    }, resetDelayMs);

    return () => {
      window.clearTimeout(timerId);
    };
  }, [resetDelayMs, shareState]);

  const copyShareLink = useCallback(async () => {
    setShareBusy(true);

    try {
      await copyToClipboard(buildShareUrl());
      setShareState('success');
    } catch {
      setShareState('error');
    } finally {
      setShareBusy(false);
    }
  }, [buildShareUrl]);

  return {
    copyShareLink,
    shareBusy,
    shareState,
  };
}
