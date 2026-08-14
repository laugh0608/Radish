import { useEffect, type MouseEvent, type RefObject } from 'react';

export type PublicDocsDiagnosticCopyHandler = (
  stage: string,
  error: string | null | undefined
) => Promise<void>;

function getDocumentScrollElement(): HTMLElement | null {
  if (typeof document === 'undefined') {
    return null;
  }

  if (document.scrollingElement instanceof HTMLElement) {
    return document.scrollingElement;
  }

  return document.documentElement instanceof HTMLElement ? document.documentElement : null;
}

export function readPublicDocsScrollTop(container: HTMLDivElement | null): number {
  const containerTop = container?.scrollTop ?? 0;
  const windowTop = typeof window !== 'undefined' ? window.scrollY : 0;
  const documentTop = getDocumentScrollElement()?.scrollTop ?? 0;
  return Math.max(containerTop, windowTop, documentTop, 0);
}

function getPublicDocsMaxScrollTop(container: HTMLDivElement | null): number {
  const containerMax = container
    ? Math.max(container.scrollHeight - container.clientHeight, 0)
    : 0;
  const documentElement = getDocumentScrollElement();
  const documentMax = typeof window !== 'undefined' && documentElement
    ? Math.max(documentElement.scrollHeight - window.innerHeight, 0)
    : 0;
  return Math.max(containerMax, documentMax, 0);
}

export function writePublicDocsScrollTop(container: HTMLDivElement | null, top: number): void {
  const nextTop = Math.max(top, 0);

  container?.scrollTo({ top: nextTop, behavior: 'auto' });

  const documentElement = getDocumentScrollElement();
  if (documentElement) {
    documentElement.scrollTop = nextTop;
  }

  if (typeof window !== 'undefined') {
    window.scrollTo({ top: nextTop, behavior: 'auto' });
  }
}

export function shouldHandlePublicDocsLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

export function handlePublicDocsLinkClick(
  event: MouseEvent<HTMLAnchorElement>,
  action: () => void
) {
  if (!shouldHandlePublicDocsLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

export function usePublicDocsScrollRestore({
  isReady,
  restoreScrollTop,
  scrollContainerRef,
  onScrollRestored
}: {
  isReady: boolean;
  restoreScrollTop: number | null;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  onScrollRestored: () => void;
}) {
  useEffect(() => {
    if (restoreScrollTop == null || !isReady) {
      return;
    }

    let frameId = 0;
    let attempt = 0;
    const maxAttempts = 12;
    const targetScrollTop = restoreScrollTop;

    const restoreScroll = () => {
      const container = scrollContainerRef.current;
      if (!container) {
        onScrollRestored();
        return;
      }

      const maxScrollTop = getPublicDocsMaxScrollTop(container);
      const nextScrollTop = Math.min(targetScrollTop, maxScrollTop);
      writePublicDocsScrollTop(container, nextScrollTop);

      const restored = Math.abs(readPublicDocsScrollTop(container) - nextScrollTop) <= 2;
      const needsMoreLayout = maxScrollTop + 2 < targetScrollTop;

      if ((!needsMoreLayout && restored) || attempt >= maxAttempts) {
        onScrollRestored();
        return;
      }

      attempt += 1;
      frameId = window.requestAnimationFrame(restoreScroll);
    };

    frameId = window.requestAnimationFrame(restoreScroll);

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [isReady, onScrollRestored, restoreScrollTop, scrollContainerRef]);
}
