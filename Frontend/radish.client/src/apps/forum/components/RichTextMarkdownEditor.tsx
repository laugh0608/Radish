import { useEffect, useMemo, useRef, useState, type ReactNode, type ClipboardEvent } from 'react';
import { Icon } from '@radish/ui/icon';
import {
  buildAttachmentMarkdownUrl,
  sanitizeMarkdownLinkHref,
  type MarkdownDocumentUploadResult,
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
  placeholder?: string;
  minHeight?: number;
  disabled?: boolean;
  toolbarExtras?: ReactNode;
  className?: string;
  onImageUpload?: (file: File) => Promise<MarkdownImageUploadResult>;
  onDocumentUpload?: (file: File) => Promise<MarkdownDocumentUploadResult>;
}

export const RichTextMarkdownEditor = ({
  value,
  onChange,
  placeholder = '直接输入正文，底层会保存为 Markdown',
  minHeight = 320,
  disabled = false,
  toolbarExtras,
  className = '',
  onImageUpload,
  onDocumentUpload
}: RichTextMarkdownEditorProps) => {
  const editorRef = useRef<HTMLDivElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);
  const lastEmittedMarkdownRef = useRef(value);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const editorHtml = useMemo(() => markdownToRichHtml(value), [value]);

  useEffect(() => {
    if (!editorRef.current || value === lastEmittedMarkdownRef.current) {
      return;
    }

    editorRef.current.innerHTML = editorHtml;
  }, [editorHtml, value]);

  const syncMarkdownFromDom = () => {
    const html = editorRef.current?.innerHTML ?? '';
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

  const insertHtml = (html: string) => {
    if (disabled || uploading) {
      return;
    }

    focusEditor();
    document.execCommand('insertHTML', false, html);
    syncMarkdownFromDom();
  };

  const handlePaste = (event: ClipboardEvent<HTMLDivElement>) => {
    const clipboardItems = Array.from(event.clipboardData.items);
    const imageItem = clipboardItems.find((item) => item.type.startsWith('image/'));
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
    if (!onImageUpload) {
      insertHtml('<p><em>请在 Markdown 模式中插入图片</em></p>');
      return;
    }

    setUploading(true);
    setUploadError(null);
    try {
      const result = await onImageUpload(file);
      const markdownSrc = buildAttachmentMarkdownUrl(result.attachmentId, {
        displayVariant: result.displayVariant,
        scalePercent: result.scalePercent,
      });
      insertHtml(`<p>${buildRichImageHtml(markdownSrc, file.name)}</p>`);
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : '图片上传失败');
    } finally {
      setUploading(false);
    }
  };

  const handleDocumentSelection = async (file: File) => {
    if (!onDocumentUpload) {
      return;
    }

    setUploading(true);
    setUploadError(null);
    try {
      const result = await onDocumentUpload(file);
      const markdownHref = buildAttachmentMarkdownUrl(result.attachmentId);
      insertHtml(`<p>${buildRichLinkHtml(markdownHref, result.fileName || file.name)}</p>`);
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : '文档上传失败');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className={`${styles.container} ${className}`.trim()}>
      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'h2')} title="标题" disabled={disabled}>
            <Icon icon="mdi:format-header-2" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('bold')} title="粗体" disabled={disabled}>
            <Icon icon="mdi:format-bold" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('italic')} title="斜体" disabled={disabled}>
            <Icon icon="mdi:format-italic" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('strikeThrough')} title="删除线" disabled={disabled}>
            <Icon icon="mdi:format-strikethrough-variant" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'blockquote')} title="引用" disabled={disabled}>
            <Icon icon="mdi:format-quote-close" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<code>行内代码</code>')} title="行内代码" disabled={disabled}>
            <Icon icon="mdi:code-tags" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<pre><code>代码块</code></pre>')} title="代码块" disabled={disabled}>
            <Icon icon="mdi:code-braces-box" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertUnorderedList')} title="无序列表" disabled={disabled}>
            <Icon icon="mdi:format-list-bulleted" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertOrderedList')} title="有序列表" disabled={disabled}>
            <Icon icon="mdi:format-list-numbered" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => {
              const safeHref = sanitizeMarkdownLinkHref(window.prompt('输入链接地址'));
              if (safeHref) {
                runCommand('createLink', safeHref);
              }
            }}
            title="链接"
            disabled={disabled}
          >
            <Icon icon="mdi:link-variant" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => imageInputRef.current?.click()}
            title="图片"
            disabled={disabled || uploading}
          >
            <Icon icon="mdi:image-outline" size={18} />
          </button>
          {onDocumentUpload && (
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => documentInputRef.current?.click()}
              title="附件"
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:file-document-outline" size={18} />
            </button>
          )}
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<hr />')} title="分割线" disabled={disabled}>
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
          data-placeholder={placeholder}
          onInput={syncMarkdownFromDom}
          onPaste={handlePaste}
          dangerouslySetInnerHTML={{ __html: editorHtml }}
        />

        {uploading && (
          <div className={styles.overlay}>
            <Icon icon="mdi:loading" size={20} className={styles.spinIcon} />
            <span>上传中...</span>
          </div>
        )}

        {uploadError && (
          <div className={styles.error}>
            <span>{uploadError}</span>
            <button type="button" className={styles.errorDismiss} onClick={() => setUploadError(null)}>
              <Icon icon="mdi:close" size={14} />
            </button>
          </div>
        )}
      </div>

      <input
        ref={imageInputRef}
        type="file"
        accept="image/*"
        hidden
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
