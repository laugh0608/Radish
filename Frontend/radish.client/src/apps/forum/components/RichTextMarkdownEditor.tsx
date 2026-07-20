import { useEffect, useMemo, useRef, useState, type ReactNode, type ClipboardEvent } from 'react';
import { Icon } from '@radish/ui/icon';
import {
  attachmentImageAccept,
  buildAttachmentMarkdownUrl,
  isSupportedAttachmentImageMimeType,
  sanitizeMarkdownLinkHref,
  type MarkdownDocumentUploadResult,
  type MarkdownEditorLabels,
  type MarkdownEditorUploadKind,
  type MarkdownEditorUploadProgressReporter,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import {
  buildRichImageHtml,
  buildRichLinkHtml,
  htmlToMarkdown,
  markdownToRichHtml,
} from './richTextMarkdown';
import styles from './RichTextMarkdownEditor.module.css';

interface RichTextMarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  labels: RichTextMarkdownEditorLabels;
  placeholder?: string;
  minHeight?: number;
  disabled?: boolean;
  toolbarExtras?: ReactNode;
  className?: string;
  onImageUpload?: (
    file: File,
    reportProgress: MarkdownEditorUploadProgressReporter,
  ) => Promise<MarkdownImageUploadResult>;
  onDocumentUpload?: (
    file: File,
    reportProgress: MarkdownEditorUploadProgressReporter,
  ) => Promise<MarkdownDocumentUploadResult>;
  onUploadError?: (kind: MarkdownEditorUploadKind, error: unknown) => void;
  onUploadingChange?: (uploading: boolean) => void;
}

interface RichTextMarkdownEditorLabels extends MarkdownEditorLabels {
  linkPrompt: string;
  imageUnavailable: string;
}

const normalizeUploadProgress = (progress: number): number | null => {
  if (!Number.isFinite(progress)) {
    return null;
  }

  return Math.min(Math.max(Math.round(progress), 0), 100);
};

export const RichTextMarkdownEditor = ({
  value,
  onChange,
  labels,
  placeholder,
  minHeight = 320,
  disabled = false,
  toolbarExtras,
  className = '',
  onImageUpload,
  onDocumentUpload,
  onUploadError,
  onUploadingChange,
}: RichTextMarkdownEditorProps) => {
  const editorRef = useRef<HTMLDivElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);
  const lastEmittedMarkdownRef = useRef(value);
  const uploadInFlightRef = useRef(false);
  const onUploadingChangeRef = useRef(onUploadingChange);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const editorHtml = useMemo(() => markdownToRichHtml(value), [value]);

  useEffect(() => {
    onUploadingChangeRef.current = onUploadingChange;
    onUploadingChange?.(uploading);
  }, [onUploadingChange, uploading]);

  useEffect(() => () => {
    onUploadingChangeRef.current?.(false);
  }, []);

  useEffect(() => {
    if (!editorRef.current || value === lastEmittedMarkdownRef.current) {
      return;
    }

    editorRef.current.innerHTML = editorHtml;
  }, [editorHtml, value]);

  const syncMarkdownFromDom = () => {
    const editor = editorRef.current;
    if (!editor) {
      return;
    }

    const html = editor.innerHTML;
    const markdown = htmlToMarkdown(html);
    lastEmittedMarkdownRef.current = markdown;
    onChange(markdown);
  };

  const focusEditor = () => {
    editorRef.current?.focus();
  };

  const runCommand = (command: string, commandValue?: string) => {
    if (disabled || uploading) {
      return;
    }

    focusEditor();
    document.execCommand(command, false, commandValue);
    syncMarkdownFromDom();
  };

  const insertHtml = (html: string, allowDuringUpload = false): boolean => {
    if (!editorRef.current || disabled || (uploadInFlightRef.current && !allowDuringUpload)) {
      return false;
    }

    focusEditor();
    document.execCommand('insertHTML', false, html);
    syncMarkdownFromDom();
    return true;
  };

  const handlePaste = (event: ClipboardEvent<HTMLDivElement>) => {
    const clipboardItems = Array.from(event.clipboardData.items);
    const imageItem = clipboardItems.find((item) => isSupportedAttachmentImageMimeType(item.type));
    const pastedImage = imageItem?.getAsFile();

    if (pastedImage) {
      event.preventDefault();
      void handleImageSelection(pastedImage);
      return;
    }

    event.preventDefault();
    const text = event.clipboardData.getData('text/plain');
    document.execCommand('insertText', false, text);
    syncMarkdownFromDom();
  };

  const handleImageSelection = async (file: File) => {
    if (disabled || uploading || uploadInFlightRef.current) {
      return;
    }

    if (!onImageUpload) {
      setUploadError(labels.imageUnavailable);
      return;
    }

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);
    try {
      const result = await onImageUpload(file, (progress) => {
        setUploadProgress(normalizeUploadProgress(progress));
      });
      const markdownSrc = buildAttachmentMarkdownUrl(result.attachmentId, {
        displayVariant: result.displayVariant,
        scalePercent: result.scalePercent,
      });
      insertHtml(`<p>${buildRichImageHtml(markdownSrc, file.name)}</p>`, true);
    } catch (error) {
      setUploadError(labels.upload.formatError('image', error));
      onUploadError?.('image', error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
    }
  };

  const handleDocumentSelection = async (file: File) => {
    if (disabled || uploading || uploadInFlightRef.current || !onDocumentUpload) {
      return;
    }

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);
    try {
      const result = await onDocumentUpload(file, (progress) => {
        setUploadProgress(normalizeUploadProgress(progress));
      });
      const markdownHref = buildAttachmentMarkdownUrl(result.attachmentId);
      insertHtml(`<p>${buildRichLinkHtml(markdownHref, result.fileName || file.name)}</p>`, true);
    } catch (error) {
      setUploadError(labels.upload.formatError('document', error));
      onUploadError?.('document', error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
    }
  };

  return (
    <div className={`${styles.container} ${className}`.trim()}>
      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'h2')} title={labels.toolbar.heading} disabled={disabled || uploading}>
            <Icon icon="mdi:format-header-2" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('bold')} title={labels.toolbar.bold} disabled={disabled || uploading}>
            <Icon icon="mdi:format-bold" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('italic')} title={labels.toolbar.italic} disabled={disabled || uploading}>
            <Icon icon="mdi:format-italic" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('strikeThrough')} title={labels.toolbar.strikethrough} disabled={disabled || uploading}>
            <Icon icon="mdi:format-strikethrough-variant" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'blockquote')} title={labels.toolbar.quote} disabled={disabled || uploading}>
            <Icon icon="mdi:format-quote-close" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml(`<code>${labels.insert.inlineCode}</code>`)} title={labels.toolbar.inlineCode} disabled={disabled || uploading}>
            <Icon icon="mdi:code-tags" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml(`<pre><code>${labels.insert.codeBlock}</code></pre>`)} title={labels.toolbar.codeBlock} disabled={disabled || uploading}>
            <Icon icon="mdi:code-braces-box" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertUnorderedList')} title={labels.toolbar.unorderedList} disabled={disabled || uploading}>
            <Icon icon="mdi:format-list-bulleted" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertOrderedList')} title={labels.toolbar.orderedList} disabled={disabled || uploading}>
            <Icon icon="mdi:format-list-numbered" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => {
              const safeHref = sanitizeMarkdownLinkHref(window.prompt(labels.linkPrompt));
              if (safeHref) {
                runCommand('createLink', safeHref);
              }
            }}
            title={labels.toolbar.link}
            disabled={disabled || uploading}
          >
            <Icon icon="mdi:link-variant" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => imageInputRef.current?.click()}
            title={labels.toolbar.image}
            disabled={disabled || uploading}
          >
            <Icon icon="mdi:image-outline" size={18} />
          </button>
          {onDocumentUpload && (
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => documentInputRef.current?.click()}
              title={labels.toolbar.document}
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:file-document-outline" size={18} />
            </button>
          )}
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<hr />')} title={labels.toolbar.horizontalRule} disabled={disabled || uploading}>
            <Icon icon="mdi:minus" size={18} />
          </button>
        </div>

        <div className={styles.toolbarSpacer} />

        {toolbarExtras && <div className={styles.toolbarExtras}>{toolbarExtras}</div>}
      </div>

      <div className={styles.editorShell} style={{ minHeight }}>
        <div
          ref={editorRef}
          className={styles.editor}
          contentEditable={!disabled && !uploading}
          suppressContentEditableWarning={true}
          data-placeholder={placeholder ?? labels.placeholder}
          onInput={syncMarkdownFromDom}
          onPaste={handlePaste}
          dangerouslySetInnerHTML={{ __html: editorHtml }}
        />

        {uploading && (
          <div className={styles.overlay} role="status" aria-live="polite">
            <Icon icon="mdi:loading" size={20} className={styles.spinIcon} />
            <span>{labels.upload.formatUploading(uploadProgress)}</span>
          </div>
        )}

        {uploadError && (
          <div className={styles.error} role="alert">
            <span>{uploadError}</span>
            <button
              type="button"
              className={styles.errorDismiss}
              onClick={() => setUploadError(null)}
              title={labels.upload.dismissError}
              aria-label={labels.upload.dismissError}
            >
              <Icon icon="mdi:close" size={14} />
            </button>
          </div>
        )}
      </div>

      <input
        ref={imageInputRef}
        type="file"
        accept={attachmentImageAccept}
        hidden
        disabled={disabled || uploading}
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void handleImageSelection(file);
          }
          event.currentTarget.value = '';
        }}
      />
      <input
        ref={documentInputRef}
        type="file"
        hidden
        disabled={disabled || uploading}
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void handleDocumentSelection(file);
          }
          event.currentTarget.value = '';
        }}
      />
    </div>
  );
};
