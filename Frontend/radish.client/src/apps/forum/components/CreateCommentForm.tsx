import { type ClipboardEvent, useState, useRef, useCallback, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { buildAttachmentMarkdownUrl, escapeMarkdownLabel } from '@radish/ui';
import { StickerPicker, type StickerPickerGroup, type StickerPickerSelection } from '@radish/ui/sticker-picker';
import type { CommentReplyTarget } from '@/api/forum';
import { searchUsersForMention } from '@/api/user';
import { redirectToLogin } from '@/services/auth';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { UserMention, type UserMentionOption as UiUserMentionOption } from '@radish/ui/user-mention';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { uploadImage, uploadDocument } from '@/api/attachment';
import { getIntlLocale } from '@/locales/language';
import { resolveAttachmentUploadErrorMessage } from '@/attachments/attachmentPresentation';
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
  loginPromptText?: string;
  loginButtonText?: string;
  loginReturnPath?: string | null;
  onLoginRequired?: (returnPath?: string | null) => void;
  onTyping?: () => void;
  autoFocusKey?: string | null;
}

const MENTION_PANEL_WIDTH = 320;
const MENTION_PANEL_HEIGHT = 220;
const MENTION_PANEL_GAP = 10;
const VIEWPORT_PADDING = 12;

function appendRecoveryHint(message: string, hint: string): string {
  const trimmedMessage = message.trim();
  const trimmedHint = hint.trim();

  if (/[。.!！?？]$/.test(trimmedMessage)) {
    return `${trimmedMessage} ${trimmedHint}`;
  }

  const separator = /^[\u4e00-\u9fff]/.test(trimmedHint) ? '。' : '. ';
  return `${trimmedMessage}${separator}${trimmedHint}`;
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
  loginPromptText,
  loginButtonText,
  loginReturnPath,
  onLoginRequired,
  onTyping,
  autoFocusKey = null,
}: CreateCommentFormProps) => {
  const { t, i18n } = useTranslation();
  const [content, setContent] = useState('');
  const contentRef = useRef('');
  const textareaWrapperRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);
  const uploadInFlightRef = useRef(false);
  const lastTypingSentAtRef = useRef(0);
  const handledAutoFocusKeyRef = useRef<string | null>(null);

  // @提及功能状态
  const [showMention, setShowMention] = useState(false);
  const [mentionKeyword, setMentionKeyword] = useState('');
  const [mentionPosition, setMentionPosition] = useState({ top: 0, left: 0 });
  const [mentionStartPos, setMentionStartPos] = useState(0);

  // 上传状态
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [mode, setMode] = useState<'edit' | 'preview'>('edit');
  const [uploadError, setUploadError] = useState<string | null>(null);
  const resolvedTitle = title ?? t('forum.comment.title');
  const resolvedSubmitText = submitText ?? t('forum.submitDiscussion');
  const resolvedPlaceholder = placeholder ?? t('forum.discussionPlaceholder');
  const resolvedLoginPromptText = loginPromptText ?? t('forum.comment.loginPrompt');
  const resolvedLoginButtonText = loginButtonText ?? t('forum.comment.loginButton');
  const isEditorDisabled = !isAuthenticated || !hasPost || disabled || uploading;
  const isEditingDisabled = isEditorDisabled || mode === 'preview';
  const intlLocale = getIntlLocale(i18n.resolvedLanguage ?? i18n.language);
  const formattedContentLength = new Intl.NumberFormat(intlLocale).format(content.length);
  const contentLengthLabel = t('forum.comment.length', {
    count: content.length,
    value: formattedContentLength,
  });
  const uploadProgressLabel = uploadProgress === null
    ? t('forum.comment.uploading')
    : t('markdownEditor.upload.uploadingProgress', {
        progress: new Intl.NumberFormat(
          intlLocale,
          { style: 'percent', maximumFractionDigits: 0 },
        ).format(uploadProgress / 100),
      });

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

  useEffect(() => {
    if (
      !autoFocusKey
      || handledAutoFocusKeyRef.current === autoFocusKey
      || !textareaRef.current
      || !isAuthenticated
      || !hasPost
    ) {
      return;
    }

    handledAutoFocusKeyRef.current = autoFocusKey;
    textareaRef.current.focus();
    textareaRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [autoFocusKey, isAuthenticated, hasPost]);

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

  const notifyTyping = useCallback(() => {
    if (!onTyping || !isAuthenticated || !hasPost) {
      return;
    }

    const now = Date.now();
    if (now - lastTypingSentAtRef.current < 2000) {
      return;
    }

    lastTypingSentAtRef.current = now;
    onTyping();
  }, [hasPost, isAuthenticated, onTyping]);

  const handleLoginClick = () => {
    if (onLoginRequired) {
      onLoginRequired(loginReturnPath);
      return;
    }

    redirectToLogin({ returnPath: loginReturnPath });
  };

  // 检测@符号并触发用户搜索
  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newContent = e.target.value;
    const cursorPos = e.target.selectionStart;

    setContent(newContent);
    if (newContent.trim()) {
      notifyTyping();
    }

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
        userName: resolveVisibleUserHandle(user, resolveVisibleUserDisplayName(user, t('common.unknownUser')))
          || resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        displayName: resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        avatar: user.voAvatar
      }));
    } catch (error) {
      log.error(t('forum.comment.searchUsersFailed'), error);
      throw error;
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

  useEffect(() => {
    if (isEditorDisabled) {
      setShowMention(false);
    }
  }, [isEditorDisabled]);

  // 选择用户
  const handleSelectUser = (user: UiUserMentionOption) => {
    if (isEditorDisabled || !textareaRef.current) return;

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
    if (uploadInFlightRef.current) {
      return;
    }

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);

    try {
      const result = await uploadImage({
        file,
        businessType: 'Comment',
        generateThumbnail: true,
        generateMultipleSizes: false,
        removeExif: true,
        onProgress: setUploadProgress,
      }, t);

      // 评论区默认插入缩略图，点开查看原图
      const markdownUrl = buildAttachmentMarkdownUrl(result.voId, {
        displayVariant: result.voThumbnailUrl ? 'thumbnail' : 'original',
      });
      const imageLabel = escapeMarkdownLabel(file.name) || t('markdownEditor.insert.imageDescription');
      const imageMarkdown = `![${imageLabel}](${markdownUrl})`;
      insertTextAtRange(imageMarkdown, selectionStart, selectionEnd);
    } catch (error) {
      const errorMessage = appendRecoveryHint(
        resolveAttachmentUploadErrorMessage(error, t('forum.comment.imageUploadFailed')),
        t('forum.comment.uploadRecoverableHint')
      );
      setUploadError(errorMessage);
      log.error(t('forum.comment.imageUploadFailed'), error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
    }
  }, [insertTextAtRange, t]);

  // 处理图片上传
  const handleImageUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !textareaRef.current || isEditingDisabled || uploadInFlightRef.current) {
      e.currentTarget.value = '';
      return;
    }

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
    const textarea = textareaRef.current;
    if (!file || !textarea || isEditingDisabled || uploadInFlightRef.current) {
      e.currentTarget.value = '';
      return;
    }

    const selectionStart = textarea.selectionStart;
    const selectionEnd = textarea.selectionEnd;

    uploadInFlightRef.current = true;
    setUploading(true);
    setUploadProgress(null);
    setUploadError(null);

    try {
      const result = await uploadDocument({
        file,
        businessType: 'Comment',
        onProgress: setUploadProgress,
      }, t);

      // 插入 Markdown 链接语法
      const documentMarkdownUrl = buildAttachmentMarkdownUrl(result.voId);
      const documentLabel = escapeMarkdownLabel(result.voOriginalName || file.name) || t('markdownEditor.toolbar.document');
      const linkMarkdown = `[${documentLabel}](${documentMarkdownUrl})`;
      insertTextAtRange(linkMarkdown, selectionStart, selectionEnd);
    } catch (error) {
      const errorMessage = appendRecoveryHint(
        resolveAttachmentUploadErrorMessage(error, t('forum.comment.documentUploadFailed')),
        t('forum.comment.uploadRecoverableHint')
      );
      setUploadError(errorMessage);
      log.error(t('forum.comment.documentUploadFailed'), error);
    } finally {
      uploadInFlightRef.current = false;
      setUploading(false);
      setUploadProgress(null);
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
          {resolvedLoginPromptText}
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            {resolvedLoginButtonText}
          </button>
        </div>
      )}

      {variant === 'inline' && <h3 className={styles.title}>{resolvedTitle}</h3>}

      <div className={styles.editorContainer}>
        <div className={styles.editorTopBar}>
          <div className={styles.editorMeta}>
            {replyTo ? (
              <div className={styles.replyMeta}>
                <div className={styles.replyMetaBody}>
                  <span className={styles.replyText}>
                    {t('forum.comment.replyingPrefix')}
                    <span className={styles.replyTarget}>{replyTo.authorName}</span>
                  </span>
                  {replyTo.contentSnapshot ? (
                    <span className={styles.replyQuote}>{replyTo.contentSnapshot}</span>
                  ) : null}
                </div>
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
              <span className={styles.editorHint}>{t('forum.comment.editorHint')}</span>
            )}
          </div>

          <div className={styles.editorStatus}>
            <span className={styles.modeBadge}>
              {mode === 'preview' ? t('forum.comment.mode.preview') : t('forum.comment.mode.edit')}
            </span>
            <span className={styles.lengthHint}>{contentLengthLabel}</span>
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
              {content
                ? <MarkdownRenderer content={content} />
                : <div className={styles.previewEmpty}>{t('forum.comment.previewEmpty')}</div>}
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
              {!isEditorDisabled && showMention && (
                <UserMention
                  keyword={mentionKeyword}
                  onSearch={handleSearchUsers}
                  onSelect={handleSelectUser}
                  onClose={() => setShowMention(false)}
                  position={mentionPosition}
                  positionMode="absolute"
                  labels={{
                    title: t('forum.mention.title'),
                    loading: t('forum.mention.loading'),
                    inputHint: t('forum.mention.inputHint'),
                    empty: t('forum.mention.empty'),
                    searchFailed: t('forum.mention.searchFailed'),
                    selectHint: t('forum.mention.selectHint'),
                  }}
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
            disabled={isEditingDisabled}
          />
          <input
            ref={documentInputRef}
            type="file"
            accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip,.rar"
            onChange={handleDocumentUpload}
            style={{ display: 'none' }}
            disabled={isEditingDisabled}
          />

          <div className={styles.toolbarLeft}>
            <StickerPicker
              groups={stickerGroups}
              mode="insert"
              theme="light"
              panelPlacement="left"
              onSelect={handlePickerSelect}
              disabled={isEditingDisabled}
              className={styles.stickerPicker}
              triggerTitle={t('forum.comment.insertSticker')}
              labels={{
                searchPlaceholder: t('forum.reaction.searchPlaceholder'),
                clearSearch: t('forum.reaction.clearSearch'),
                reactionOnly: (name) => t('forum.reaction.reactionOnly', { name }),
                noEmoji: t('forum.reaction.noEmoji'),
                noSticker: t('forum.reaction.noSticker'),
              }}
            />

            <button
              type="button"
              onClick={handleImageButtonClick}
              disabled={isEditingDisabled}
              className={styles.toolbarButtonIcon}
              title={t('forum.comment.uploadImage')}
            >
              <Icon icon={uploading ? 'mdi:loading' : 'mdi:image-outline'} size={18} />
            </button>

            <button
              type="button"
              onClick={handleDocumentButtonClick}
              disabled={isEditingDisabled}
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
              title={mode === 'edit'
                ? t('markdownEditor.toolbar.preview')
                : t('markdownEditor.toolbar.edit')}
              aria-pressed={mode === 'preview'}
              disabled={uploading}
            >
              <Icon icon={mode === 'edit' ? 'mdi:eye-outline' : 'mdi:pencil-outline'} size={18} />
              <span>{mode === 'edit'
                ? t('markdownEditor.toolbar.preview')
                : t('markdownEditor.toolbar.edit')}</span>
            </button>

            {uploading && (
              <span className={styles.uploadingHint}>{uploadProgressLabel}</span>
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
