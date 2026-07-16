import type { TFunction } from 'i18next';
import { WikiDocumentStatus, WikiDocumentVisibility } from './types/wiki';

export function toStatusText(t: TFunction, status: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return t('wiki.status.published');
    case WikiDocumentStatus.Archived:
      return t('wiki.status.archived');
    default:
      return t('wiki.status.draft');
  }
}

export function toVisibilityText(t: TFunction, visibility?: number): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return t('wiki.visibility.public');
    case WikiDocumentVisibility.Restricted:
      return t('wiki.visibility.restricted');
    default:
      return t('wiki.visibility.authenticated');
  }
}

export function toSourceText(t: TFunction, sourceType?: string | null): string {
  switch ((sourceType || '').trim().toLowerCase()) {
    case 'manual':
      return t('wiki.source.manual');
    case 'imported':
      return t('wiki.source.imported');
    case 'custom':
      return t('wiki.source.custom');
    case 'builtin':
      return t('wiki.source.builtin');
    case 'rollback':
      return t('wiki.source.rollback');
    default:
      return sourceType?.trim() || t('wiki.source.unknown');
  }
}
