import type { TFunction } from 'i18next';
import { getIntlLocale } from '../../locales/language.ts';
import type { WikiDocumentDetailVo, WikiDocumentVo } from '../../api/wikiGovernanceApi.ts';

export const DOCUMENT_STATUS = {
  draft: 0,
  published: 1,
  archived: 2,
} as const;

export const DOCUMENT_VISIBILITY = {
  public: 1,
  authenticated: 2,
  restricted: 3,
} as const;

export function getDocumentStatusText(status: number, t: TFunction): string {
  if (status === DOCUMENT_STATUS.published) {
    return t('documents.status.published');
  }

  if (status === DOCUMENT_STATUS.archived) {
    return t('documents.status.archived');
  }

  return t('documents.status.draft');
}

export function getDocumentVisibilityText(visibility: number, t: TFunction): string {
  if (visibility === DOCUMENT_VISIBILITY.public) {
    return t('documents.visibility.public');
  }

  if (visibility === DOCUMENT_VISIBILITY.restricted) {
    return t('documents.visibility.restricted');
  }

  return t('documents.visibility.authenticated');
}

export function getDocumentSourceTypeText(sourceType: string, t: TFunction): string {
  const key = `documents.source.${sourceType.trim().toLowerCase()}`;
  const translated = t(key);
  return translated === key ? sourceType : translated;
}

export function formatDocumentDateTime(value: string | null | undefined, language?: string): string {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(parsed);
}

export function formatDocumentNumber(value: number, language?: string): string {
  return new Intl.NumberFormat(getIntlLocale(language)).format(value);
}

export function getDocumentSummary(record: WikiDocumentVo | WikiDocumentDetailVo, t: TFunction): string {
  const summary = record.voSummary?.trim();
  if (!summary) {
    return t('documents.summaryFallback');
  }

  return summary.length > 88 ? `${summary.slice(0, 88)}...` : summary;
}

export function getDocumentAccessSummary(
  record: WikiDocumentVo | WikiDocumentDetailVo,
  t: TFunction,
): string {
  if (record.voVisibility !== DOCUMENT_VISIBILITY.restricted) {
    return getDocumentVisibilityText(record.voVisibility, t);
  }

  return t('documents.access.restrictedSummary', {
    roles: t('documents.count.roles', { count: record.voAllowedRoles?.length ?? 0 }),
    permissions: t('documents.count.permissions', { count: record.voAllowedPermissions?.length ?? 0 }),
  });
}
