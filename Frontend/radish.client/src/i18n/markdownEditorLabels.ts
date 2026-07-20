import type { TFunction } from 'i18next';
import type { MarkdownEditorLabels } from '@radish/ui/markdown-editor';
import { resolveAttachmentUploadErrorMessage } from '@/attachments/attachmentPresentation';
import { getIntlLocale } from '@/locales/language';

export function createMarkdownEditorLabels(
  t: TFunction,
  language: string | null | undefined,
): MarkdownEditorLabels {
  const percentageFormatter = new Intl.NumberFormat(getIntlLocale(language), {
    style: 'percent',
    maximumFractionDigits: 0,
  });
  const imageUploadFailed = t('markdownEditor.upload.imageFailed');
  const documentUploadFailed = t('markdownEditor.upload.documentFailed');

  return {
    placeholder: t('markdownEditor.placeholder'),
    insert: {
      imageDescription: t('markdownEditor.insert.imageDescription'),
      boldText: t('markdownEditor.insert.boldText'),
      italicText: t('markdownEditor.insert.italicText'),
      strikethroughText: t('markdownEditor.insert.strikethroughText'),
      heading: t('markdownEditor.insert.heading'),
      quote: t('markdownEditor.insert.quote'),
      inlineCode: t('markdownEditor.insert.inlineCode'),
      codeBlock: t('markdownEditor.insert.codeBlock'),
      listItem: t('markdownEditor.insert.listItem'),
      linkText: t('markdownEditor.insert.linkText'),
    },
    toolbar: {
      heading: t('markdownEditor.toolbar.heading'),
      bold: t('markdownEditor.toolbar.bold'),
      italic: t('markdownEditor.toolbar.italic'),
      strikethrough: t('markdownEditor.toolbar.strikethrough'),
      quote: t('markdownEditor.toolbar.quote'),
      inlineCode: t('markdownEditor.toolbar.inlineCode'),
      codeBlock: t('markdownEditor.toolbar.codeBlock'),
      unorderedList: t('markdownEditor.toolbar.unorderedList'),
      orderedList: t('markdownEditor.toolbar.orderedList'),
      link: t('markdownEditor.toolbar.link'),
      image: t('markdownEditor.toolbar.image'),
      document: t('markdownEditor.toolbar.document'),
      horizontalRule: t('markdownEditor.toolbar.horizontalRule'),
      sticker: t('markdownEditor.toolbar.sticker'),
      edit: t('markdownEditor.toolbar.edit'),
      preview: t('markdownEditor.toolbar.preview'),
      split: t('markdownEditor.toolbar.split'),
    },
    upload: {
      formatUploading: (progress) => progress === null
        ? t('markdownEditor.upload.uploading')
        : t('markdownEditor.upload.uploadingProgress', {
            progress: percentageFormatter.format(progress / 100),
          }),
      formatError: (kind, error) => {
        const fallback = kind === 'image' ? imageUploadFailed : documentUploadFailed;
        return resolveAttachmentUploadErrorMessage(error, fallback);
      },
      imageFailed: imageUploadFailed,
      documentFailed: documentUploadFailed,
      dismissError: t('markdownEditor.upload.dismissError'),
    },
    previewEmpty: t('markdownEditor.previewEmpty'),
    stickerPicker: {
      searchPlaceholder: t('forum.reaction.searchPlaceholder'),
      clearSearch: t('forum.reaction.clearSearch'),
      reactionOnly: (name) => t('forum.reaction.reactionOnly', { name }),
      noEmoji: t('forum.reaction.noEmoji'),
      noSticker: t('forum.reaction.noSticker'),
    },
    userMention: {
      title: t('forum.mention.title'),
      loading: t('forum.mention.loading'),
      inputHint: t('forum.mention.inputHint'),
      empty: t('forum.mention.empty'),
      searchFailed: t('forum.mention.searchFailed'),
      selectHint: t('forum.mention.selectHint'),
    },
  };
}
