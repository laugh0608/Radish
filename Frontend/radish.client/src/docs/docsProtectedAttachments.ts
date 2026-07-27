import { loadAttachmentAssetBlob } from '@radish/http';
import type { ProtectedMarkdownAttachmentOptions } from '@radish/ui/markdown-renderer';
import type { TFunction } from 'i18next';

export function createDocsProtectedAttachmentOptions(
  t: TFunction,
  scopeKey: string,
): ProtectedMarkdownAttachmentOptions {
  return {
    loadBlob: loadAttachmentAssetBlob,
    scopeKey,
    labels: {
      loading: t('wiki.attachments.loading'),
      loadFailed: t('wiki.attachments.loadFailed'),
      retry: t('wiki.attachments.retry'),
      download: t('wiki.attachments.download'),
      openImage: t('wiki.attachments.openImage'),
      lightboxClose: t('wiki.attachments.lightboxClose'),
      lightboxPrevious: t('wiki.attachments.lightboxPrevious'),
      lightboxNext: t('wiki.attachments.lightboxNext'),
    },
  };
}
