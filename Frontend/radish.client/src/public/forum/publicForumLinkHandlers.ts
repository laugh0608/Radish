import type { MouseEvent } from 'react';

export function shouldHandlePublicForumLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

export function handlePublicForumLinkClick(event: MouseEvent<HTMLAnchorElement>, action?: () => void) {
  if (!action || !shouldHandlePublicForumLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}
