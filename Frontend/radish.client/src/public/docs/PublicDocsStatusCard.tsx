import type { MouseEvent } from 'react';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { handlePublicDocsLinkClick } from './publicDocsViewSupport';

interface PublicDocsStatusAction {
  label: string;
  href?: string;
  onClick: () => void;
}

interface PublicDocsStatusCardProps {
  tone: 'loading' | 'empty' | 'error' | 'notFound';
  title: string;
  description: string;
  compact?: boolean;
  primaryAction?: PublicDocsStatusAction;
  secondaryAction?: PublicDocsStatusAction;
  diagnosticAction?: PublicDocsStatusAction;
}

export function PublicDocsStatusCard({
  tone,
  title,
  description,
  compact = false,
  primaryAction,
  secondaryAction,
  diagnosticAction
}: PublicDocsStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:file-document-outline'
      : tone === 'notFound'
        ? 'mdi:file-search-outline'
        : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      kind: 'primary',
      onClick: primaryAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, primaryAction.onClick)
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, secondaryAction.onClick)
        : () => secondaryAction.onClick(),
    });
  }

  if (diagnosticAction) {
    actions.push({
      label: diagnosticAction.label,
      href: diagnosticAction.href,
      kind: 'secondary',
      onClick: diagnosticAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, diagnosticAction.onClick)
        : () => diagnosticAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      compact={compact}
      actions={actions}
    />
  );
}
