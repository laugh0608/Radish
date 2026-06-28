import type { MouseEvent } from 'react';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound' | 'info';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  compact?: boolean;
  primaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
}

export function PublicStatusCard({
  tone,
  title,
  description,
  compact = false,
  primaryAction,
  secondaryAction
}: PublicStatusCardProps) {
  const actions: WebStateSlotAction[] = [];

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      onClick: primaryAction.href
        ? (event) => handlePublicForumLinkClick(event as MouseEvent<HTMLAnchorElement>, primaryAction.onClick)
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => handlePublicForumLinkClick(event as MouseEvent<HTMLAnchorElement>, secondaryAction.onClick)
        : () => secondaryAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      compact={compact}
      actions={actions}
    />
  );
}
