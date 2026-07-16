import type { TFunction } from 'i18next';
import { getIntlLocale } from '../locales/language.ts';

const fileSizeUnits = ['B', 'KB', 'MB', 'GB', 'TB'] as const;

const businessTypeKeys: Readonly<Record<string, string>> = {
  general: 'profile.attachments.business.general',
  post: 'profile.attachments.business.post',
  comment: 'profile.attachments.business.comment',
  avatar: 'profile.attachments.business.avatar',
  document: 'profile.attachments.business.document',
};

const uploadErrorCodes = new Set([
  'Attachment.FileEmpty',
  'Attachment.ImageTypeUnsupported',
  'Attachment.DocumentTypeUnsupported',
  'Attachment.UploadForbidden',
  'Attachment.RateLimited',
  'Attachment.FileTooLarge',
  'Attachment.UnsupportedMediaType',
  'Attachment.ContentMismatch',
  'Attachment.StorageFailed',
  'Attachment.ProcessingFailed',
]);

const uploadErrorMessageKeys = new Set([
  'error.attachment.file_empty',
  'error.attachment.image_type_unsupported',
  'error.attachment.document_type_unsupported',
  'error.attachment.upload_forbidden',
  'error.attachment.rate_limited',
  'error.attachment.file_too_large',
  'error.attachment.unsupported_media_type',
  'error.attachment.content_mismatch',
  'error.attachment.storage_failed',
  'error.attachment.processing_failed',
]);

export function resolveAttachmentUploadErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null) {
    return fallback;
  }

  const structuredError = error as { code?: unknown; messageKey?: unknown; message?: unknown };
  const hasKnownCode = typeof structuredError.code === 'string' && uploadErrorCodes.has(structuredError.code);
  const hasKnownMessageKey = typeof structuredError.messageKey === 'string'
    && uploadErrorMessageKeys.has(structuredError.messageKey);
  const message = typeof structuredError.message === 'string' ? structuredError.message.trim() : '';

  return (hasKnownCode || hasKnownMessageKey) && message ? message : fallback;
}

export function formatAttachmentFileSize(
  bytes: number,
  language: string | null | undefined,
): string {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return '0 B';
  }

  const unitIndex = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    fileSizeUnits.length - 1,
  );
  const value = bytes / (1024 ** unitIndex);
  const formattedValue = new Intl.NumberFormat(getIntlLocale(language), {
    minimumFractionDigits: 0,
    maximumFractionDigits: unitIndex === 0 ? 0 : 2,
  }).format(value);

  return `${formattedValue} ${fileSizeUnits[unitIndex]}`;
}

export function resolveAttachmentBusinessType(
  value: string | null | undefined,
  t: TFunction,
): string {
  const normalized = value?.trim();
  if (!normalized) {
    return t('profile.attachments.business.general');
  }

  const key = businessTypeKeys[normalized.toLowerCase()];
  return key ? t(key) : normalized;
}
