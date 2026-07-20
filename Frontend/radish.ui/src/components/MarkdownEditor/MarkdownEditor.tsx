import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import type { KeyboardEvent, ChangeEvent, ClipboardEvent, DragEvent } from 'react';
import { Icon } from '../Icon/Icon';
import { StickerPicker } from '../StickerPicker/StickerPicker';
import type {
  StickerPickerGroup,
  StickerPickerLabels,
  StickerPickerSelection,
} from '../StickerPicker/StickerPicker';
import { MarkdownRenderer, type MarkdownStickerMap } from '../MarkdownRenderer/MarkdownRenderer';
import { UserMention } from '../UserMention/UserMention';
import type { UserMentionLabels, UserMentionOption } from '../UserMention/UserMention';
import {
  buildAttachmentMarkdownUrl,
  attachmentImageAccept,
  escapeMarkdownLabel,
  isSupportedAttachmentImageFile,
  isSupportedAttachmentImageMimeType,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '../../utils';
import styles from './MarkdownEditor.module.css';

const MENTION_PANEL_WIDTH = 320;
const MENTION_PANEL_HEIGHT = 220;
const MENTION_PANEL_GAP = 10;
const MENTION_PANEL_PADDING = 12;

interface MentionContext {
  keyword: string;
  start: number;
}

export type MarkdownEditorUploadKind = 'image' | 'document';

export type MarkdownEditorUploadProgressReporter = (progress: number) => void;

export interface MarkdownEditorLabels {
  placeholder: string;
  insert: {
    imageDescription: string;
    boldText: string;
    italicText: string;
    strikethroughText: string;
    heading: string;
    quote: string;
    inlineCode: string;
    codeBlock: string;
    listItem: string;
    linkText: string;
  };
  toolbar: {
    heading: string;
    bold: string;
    italic: string;
    strikethrough: string;
    quote: string;
    inlineCode: string;
    codeBlock: string;
    unorderedList: string;
    orderedList: string;
    link: string;
    image: string;
    document: string;
    horizontalRule: string;
    sticker: string;
    edit: string;
    preview: string;
    split: string;
  };
  upload: {
    formatUploading: (progress: number | null) => string;
    formatError: (kind: MarkdownEditorUploadKind, error: unknown) => string;
    imageFailed: string;
    documentFailed: string;
    dismissError: string;
  };
  previewEmpty: string;
  stickerPicker: StickerPickerLabels;
  userMention: UserMentionLabels;
}

const getTextareaCaretRelativePosition = (
  textarea: HTMLTextAreaElement,
  cursorPos: number
): { top: number; left: number; lineHeight: number } | null => {
  if (typeof document === 'undefined') {
    return null;
  }

  const computed = window.getComputedStyle(textarea);
  const mirror = document.createElement('div');
  const span = document.createElement('span');
  const textBeforeCaret = textarea.value.slice(0, cursorPos);

  const mirroredProperties = [
    'boxSizing',
    'width',
    'height',
    'paddingTop',
    'paddingRight',
    'paddingBottom',
    'paddingLeft',
    'borderTopWidth',
    'borderRightWidth',
    'borderBottomWidth',
    'borderLeftWidth',
    'borderTopStyle',
    'borderRightStyle',
    'borderBottomStyle',
    'borderLeftStyle',
    'fontFamily',
    'fontSize',
    'fontStyle',
    'fontVariant',
    'fontWeight',
    'fontStretch',
    'fontKerning',
    'fontFeatureSettings',
    'fontVariationSettings',
    'lineHeight',
    'letterSpacing',
    'textTransform',
    'textIndent',
    'textAlign',
    'whiteSpace',
    'wordBreak',
    'overflowWrap',
    'tabSize'
  ] as const;

  for (const property of mirroredProperties) {
    mirror.style[property] = computed[property];
  }

  mirror.style.position = 'absolute';
  mirror.style.top = '0';
  mirror.style.left = '0';
  mirror.style.visibility = 'hidden';
  mirror.style.pointerEvents = 'none';
  mirror.style.overflow = 'hidden';
  mirror.style.whiteSpace = 'pre-wrap';
  mirror.style.wordBreak = 'break-word';
  mirror.style.overflowWrap = 'break-word';

  mirror.textContent = textBeforeCaret;
  span.textContent = textarea.value.slice(cursorPos) || '.';
  mirror.appendChild(span);
  document.body.appendChild(mirror);

  const lineHeight = Number.parseFloat(computed.lineHeight) || 24;
  const caretTop = span.offsetTop - textarea.scrollTop;
  const caretLeft = span.offsetLeft - textarea.scrollLeft;

  document.body.removeChild(mirror);

  return {
    top: caretTop,
    left: caretLeft,
    lineHeight
  };
};

function findMentionContext(text: string, cursor: number): MentionContext | null {
  if (cursor < 0) {
    return null;
  }

  const textBeforeCursor = text.slice(0, cursor);
  const lastAtIndex = textBeforeCursor.lastIndexOf('@');
  if (lastAtIndex < 0) {
    return null;
  }

  const textAfterAt = textBeforeCursor.slice(lastAtIndex + 1);
  if (/[\s()[\]{}]/.test(textAfterAt)) {
    return null;
  }

  return {
    keyword: textAfterAt,
    start: lastAtIndex
  };
}

export interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  labels: MarkdownEditorLabels;
  placeholder?: string;
  minHeight?: number;
  maxHeight?: number;
  defaultMode?: 'edit' | 'preview' | 'split';
  disabled?: boolean;
  showToolbar?: boolean;
  theme?: 'dark' | 'light';
  toolbarExtras?: ReactNode;
  className?: string;
  /**
   * 图片上传处理函数
   * 如果不提供，图片按钮将插入默认的 Markdown 语法
   */
  onImageUpload?: (
    file: File,
    reportProgress: MarkdownEditorUploadProgressReporter
  ) => Promise<MarkdownImageUploadResult>;
  /**
   * 文档上传处理函数
   * 如果不提供，文档按钮将不显示
   */
  onDocumentUpload?: (
    file: File,
    reportProgress: MarkdownEditorUploadProgressReporter
  ) => Promise<MarkdownDocumentUploadResult>;
  /** 上传失败时交由宿主记录或补充反馈。 */
  onUploadError?: (kind: MarkdownEditorUploadKind, error: unknown) => void;
  /** 上传忙碌态交由宿主锁定会卸载编辑器的入口。 */
  onUploadingChange?: (uploading: boolean) => void;
  /** 贴图分组（可选，传入后显示 StickerPicker） */
  stickerGroups?: StickerPickerGroup[];
  /** 贴图渲染映射（可选） */
  stickerMap?: MarkdownStickerMap;
  /** 选择贴图/emoji 后回调（可用于埋点或记录使用） */
  onStickerSelect?: (selection: StickerPickerSelection) => void;
  /** 搜索 @ 提及用户（传入后启用 @ 提及能力） */
  onUserMentionSearch?: (keyword: string) => Promise<UserMentionOption[]>;
}

type ToolbarAction = 'bold' | 'italic' | 'strikethrough' | 'heading' | 'quote' | 'code' | 'codeblock' | 'ul' | 'ol' | 'link' | 'image' | 'document' | 'hr';

const normalizeUploadProgress = (progress: number): number | null => {
  if (!Number.isFinite(progress)) {
    return null;
  }

  return Math.min(Math.max(Math.round(progress), 0), 100);
};

export const MarkdownEditor = ({
  value,
  onChange,
  labels,
  placeholder,
  minHeight = 150,
  maxHeight,
  defaultMode,
  disabled = false,
  showToolbar = true,
  theme = 'dark',
  toolbarExtras,
  className = '',
  onImageUpload,
  onDocumentUpload,
  onUploadError,
  onUploadingChange,
  stickerGroups,
  stickerMap,
  onStickerSelect,
  onUserMentionSearch,
}: MarkdownEditorProps) => {
  const [mode, setMode] = useState<'edit' | 'preview' | 'split'>(
    defaultMode ?? (typeof window !== 'undefined' && window.innerWidth > 768 ? 'split' : 'edit')
  );
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [mentionContext, setMentionContext] = useState<MentionContext | null>(null);
  const [mentionPosition, setMentionPosition] = useState({ top: 0, left: 0 });
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const editPaneRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);
  const uploadInFlightRef = useRef(false);
  const onUploadingChangeRef = useRef(onUploadingChange);
  const editorPlaceholder = placeholder ?? labels.placeholder;

  useEffect(() => {
    onUploadingChangeRef.current = onUploadingChange;
    onUploadingChange?.(uploading);
  }, [onUploadingChange, uploading]);

  useEffect(() => () => {
    onUploadingChangeRef.current?.(false);
  }, []);

  useEffect(() => {
    if (disabled || uploading) {
      setMentionContext(null);
    }
  }, [disabled, uploading]);

  // 常用 Emoji
  const emojis = [
    '😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂',
    '🙂', '🙃', '😉', '😊', '😇', '🥰', '😍', '🤩',
    '😘', '😗', '😚', '😙', '😋', '😛', '😜', '🤪',
    '😝', '🤑', '🤗', '🤭', '🤫', '🤔', '🤐', '🤨',
    '😐', '😑', '😶', '😏', '😒', '🙄', '😬', '🤥',
    '😌', '😔', '😪', '🤤', '😴', '😷', '🤒', '🤕',
    '🤢', '🤮', '🤧', '🥵', '🥶', '🥴', '😵', '🤯',
    '🤠', '🥳', '😎', '🤓', '🧐', '😕', '😟', '🙁',
    '☹️', '😮', '😯', '😲', '😳', '🥺', '😦', '😧',
    '😨', '😰', '😥', '😢', '😭', '😱', '😖', '😣',
    '😞', '😓', '😩', '😫', '🥱', '😤', '😡', '😠',
    '🤬', '😈', '👿', '💀', '☠️', '💩', '🤡', '👹',
    '👺', '👻', '👽', '👾', '🤖', '😺', '😸', '😹',
    '👍', '👎', '👌', '✌️', '🤞', '🤟', '🤘', '🤙',
    '👈', '👉', '👆', '👇', '☝️', '✋', '🤚', '🖐',
    '🖖', '👋', '🤛', '🤜', '✊', '👊', '🤝', '🙏',
    '❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍',
    '💔', '❣️', '💕', '💞', '💓', '💗', '💖', '💘',
    '💝', '💟', '☮️', '✝️', '☪️', '🕉', '☸️', '✡️',
    '🔯', '🕎', '☯️', '☦️', '🛐', '⛎', '♈', '♉'
  ];

  const ensureStickerCode = (value: string): string => value.trim().toLowerCase();

  const escapeMarkdownAlt = (value: string): string => value.replace(/[[\]]/g, '').trim();

  const buildStickerMarkdownUrl = (
    groupCode: string,
    stickerCode: string
  ): string => {
    const normalizedGroupCode = ensureStickerCode(groupCode);
    const normalizedStickerCode = ensureStickerCode(stickerCode);
    return `sticker://${normalizedGroupCode}/${normalizedStickerCode}`;
  };

  const mergedStickerMap = useMemo<MarkdownStickerMap | undefined>(() => {
    if (stickerMap) {
      return stickerMap;
    }

    if (!stickerGroups || stickerGroups.length === 0) {
      return undefined;
    }

    const map: MarkdownStickerMap = {};
    for (const group of stickerGroups) {
      const groupCode = ensureStickerCode(group.code);
      if (!groupCode) {
        continue;
      }

      for (const sticker of group.stickers || []) {
        const stickerCode = ensureStickerCode(sticker.code);
        if (!stickerCode || !sticker.imageUrl) {
          continue;
        }

        map[`${groupCode}/${stickerCode}`] = {
          imageUrl: sticker.imageUrl,
          thumbnailUrl: sticker.thumbnailUrl || undefined,
          name: sticker.name,
        };
      }
    }

    return map;
  }, [stickerGroups, stickerMap]);

  const updateMentionPosition = useCallback((textarea: HTMLTextAreaElement, cursorPos?: number) => {
    const container = editPaneRef.current;
    if (!container) {
      return;
    }

    const selectionStart = cursorPos ?? textarea.selectionStart ?? 0;
    const caretPosition = getTextareaCaretRelativePosition(textarea, selectionStart);
    const baseTop = caretPosition?.top ?? 18;
    const baseLeft = caretPosition?.left ?? 18;
    const lineHeight = caretPosition?.lineHeight ?? 24;
    const containerWidth = container.clientWidth;
    const containerHeight = container.clientHeight;
    const left = Math.max(
      MENTION_PANEL_PADDING,
      Math.min(baseLeft - 14, containerWidth - MENTION_PANEL_WIDTH - MENTION_PANEL_PADDING)
    );
    let top = baseTop + lineHeight + MENTION_PANEL_GAP;

    if (top + MENTION_PANEL_HEIGHT > containerHeight - MENTION_PANEL_PADDING) {
      top = baseTop - MENTION_PANEL_HEIGHT - MENTION_PANEL_GAP;
    }

    top = Math.max(
      MENTION_PANEL_PADDING,
      Math.min(top, containerHeight - MENTION_PANEL_HEIGHT - MENTION_PANEL_PADDING)
    );

    setMentionPosition({ top, left });
  }, []);

  useEffect(() => {
    if (!mentionContext || !textareaRef.current) {
      return;
    }

    const syncPosition = () => {
      if (textareaRef.current) {
        updateMentionPosition(textareaRef.current, textareaRef.current.selectionStart);
      }
    };

    syncPosition();
    window.addEventListener('resize', syncPosition);

    return () => {
      window.removeEventListener('resize', syncPosition);
    };
  }, [mentionContext, updateMentionPosition]);

  // 插入文本
  const insertText = (before: string, after: string = '', placeholder: string = '') => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const currentValue = textarea.value;
    const selectedText = currentValue.substring(start, end);
    const textToInsert = selectedText || placeholder;

    const newValue = currentValue.substring(0, start) + before + textToInsert + after + currentValue.substring(end);
    onChange(newValue);

    // 设置新的光标位置
    setTimeout(() => {
      const newCursorPos = start + before.length + textToInsert.length;
      textarea.focus();
      textarea.setSelectionRange(newCursorPos, newCursorPos);
    }, 0);
  };

  const reportUploadProgress: MarkdownEditorUploadProgressReporter = (progress) => {
    const normalizedProgress = normalizeUploadProgress(progress);
    if (normalizedProgress !== null) {
      setUploadProgress(normalizedProgress);
    }
  };

  // 处理图片上传
  const handleImageUpload = async (file: File) => {
    if (disabled || uploading || uploadInFlightRef.current || !textareaRef.current) {
      return;
    }

    if (!onImageUpload) {
      // 如果没有提供上传函数，使用默认行为
      insertText('![', '](url)', labels.insert.imageDescription);
      return;
    }

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);

    try {
      const result = await onImageUpload(file, reportUploadProgress);
      const imageUrl = buildAttachmentMarkdownUrl(result.attachmentId, {
        displayVariant: result.displayVariant,
        scalePercent: result.scalePercent,
      });

      // 插入图片 Markdown 语法
      const imageLabel = escapeMarkdownLabel(file.name) || labels.insert.imageDescription;
      insertText(`![${imageLabel}](${imageUrl})`, '', '');
    } catch (error) {
      setUploadError(labels.upload.formatError('image', error));
      onUploadError?.('image', error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
    }
  };

  // 处理文档上传
  const handleDocumentUpload = async (file: File) => {
    if (disabled || uploading || uploadInFlightRef.current || !textareaRef.current || !onDocumentUpload) {
      return;
    }

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);

    try {
      const result = await onDocumentUpload(file, reportUploadProgress);
      const documentUrl = buildAttachmentMarkdownUrl(result.attachmentId);

      // 插入文档链接 Markdown 语法
      const documentLabel = escapeMarkdownLabel(result.fileName || file.name) || labels.toolbar.document;
      insertText(`[${documentLabel}](${documentUrl})`, '', '');
    } catch (error) {
      setUploadError(labels.upload.formatError('document', error));
      onUploadError?.('document', error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
    }
  };

  // 处理文件选择
  const handleFileInputChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && isSupportedAttachmentImageFile(file)) {
      await handleImageUpload(file);
    }
    // 清空文件输入，允许重复选择同一个文件
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // 处理文档文件选择
  const handleDocumentInputChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      await handleDocumentUpload(file);
    }
    // 清空文件输入，允许重复选择同一个文件
    if (documentInputRef.current) {
      documentInputRef.current.value = '';
    }
  };

  // 处理粘贴事件
  const handlePaste = async (e: ClipboardEvent<HTMLTextAreaElement>) => {
    if (!onImageUpload) return;

    const items = e.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (isSupportedAttachmentImageMimeType(item.type)) {
        e.preventDefault();
        const file = item.getAsFile();
        if (file && isSupportedAttachmentImageFile(file)) {
          await handleImageUpload(file);
        }
        break;
      }
    }
  };

  // 处理拖拽上传
  const handleDrop = async (e: DragEvent<HTMLTextAreaElement>) => {
    if (!onImageUpload) return;

    e.preventDefault();
    e.stopPropagation();

    const files = e.dataTransfer?.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    if (isSupportedAttachmentImageFile(file)) {
      await handleImageUpload(file);
    }
  };

  const handleDragOver = (e: DragEvent<HTMLTextAreaElement>) => {
    if (!onImageUpload) return;
    e.preventDefault();
    e.stopPropagation();
  };

  // 工具栏操作
  const handleToolbarAction = (action: ToolbarAction) => {
    if (disabled || uploading || !textareaRef.current) {
      return;
    }

    switch (action) {
      case 'bold':
        insertText('**', '**', labels.insert.boldText);
        break;
      case 'italic':
        insertText('*', '*', labels.insert.italicText);
        break;
      case 'strikethrough':
        insertText('~~', '~~', labels.insert.strikethroughText);
        break;
      case 'heading':
        insertText('## ', '', labels.insert.heading);
        break;
      case 'quote':
        insertText('> ', '', labels.insert.quote);
        break;
      case 'code':
        insertText('`', '`', labels.insert.inlineCode);
        break;
      case 'codeblock':
        insertText('```\n', '\n```', labels.insert.codeBlock);
        break;
      case 'ul':
        insertText('- ', '', labels.insert.listItem);
        break;
      case 'ol':
        insertText('1. ', '', labels.insert.listItem);
        break;
      case 'link':
        insertText('[', '](url)', labels.insert.linkText);
        break;
      case 'image':
        if (onImageUpload) {
          // 如果提供了上传函数，触发文件选择
          fileInputRef.current?.click();
        } else {
          // 否则插入默认模板
          insertText('![', '](url)', labels.insert.imageDescription);
        }
        break;
      case 'document':
        if (onDocumentUpload) {
          // 触发文档文件选择
          documentInputRef.current?.click();
        }
        break;
      case 'hr':
        insertText('\n---\n', '', '');
        break;
    }
  };

  // 快捷键处理
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.ctrlKey || e.metaKey) {
      switch (e.key) {
        case 'b':
          e.preventDefault();
          handleToolbarAction('bold');
          break;
        case 'i':
          e.preventDefault();
          handleToolbarAction('italic');
          break;
        case 'k':
          e.preventDefault();
          handleToolbarAction('link');
          break;
      }
    }
  };

  // Emoji 插入
  const insertEmoji = (emoji: string) => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const newValue = value.substring(0, start) + emoji + value.substring(start);
    onChange(newValue);

    setTimeout(() => {
      textarea.focus();
      const newPos = start + emoji.length;
      textarea.setSelectionRange(newPos, newPos);
    }, 0);

    onStickerSelect?.({ type: 'unicode', emoji });
  };

  const handleStickerPickerSelect = (selection: StickerPickerSelection) => {
    if (selection.type === 'unicode') {
      if (selection.emoji) {
        insertEmoji(selection.emoji);
      }
      return;
    }

    const groupCode = selection.groupCode?.trim();
    const stickerCode = selection.stickerCode?.trim();
    if (!groupCode || !stickerCode) {
      return;
    }

    const altText = escapeMarkdownAlt(selection.stickerName || stickerCode) || stickerCode;
    const stickerUrl = buildStickerMarkdownUrl(groupCode, stickerCode);
    insertText(`![${altText}](${stickerUrl})`, '', '');
    onStickerSelect?.(selection);
  };

  const replaceSelection = useCallback((text: string, start: number, end: number) => {
    const textarea = textareaRef.current;
    if (!textarea) {
      return;
    }

    const currentValue = textarea.value;
    const nextValue = currentValue.substring(0, start) + text + currentValue.substring(end);
    onChange(nextValue);

    setTimeout(() => {
      textarea.focus();
      const nextCursor = start + text.length;
      textarea.setSelectionRange(nextCursor, nextCursor);
    }, 0);
  }, [onChange]);

  const handleMentionSelect = useCallback((user: UserMentionOption) => {
    const textarea = textareaRef.current;
    if (disabled || uploading || !textarea || !mentionContext) {
      return;
    }

    const mentionText = `@${user.userName} `;
    replaceSelection(mentionText, mentionContext.start, textarea.selectionStart);
    setMentionContext(null);
  }, [disabled, mentionContext, replaceSelection, uploading]);

  const handleChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    const nextValue = e.target.value;
    onChange(nextValue);

    if (!onUserMentionSearch) {
      return;
    }

    const nextMentionContext = findMentionContext(nextValue, e.target.selectionStart);
    setMentionContext(nextMentionContext);

    if (nextMentionContext) {
      updateMentionPosition(e.target, e.target.selectionStart);
    }
  };

  const rootStyle: React.CSSProperties = {
    minHeight: `${minHeight}px`,
    ...(maxHeight ? { maxHeight: `${maxHeight}px` } : {})
  };

  const themeClassName = theme === 'light' ? styles.themeLight : '';
  const editingDisabled = disabled || uploading || mode === 'preview';

  return (
    <div className={`${styles.container} ${themeClassName} ${className}`} style={rootStyle}>
      {/* 隐藏的文件输入 */}
      <input
        ref={fileInputRef}
        type="file"
        accept={attachmentImageAccept}
        style={{ display: 'none' }}
        disabled={editingDisabled}
        onChange={handleFileInputChange}
      />
      <input
        ref={documentInputRef}
        type="file"
        accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip,.rar"
        style={{ display: 'none' }}
        disabled={editingDisabled}
        onChange={handleDocumentInputChange}
      />

      {/* 工具栏 */}
      {showToolbar && (
        <div className={styles.toolbar}>
          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('heading')}
              title={labels.toolbar.heading}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-header-pound" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('bold')}
              title={labels.toolbar.bold}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-bold" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('italic')}
              title={labels.toolbar.italic}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-italic" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('strikethrough')}
              title={labels.toolbar.strikethrough}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-strikethrough" size={18} />
            </button>
          </div>

          <div className={styles.toolbarDivider} />

          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('quote')}
              title={labels.toolbar.quote}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-quote-close" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('code')}
              title={labels.toolbar.inlineCode}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:code-tags" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('codeblock')}
              title={labels.toolbar.codeBlock}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:code-braces" size={18} />
            </button>
          </div>

          <div className={styles.toolbarDivider} />

          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('ul')}
              title={labels.toolbar.unorderedList}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-list-bulleted" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('ol')}
              title={labels.toolbar.orderedList}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:format-list-numbered" size={18} />
            </button>
          </div>

          <div className={styles.toolbarDivider} />

          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('link')}
              title={labels.toolbar.link}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:link" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('image')}
              title={labels.toolbar.image}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:image" size={18} />
            </button>
            {onDocumentUpload && (
              <button
                type="button"
                className={styles.toolbarButton}
                onClick={() => handleToolbarAction('document')}
                title={labels.toolbar.document}
                disabled={editingDisabled}
              >
                <Icon icon="mdi:file-document-outline" size={18} />
              </button>
            )}
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('hr')}
              title={labels.toolbar.horizontalRule}
              disabled={editingDisabled}
            >
              <Icon icon="mdi:minus" size={18} />
            </button>
          </div>

          <div className={styles.toolbarDivider} />

          <div className={styles.toolbarGroup}>
            <StickerPicker
              groups={stickerGroups || []}
              mode="insert"
              theme={theme}
              disabled={editingDisabled}
              onSelect={handleStickerPickerSelect}
              triggerTitle={labels.toolbar.sticker}
              emojis={emojis}
              labels={labels.stickerPicker}
            />
          </div>

          <div className={styles.toolbarSpacer} />

          {toolbarExtras && (
            <div className={styles.toolbarExtras}>{toolbarExtras}</div>
          )}

          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={`${styles.toolbarButton} ${mode === 'edit' ? styles.active : ''}`}
              onClick={() => setMode('edit')}
              title={labels.toolbar.edit}
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:pencil" size={18} />
            </button>
            <button
              type="button"
              className={`${styles.toolbarButton} ${mode === 'preview' ? styles.active : ''}`}
              onClick={() => setMode('preview')}
              title={labels.toolbar.preview}
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:eye" size={18} />
            </button>
            <button
              type="button"
              className={`${styles.toolbarButton} ${mode === 'split' ? styles.active : ''}`}
              onClick={() => setMode('split')}
              title={labels.toolbar.split}
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:format-columns" size={18} />
            </button>
          </div>
        </div>
      )}

      {/* 编辑/预览区域 */}
      <div className={`${styles.content} ${mode === 'split' ? styles.contentSplit : ''}`}>
        {(mode === 'edit' || mode === 'split') && (
          <div
            ref={editPaneRef}
            className={`${styles.editPane} ${mode === 'split' ? styles.paneSplit : ''}`}
          >
            <textarea
              ref={textareaRef}
              className={styles.textarea}
              value={value}
              onChange={handleChange}
              onKeyDown={handleKeyDown}
              onPaste={handlePaste}
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onClick={(event) => {
                if (mentionContext) {
                  updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                }
              }}
              onKeyUp={(event) => {
                if (mentionContext) {
                  updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                }
              }}
              onScroll={(event) => {
                if (mentionContext) {
                  updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                }
              }}
              placeholder={editorPlaceholder}
              disabled={disabled || uploading}
            />
            {!disabled && !uploading && mentionContext && onUserMentionSearch && (
              <UserMention
                keyword={mentionContext.keyword}
                onSearch={onUserMentionSearch}
                onSelect={handleMentionSelect}
                onClose={() => setMentionContext(null)}
                position={mentionPosition}
                positionMode="absolute"
                labels={labels.userMention}
              />
            )}
            {uploading && (
              <div className={styles.uploadingOverlay} role="status" aria-live="polite">
                <Icon icon="mdi:loading" size={24} className={styles.spinIcon} />
                <span>{labels.upload.formatUploading(uploadProgress)}</span>
              </div>
            )}
            {uploadError && (
              <div className={styles.uploadError} role="alert">
                <Icon icon="mdi:alert-circle" size={16} />
                <span>{uploadError}</span>
                <button
                  type="button"
                  className={styles.dismissError}
                  onClick={() => setUploadError(null)}
                  title={labels.upload.dismissError}
                  aria-label={labels.upload.dismissError}
                >
                  <Icon icon="mdi:close" size={14} />
                </button>
              </div>
            )}
          </div>
        )}
        {(mode === 'preview' || mode === 'split') && (
          <div className={`${styles.previewPane} ${mode === 'split' ? styles.paneSplit : ''}`}>
            {value ? (
              <MarkdownRenderer content={value} stickerMap={mergedStickerMap} />
            ) : (
              <p className={styles.previewEmpty}>{labels.previewEmpty}</p>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
