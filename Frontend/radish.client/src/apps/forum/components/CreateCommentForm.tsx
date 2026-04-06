import { type ClipboardEvent, useState, useRef, useCallback, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { buildAttachmentMarkdownUrl } from '@radish/ui';
import { StickerPicker, type StickerPickerGroup, type StickerPickerSelection } from '@radish/ui/sticker-picker';
import type { CommentReplyTarget } from '@/api/forum';
import { getOidcLoginUrl } from '@/api/forum';
import { searchUsersForMention } from '@/api/user';
import { UserMention, type UserMentionOption as UiUserMentionOption } from '@radish/ui/user-mention';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { uploadImage, uploadDocument } from '@/api/attachment';
import styles from './CreateCommentForm.module.css';

interface CreateCommentFormProps {
  isAuthenticated: boolean;
  hasPost: boolean;
  onSubmit: (content: string) => void;
  disabled?: boolean;
  replyTo?: CommentReplyTarget | null;
  onCancelReply?: () => void;
  variant?: 'inline' | 'sheet';
  stickerGroups?: StickerPickerGroup[];
  onStickerSelect?: (selection: StickerPickerSelection) => void;
  title?: string;
  submitText?: string;
  placeholder?: string;
}

const MENTION_PANEL_WIDTH = 320;
const MENTION_PANEL_HEIGHT = 220;
const MENTION_PANEL_GAP = 10;
const VIEWPORT_PADDING = 12;

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

  mirror.style.position = 'fixed';
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

export const CreateCommentForm = ({
  isAuthenticated,
  hasPost,
  onSubmit,
  disabled = false,
  replyTo = null,
  onCancelReply,
  variant = 'inline',
  stickerGroups = [],
  onStickerSelect,
  title,
  submitText,
  placeholder,
}: CreateCommentFormProps) => {
  const { t } = useTranslation();
  const [content, setContent] = useState('');
  const contentRef = useRef('');
  const textareaWrapperRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);

  // @提及功能状态
  const [showMention, setShowMention] = useState(false);
  const [mentionKeyword, setMentionKeyword] = useState('');
  const [mentionPosition, setMentionPosition] = useState({ top: 0, left: 0 });
  const [mentionStartPos, setMentionStartPos] = useState(0);

  // 上传状态
  const [uploading, setUploading] = useState(false);
  const [mode, setMode] = useState<'edit' | 'preview'>('edit');
  const [uploadError, setUploadError] = useState<string | null>(null);
  const resolvedTitle = title ?? t('forum.comment.title');
  const resolvedSubmitText = submitText ?? t('forum.submitDiscussion');
  const resolvedPlaceholder = placeholder ?? t('forum.discussionPlaceholder');
  const isEditorDisabled = !isAuthenticated || !hasPost || disabled || uploading;

  useEffect(() => {
    contentRef.current = content;
  }, [content]);

  const updateMentionPosition = useCallback((textarea: HTMLTextAreaElement, cursorPos?: number) => {
    const wrapper = textareaWrapperRef.current;
    if (!wrapper) {
      return;
    }

    const selectionStart = cursorPos ?? textarea.selectionStart ?? 0;
    const caretPosition = getTextareaCaretRelativePosition(textarea, selectionStart);
    const fallbackTop = 56;
    const fallbackLeft = 18;
    const baseTop = caretPosition?.top ?? fallbackTop;
    const baseLeft = caretPosition?.left ?? fallbackLeft;
    const lineHeight = caretPosition?.lineHeight ?? 24;
    const wrapperWidth = wrapper.clientWidth;
    const wrapperHeight = wrapper.clientHeight;
    const left = Math.max(
      VIEWPORT_PADDING,
      Math.min(baseLeft - 14, wrapperWidth - MENTION_PANEL_WIDTH - VIEWPORT_PADDING)
    );
    let top = baseTop + lineHeight + MENTION_PANEL_GAP;

    if (top + MENTION_PANEL_HEIGHT > wrapperHeight - VIEWPORT_PADDING) {
      top = baseTop - MENTION_PANEL_HEIGHT - MENTION_PANEL_GAP;
    }

    top = Math.max(
      VIEWPORT_PADDING,
      Math.min(top, wrapperHeight - MENTION_PANEL_HEIGHT - VIEWPORT_PADDING)
    );

    setMentionPosition({
      top,
      left
    });
  }, []);

  useEffect(() => {
    if (!replyTo || !textareaRef.current || !isAuthenticated || !hasPost) {
      return;
    }

    textareaRef.current.focus();
    textareaRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [replyTo, isAuthenticated, hasPost]);

  const normalizeCode = (value: string): string => value.trim().toLowerCase();

  const escapeMarkdownAlt = (value: string): string => value.replace(/[\][]/g, '').trim();

  const buildStickerMarkdownUrl = (
    groupCode: string,
    stickerCode: string
  ): string => {
    const normalizedGroupCode = normalizeCode(groupCode);
    const normalizedStickerCode = normalizeCode(stickerCode);
    return `sticker://${normalizedGroupCode}/${normalizedStickerCode}`;
  };

  const handleSubmit = () => {
    if (!content.trim()) {
      return;
    }
    onSubmit(content);
    setContent('');
    setShowMention(false);
  };

  const handleLoginClick = () => {
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  // 检测@符号并触发用户搜索
  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newContent = e.target.value;
    const cursorPos = e.target.selectionStart;

    setContent(newContent);

    // 查找光标前最近的@符号
    const textBeforeCursor = newContent.substring(0, cursorPos);
    const lastAtIndex = textBeforeCursor.lastIndexOf('@');

    // 如果找到@符号，并且@符号后面没有空格
    if (lastAtIndex !== -1) {
      const textAfterAt = textBeforeCursor.substring(lastAtIndex + 1);

      // @符号后面不能有空格或换行
      if (!/[\s\n]/.test(textAfterAt)) {
        setMentionKeyword(textAfterAt);
        setMentionStartPos(lastAtIndex);
        setShowMention(true);

        // 计算下拉框位置
        if (textareaRef.current) {
          updateMentionPosition(textareaRef.current, cursorPos);
        }
        return;
      }
    }

    // 没有匹配到@，关闭下拉框
    setShowMention(false);
  };

  // 搜索用户
  const handleSearchUsers = useCallback(async (keyword: string): Promise<UiUserMentionOption[]> => {
    try {
      const users = await searchUsersForMention(keyword, t);
      return users.map(user => ({
        id: user.voId,
        userName: user.voUserName,
        displayName: user.voDisplayName,
        avatar: user.voAvatar
      }));
    } catch (error) {
      log.error(t('forum.comment.searchUsersFailed'), error);
      return [];
    }
  }, [t]);

  useEffect(() => {
    if (!showMention || !textareaRef.current) {
      return;
    }

    const syncPosition = () => {
      if (textareaRef.current) {
        updateMentionPosition(textareaRef.current);
      }
    };

    syncPosition();
    window.addEventListener('resize', syncPosition);

    return () => {
      window.removeEventListener('resize', syncPosition);
    };
  }, [showMention, updateMentionPosition]);

  // 选择用户
  const handleSelectUser = (user: UiUserMentionOption) => {
    if (!textareaRef.current) return;

    // 替换@和关键词为@用户名
    const beforeMention = content.substring(0, mentionStartPos);
    const afterMention = content.substring(textareaRef.current.selectionStart);
    const newContent = `${beforeMention}@${user.userName} ${afterMention}`;

    setContent(newContent);
    setShowMention(false);

    // 设置光标位置到@用户名后面
    setTimeout(() => {
      if (textareaRef.current) {
        const cursorPos = mentionStartPos + user.userName.length + 2; // @ + 用户名 + 空格
        textareaRef.current.selectionStart = cursorPos;
        textareaRef.current.selectionEnd = cursorPos;
        textareaRef.current.focus();
      }
    }, 0);
  };

  const insertTextAtRange = useCallback((text: string, start: number, end: number) => {
    const textarea = textareaRef.current;
    const currentContent = contentRef.current;
    const safeStart = Math.max(0, Math.min(start, currentContent.length));
    const safeEnd = Math.max(safeStart, Math.min(end, currentContent.length));
    const newContent = currentContent.substring(0, safeStart) + text + currentContent.substring(safeEnd);

    setContent(newContent);
    contentRef.current = newContent;

    setTimeout(() => {
      if (!textarea) {
        return;
      }

      const newCursorPos = safeStart + text.length;
      textarea.focus();
      textarea.setSelectionRange(newCursorPos, newCursorPos);
    }, 0);
  }, []);

  // 插入文本到光标位置
  const insertTextAtCursor = (text: string) => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    insertTextAtRange(text, textarea.selectionStart, textarea.selectionEnd);
  };

  const uploadImageFile = useCallback(async (file: File, selectionStart: number, selectionEnd: number) => {
    setUploading(true);
    setUploadError(null);

    try {
      const result = await uploadImage({
        file,
        businessType: 'Comment',
        generateThumbnail: true,
        generateMultipleSizes: false,
        removeExif: true
      }, t);

      // 评论区默认插入缩略图，点开查看原图
      const markdownUrl = buildAttachmentMarkdownUrl(result.voId, {
        displayVariant: result.voThumbnailUrl ? 'thumbnail' : 'original',
      });
      const imageMarkdown = `![${file.name}](${markdownUrl})`;
      insertTextAtRange(imageMarkdown, selectionStart, selectionEnd);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : t('forum.comment.imageUploadFailed');
      setUploadError(errorMessage);
      log.error(t('forum.comment.imageUploadFailed'), error);
    } finally {
      setUploading(false);
    }
  }, [insertTextAtRange, t]);

  // 处理图片上传
  const handleImageUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !textareaRef.current) return;

    const selectionStart = textareaRef.current.selectionStart;
    const selectionEnd = textareaRef.current.selectionEnd;

    try {
      await uploadImageFile(file, selectionStart, selectionEnd);
    } finally {
      // 清空 input 以允许重复上传同一文件
      if (imageInputRef.current) {
        imageInputRef.current.value = '';
      }
    }
  };

  // 处理文档上传
  const handleDocumentUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    setUploadError(null);

    try {
      const result = await uploadDocument({
        file,
        businessType: 'Comment'
      }, t);

      // 插入 Markdown 链接语法
      const documentMarkdownUrl = buildAttachmentMarkdownUrl(result.voId);
      const linkMarkdown = `[${result.voOriginalName || file.name}](${documentMarkdownUrl})`;
      insertTextAtCursor(linkMarkdown);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : t('forum.comment.documentUploadFailed');
      setUploadError(errorMessage);
      log.error(t('forum.comment.documentUploadFailed'), error);
    } finally {
      setUploading(false);
      // 清空 input 以允许重复上传同一文件
      if (documentInputRef.current) {
        documentInputRef.current.value = '';
      }
    }
  };

  // 触发文件选择
  const handleImageButtonClick = () => {
    imageInputRef.current?.click();
  };

  const handleDocumentButtonClick = () => {
    documentInputRef.current?.click();
  };

  const handleTextareaPaste = useCallback(async (event: ClipboardEvent<HTMLTextAreaElement>) => {
    if (isEditorDisabled) {
      return;
    }

    const clipboardItems = Array.from(event.clipboardData.items);
    const imageItem = clipboardItems.find((item) => item.type.startsWith('image/'));
    const pastedImage = imageItem?.getAsFile();

    if (!pastedImage) {
      return;
    }

    event.preventDefault();
    await uploadImageFile(pastedImage, event.currentTarget.selectionStart, event.currentTarget.selectionEnd);
  }, [isEditorDisabled, uploadImageFile]);

  const handlePickerSelect = (selection: StickerPickerSelection) => {
    if (selection.type === 'unicode') {
      if (!selection.emoji) {
        return;
      }

      insertTextAtCursor(selection.emoji);
      onStickerSelect?.(selection);
      return;
    }

    const groupCode = selection.groupCode?.trim();
    const stickerCode = selection.stickerCode?.trim();
    if (!groupCode || !stickerCode) {
      return;
    }

    const altText = escapeMarkdownAlt(selection.stickerName || stickerCode) || stickerCode;
    const stickerUrl = buildStickerMarkdownUrl(groupCode, stickerCode);
    insertTextAtCursor(`![${altText}](${stickerUrl})`);
    onStickerSelect?.(selection);
  };

  const containerClassName = `${styles.container} ${variant === 'sheet' ? styles.containerSheet : ''}`;
  const submitClassName = `${styles.submitButton} ${variant === 'sheet' ? styles.submitButtonSheet : ''}`;

  return (
    <div className={containerClassName}>
      {!isAuthenticated && (
        <div className={styles.loginPrompt}>
          {t('forum.comment.loginPrompt')}
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            {t('forum.comment.loginButton')}
          </button>
        </div>
      )}

      {variant === 'inline' && <h3 className={styles.title}>{resolvedTitle}</h3>}

      <div className={styles.editorContainer}>
        <div className={styles.editorTopBar}>
          <div className={styles.editorMeta}>
            {replyTo ? (
              <div className={styles.replyMeta}>
                <span className={styles.replyText}>
                  回复给 <span className={styles.replyTarget}>@{replyTo.authorName}</span>
                </span>
                {onCancelReply && (
                  <button
                    type="button"
                    onClick={onCancelReply}
                    className={styles.cancelReplyButton}
                    title={t('forum.comment.cancelReply')}
                  >
                    <Icon icon="mdi:close" size={14} />
                  </button>
                )}
              </div>
            ) : (
              <span className={styles.editorHint}>支持 Markdown、@ 提及、图片和附件</span>
            )}
          </div>

          <div className={styles.editorStatus}>
            <span className={styles.modeBadge}>{mode === 'preview' ? '预览' : '编辑'}</span>
            <span className={styles.lengthHint}>{content.length} 字</span>
          </div>
        </div>

        {replyTo && (
          <div className={styles.replyDivider} />
        )}

        {uploadError && (
          <div className={styles.uploadError}>
            <Icon icon="mdi:alert-circle" size={16} />
            <span>{uploadError}</span>
          </div>
        )}

        <div ref={textareaWrapperRef} className={styles.textareaWrapper}>
          {mode === 'preview' ? (
            <div className={styles.previewContainer}>
              {content ? <MarkdownRenderer content={content} /> : <div className={styles.previewEmpty}>没有任何内容</div>}
            </div>
          ) : (
            <>
              <textarea
                ref={textareaRef}
                placeholder={resolvedPlaceholder}
                value={content}
                onChange={handleTextChange}
                onClick={(event) => {
                  if (showMention) {
                    updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                  }
                }}
                onKeyUp={(event) => {
                  if (showMention) {
                    updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                  }
                }}
                onScroll={(event) => {
                  if (showMention) {
                    updateMentionPosition(event.currentTarget, event.currentTarget.selectionStart);
                  }
                }}
                onPaste={(event) => {
                  void handleTextareaPaste(event);
                }}
                rows={5}
                className={styles.textarea}
                disabled={isEditorDisabled}
              />
              {showMention && (
                <UserMention
                  keyword={mentionKeyword}
                  onSearch={handleSearchUsers}
                  onSelect={handleSelectUser}
                  onClose={() => setShowMention(false)}
                  position={mentionPosition}
                  positionMode="absolute"
                />
              )}
            </>
          )}
        </div>

        <div className={styles.actionBar}>
          <input
            ref={imageInputRef}
            type="file"
            accept="image/*"
            onChange={handleImageUpload}
            style={{ display: 'none' }}
          />
          <input
            ref={documentInputRef}
            type="file"
            accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip,.rar"
            onChange={handleDocumentUpload}
            style={{ display: 'none' }}
          />

          <div className={styles.toolbarLeft}>
            <StickerPicker
              groups={stickerGroups}
              mode="insert"
              theme="light"
              panelPlacement="left"
              onSelect={handlePickerSelect}
              disabled={isEditorDisabled}
              className={styles.stickerPicker}
              triggerTitle={t('forum.comment.insertSticker')}
            />

            <button
              type="button"
              onClick={handleImageButtonClick}
              disabled={isEditorDisabled}
              className={styles.toolbarButtonIcon}
              title={t('forum.comment.uploadImage')}
            >
              <Icon icon={uploading ? 'mdi:loading' : 'mdi:image-outline'} size={18} />
            </button>

            <button
              type="button"
              onClick={handleDocumentButtonClick}
              disabled={isEditorDisabled}
              className={styles.toolbarButtonIcon}
              title={t('forum.comment.uploadDocument')}
            >
              <Icon icon="mdi:file-document-outline" size={18} />
            </button>
          </div>

          <div className={styles.toolbarRight}>
            <button
              type="button"
              className={`${styles.modeToggleButton} ${mode === 'preview' ? styles.activeIcon : ''}`}
              onClick={() => setMode(mode === 'edit' ? 'preview' : 'edit')}
              title={mode === 'edit' ? '预览' : '继续编辑'}
              aria-pressed={mode === 'preview'}
            >
              <Icon icon={mode === 'edit' ? 'mdi:eye-outline' : 'mdi:pencil-outline'} size={18} />
              <span>{mode === 'edit' ? '预览' : '编辑'}</span>
            </button>

            {uploading && (
              <span className={styles.uploadingHint}>{t('forum.comment.uploading')}</span>
            )}

            <button
              type="button"
              onClick={handleSubmit}
              disabled={!isAuthenticated || !hasPost || disabled || !content.trim() || uploading}
              className={submitClassName}
            >
              {resolvedSubmitText}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
