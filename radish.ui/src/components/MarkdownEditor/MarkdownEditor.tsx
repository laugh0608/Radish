import { useState, useRef, KeyboardEvent, ChangeEvent, ClipboardEvent, DragEvent } from 'react';
import { Icon } from '../Icon/Icon';
import { MarkdownRenderer } from '../MarkdownRenderer/MarkdownRenderer';
import styles from './MarkdownEditor.module.css';

export interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minHeight?: number;
  maxHeight?: number;
  disabled?: boolean;
  showToolbar?: boolean;
  className?: string;
  /**
   * 图片上传处理函数
   * 如果不提供，图片按钮将插入默认的 Markdown 语法
   */
  onImageUpload?: (file: File) => Promise<{ url: string; thumbnailUrl?: string }>;
}

type ToolbarAction = 'bold' | 'italic' | 'strikethrough' | 'heading' | 'quote' | 'code' | 'codeblock' | 'ul' | 'ol' | 'link' | 'image' | 'hr';

export const MarkdownEditor = ({
  value,
  onChange,
  placeholder = '输入内容，支持 Markdown...',
  minHeight = 150,
  maxHeight,
  disabled = false,
  showToolbar = true,
  className = '',
  onImageUpload
}: MarkdownEditorProps) => {
  const [mode, setMode] = useState<'edit' | 'preview'>('edit');
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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

  // 插入文本
  const insertText = (before: string, after: string = '', placeholder: string = '') => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = value.substring(start, end);
    const textToInsert = selectedText || placeholder;

    const newValue = value.substring(0, start) + before + textToInsert + after + value.substring(end);
    onChange(newValue);

    // 设置新的光标位置
    setTimeout(() => {
      const newCursorPos = start + before.length + textToInsert.length;
      textarea.focus();
      textarea.setSelectionRange(newCursorPos, newCursorPos);
    }, 0);
  };

  // 处理图片上传
  const handleImageUpload = async (file: File) => {
    if (!onImageUpload) {
      // 如果没有提供上传函数，使用默认行为
      insertText('![', '](url)', '图片描述');
      return;
    }

    setUploading(true);
    setUploadError(null);

    try {
      const result = await onImageUpload(file);
      const imageUrl = result.thumbnailUrl || result.url;

      // 插入图片 Markdown 语法
      insertText(`![${file.name}](${imageUrl})`, '', '');

      setUploading(false);
    } catch (error) {
      setUploading(false);
      setUploadError(error instanceof Error ? error.message : '上传失败');
      console.error('图片上传失败:', error);
    }
  };

  // 处理文件选择
  const handleFileInputChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && file.type.startsWith('image/')) {
      await handleImageUpload(file);
    }
    // 清空文件输入，允许重复选择同一个文件
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // 处理粘贴事件
  const handlePaste = async (e: ClipboardEvent<HTMLTextAreaElement>) => {
    if (!onImageUpload) return;

    const items = e.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.startsWith('image/')) {
        e.preventDefault();
        const file = item.getAsFile();
        if (file) {
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
    if (file.type.startsWith('image/')) {
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
    switch (action) {
      case 'bold':
        insertText('**', '**', '粗体文本');
        break;
      case 'italic':
        insertText('*', '*', '斜体文本');
        break;
      case 'strikethrough':
        insertText('~~', '~~', '删除线文本');
        break;
      case 'heading':
        insertText('## ', '', '标题');
        break;
      case 'quote':
        insertText('> ', '', '引用文本');
        break;
      case 'code':
        insertText('`', '`', '代码');
        break;
      case 'codeblock':
        insertText('```\n', '\n```', '代码块');
        break;
      case 'ul':
        insertText('- ', '', '列表项');
        break;
      case 'ol':
        insertText('1. ', '', '列表项');
        break;
      case 'link':
        insertText('[', '](url)', '链接文本');
        break;
      case 'image':
        if (onImageUpload) {
          // 如果提供了上传函数，触发文件选择
          fileInputRef.current?.click();
        } else {
          // 否则插入默认模板
          insertText('![', '](url)', '图片描述');
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

    setShowEmojiPicker(false);
  };

  const handleChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    onChange(e.target.value);
  };

  const containerStyle: React.CSSProperties = {
    minHeight: `${minHeight}px`,
    ...(maxHeight ? { maxHeight: `${maxHeight}px` } : {})
  };

  return (
    <div className={`${styles.container} ${className}`}>
      {/* 隐藏的文件输入 */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        style={{ display: 'none' }}
        onChange={handleFileInputChange}
      />

      {/* 工具栏 */}
      {showToolbar && (
        <div className={styles.toolbar}>
          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('heading')}
              title="标题 (##)"
              disabled={disabled}
            >
              <Icon icon="mdi:format-header-pound" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('bold')}
              title="加粗 (Ctrl+B)"
              disabled={disabled}
            >
              <Icon icon="mdi:format-bold" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('italic')}
              title="斜体 (Ctrl+I)"
              disabled={disabled}
            >
              <Icon icon="mdi:format-italic" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('strikethrough')}
              title="删除线"
              disabled={disabled}
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
              title="引用"
              disabled={disabled}
            >
              <Icon icon="mdi:format-quote-close" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('code')}
              title="行内代码"
              disabled={disabled}
            >
              <Icon icon="mdi:code-tags" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('codeblock')}
              title="代码块"
              disabled={disabled}
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
              title="无序列表"
              disabled={disabled}
            >
              <Icon icon="mdi:format-list-bulleted" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('ol')}
              title="有序列表"
              disabled={disabled}
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
              title="链接 (Ctrl+K)"
              disabled={disabled}
            >
              <Icon icon="mdi:link" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('image')}
              title="图片"
              disabled={disabled}
            >
              <Icon icon="mdi:image" size={18} />
            </button>
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => handleToolbarAction('hr')}
              title="分割线"
              disabled={disabled}
            >
              <Icon icon="mdi:minus" size={18} />
            </button>
          </div>

          <div className={styles.toolbarDivider} />

          <div className={styles.toolbarGroup}>
            <div className={styles.emojiPickerWrapper}>
              <button
                type="button"
                className={styles.toolbarButton}
                onClick={() => setShowEmojiPicker(!showEmojiPicker)}
                title="Emoji"
                disabled={disabled}
              >
                <Icon icon="mdi:emoticon-happy-outline" size={18} />
              </button>
              {showEmojiPicker && (
                <div className={styles.emojiPicker}>
                  <div className={styles.emojiGrid}>
                    {emojis.map((emoji, index) => (
                      <button
                        key={index}
                        type="button"
                        className={styles.emojiButton}
                        onClick={() => insertEmoji(emoji)}
                      >
                        {emoji}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className={styles.toolbarSpacer} />

          <div className={styles.toolbarGroup}>
            <button
              type="button"
              className={`${styles.toolbarButton} ${mode === 'edit' ? styles.active : ''}`}
              onClick={() => setMode('edit')}
              title="编辑"
            >
              <Icon icon="mdi:pencil" size={18} />
            </button>
            <button
              type="button"
              className={`${styles.toolbarButton} ${mode === 'preview' ? styles.active : ''}`}
              onClick={() => setMode('preview')}
              title="预览"
            >
              <Icon icon="mdi:eye" size={18} />
            </button>
          </div>
        </div>
      )}

      {/* 编辑/预览区域 */}
      <div className={styles.content} style={containerStyle}>
        {mode === 'edit' ? (
          <>
            <textarea
              ref={textareaRef}
              className={styles.textarea}
              value={value}
              onChange={handleChange}
              onKeyDown={handleKeyDown}
              onPaste={handlePaste}
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              placeholder={placeholder}
              disabled={disabled || uploading}
            />
            {uploading && (
              <div className={styles.uploadingOverlay}>
                <Icon icon="mdi:loading" size={24} className={styles.spinIcon} />
                <span>上传中...</span>
              </div>
            )}
            {uploadError && (
              <div className={styles.uploadError}>
                <Icon icon="mdi:alert-circle" size={16} />
                <span>{uploadError}</span>
                <button
                  type="button"
                  className={styles.dismissError}
                  onClick={() => setUploadError(null)}
                >
                  <Icon icon="mdi:close" size={14} />
                </button>
              </div>
            )}
          </>
        ) : (
          <div className={styles.preview}>
            {value ? (
              <MarkdownRenderer content={value} />
            ) : (
              <p className={styles.previewEmpty}>没有内容可预览</p>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
