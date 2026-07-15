import type { TFunction } from 'i18next';
import {
  normalizeAccessList,
  normalizeOptionalNumber,
  type EditorDraft,
} from '../apps/wiki/wikiApp.helpers.ts';
import { WikiDocumentStatus, WikiDocumentVisibility } from '../apps/wiki/types/wiki.ts';

export function getDocsAuthorStatusText(status: number | undefined, t: TFunction): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return t('wiki.status.published');
    case WikiDocumentStatus.Archived:
      return t('wiki.status.archived');
    default:
      return t('wiki.status.draft');
  }
}

export function getDocsAuthorVisibilityText(visibility: number | undefined, t: TFunction): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return t('wiki.visibility.public');
    case WikiDocumentVisibility.Restricted:
      return t('wiki.visibility.restricted');
    default:
      return t('wiki.visibility.authenticated');
  }
}

export function getDocsAuthorSourceText(sourceType: string | null | undefined, t: TFunction): string {
  const normalizedSourceType = sourceType?.trim().toLowerCase();
  const sourceKey = normalizedSourceType ? `wiki.source.${normalizedSourceType}` : 'wiki.source.unknown';
  const translatedSource = t(sourceKey);
  return translatedSource === sourceKey ? sourceType?.trim() || t('wiki.source.unknown') : translatedSource;
}

export function getDocsAuthorSummaryPreview(summary: string | null | undefined, t: TFunction): string {
  const normalizedSummary = summary?.trim();
  if (!normalizedSummary) {
    return t('wiki.author.summaryFallback');
  }

  return normalizedSummary.length > 96 ? `${normalizedSummary.slice(0, 96)}...` : normalizedSummary;
}

export function validateDocsAuthorDraft(draft: EditorDraft, t: TFunction): string | null {
  if (!draft.title.trim() || !draft.markdownContent.trim()) {
    return t('wiki.author.validation.requiredFields');
  }

  const visibility = normalizeOptionalNumber(draft.visibility) ?? WikiDocumentVisibility.Authenticated;
  if (
    visibility === WikiDocumentVisibility.Restricted
    && normalizeAccessList(draft.allowedRoles).length === 0
    && normalizeAccessList(draft.allowedPermissions).length === 0
  ) {
    return t('wiki.author.validation.restrictedAccessRequired');
  }

  return null;
}

export function formatDocsAuthorNumber(value: number, language?: string): string {
  return new Intl.NumberFormat(language || 'zh-CN').format(value);
}
