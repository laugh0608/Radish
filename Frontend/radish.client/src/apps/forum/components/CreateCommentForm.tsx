import { useState, useRef, useCallback, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { StickerPicker, type StickerPickerGroup, type StickerPickerSelection } from '@radish/ui/sticker-picker';
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
  replyTo?: { commentId: number; authorName: string } | null;
  onCancelReply?: () => void;
  variant?: 'inline' | 'sheet';
  stickerGroups?: StickerPickerGroup[];
  onStickerSelect?: (selection: StickerPickerSelection) => void;
  title?: string;
  submitText?: string;
  placeholder?: string;
}

const appendImageMeta = (displayUrl: string, fullUrl?: string, scalePercent?: number): string => {
  const params = new URLSearchParams();
  if (fullUrl) {
    params.set('full', fullUrl);
  }
  if (scalePercent && Number.isFinite(scalePercent)) {
    params.set('scale', String(Math.min(Math.max(scalePercent, 10), 100)));
  }
  const meta = params.toString();
  return meta ? `${displayUrl}#radish:${meta}` : displayUrl;
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
  const resolvedSubmitText = submitText ?? t('forum.comment.title');
  const resolvedPlaceholder = placeholder ?? t('forum.discussionPlaceholder');

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
    stickerCode: string,
    imageUrl?: string,
    thumbnailUrl?: string
  ): string => {
    const normalizedGroupCode = normalizeCode(groupCode);
    const normalizedStickerCode = normalizeCode(stickerCode);
    const params = new URLSearchParams();

    if (imageUrl) {
      params.set('image', imageUrl);
    }
    if (thumbnailUrl) {
      params.set('thumbnail', thumbnailUrl);
    }

    const meta = params.toString();
    const base = `sticker://${normalizedGroupCode}/${normalizedStickerCode}`;
    return meta ? `${base}#radish:${meta}` : base;
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
          const rect = textareaRef.current.getBoundingClientRect();
          setMentionPosition({
            top: rect.bottom,
            left: rect.left
          });
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

  // 插入文本到光标位置
  const insertTextAtCursor = (text: string) => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const newContent = content.substring(0, start) + text + content.substring(end);
    setContent(newContent);

    // 设置光标位置到插入文本后面
    setTimeout(() => {
      const newCursorPos = start + text.length;
      textarea.focus();
      textarea.setSelectionRange(newCursorPos, newCursorPos);
    }, 0);
  };

  // 处理图片上传
  const handleImageUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

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
      const displayUrl = result.voThumbnailUrl || result.voUrl;
      const markdownUrl = appendImageMeta(displayUrl, result.voUrl);
      const imageMarkdown = `![${file.name}](${markdownUrl})`;
      insertTextAtCursor(imageMarkdown);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : t('forum.comment.imageUploadFailed');
      setUploadError(errorMessage);
      log.error(t('forum.comment.imageUploadFailed'), error);
    } finally {
      setUploading(false);
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
      const linkMarkdown = `[${result.voOriginalName || file.name}](${result.voUrl})`;
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
    const stickerUrl = buildStickerMarkdownUrl(
      groupCode,
      stickerCode,
      selection.imageUrl,
      selection.thumbnailUrl
    );
    insertTextAtCursor(`![${altText}](${stickerUrl})`);
    onStickerSelect?.(selection);
  };

  const containerClassName = `${styles.container} ${variant === 'sheet' ? styles.containerSheet : ''}`;
  const titleClassName = `${styles.title} ${variant === 'sheet' ? styles.titleSheet : ''}`;
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

      

            <div className={styles.editorContainer}>
        {replyTo && (
          <div className={styles.replyHint}>
            <span className={styles.replyText}>
              回复给 <span className={styles.replyTarget}>@{replyTo.authorName}</span>
            </span>
            {onCancelReply && (
              <button type="button" onClick={onCancelReply} className={styles.cancelReplyButton} title={t('forum.comment.cancelReply')}>
                <Icon icon="mdi:close" size={14} />
              </button>
            )}
          </div>
        )}

        {uploadError && (
          <div className={styles.uploadError}>
            <Icon icon="mdi:alert-circle" size={16} />
            <span>{uploadError}</span>
          </div>
        )}

        <div className={styles.textareaWrapper}>
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
                rows={3}
                className={styles.textarea}
                disabled={!isAuthenticated || !hasPost || disabled || uploading}
              />
              {showMention && (
                <UserMention
                  keyword={mentionKeyword}
                  onSearch={handleSearchUsers}
                  onSelect={handleSelectUser}
                  onClose={() => setShowMention(false)}
                  position={mentionPosition}
                />
              )}
            </>
          )}
        </div>

        <div className={styles.actionBar}>
          <div className={styles.toolbarLeft}>
            <input
              ref={imageInputRef}
              type="file"
              accept="image/*"
              onChange={handleImageUpload}
              style={{ display: 'none' }}
            />

            <StickerPicker
              groups={stickerGroups}
              mode="insert"
              theme="light"
              panelPlacement="left"
              onSelect={handlePickerSelect}
              disabled={!isAuthenticated || !hasPost || disabled || uploading}
              className={styles.stickerPicker}
              triggerTitle={t('forum.comment.insertSticker')}
            />

            <button
              type="button"
              onClick={handleImageButtonClick}
              disabled={!isAuthenticated || !hasPost || disabled || uploading}
              className={styles.toolbarButtonIcon}
              title={t('forum.comment.uploadImage')}
            >
              <Icon icon={uploading ? "mdi:loading" : "mdi:image-outline"} size={20} />
            </button>

            <button
              type="button"
              className={`${styles.toolbarButtonIcon} ${mode === 'preview' ? styles.activeIcon : ''}`}
              onClick={() => setMode(mode === 'edit' ? 'preview' : 'edit')}
              title={mode === 'edit' ? "预览" : "继续编辑"}
            >
              <Icon icon={mode === 'edit' ? "mdi:eye-outline" : "mdi:pencil-outline"} size={20} />
            </button>

            {uploading && (
              <span className={styles.uploadingHint}>{t('forum.comment.uploading')}</span>
            )}
          </div>

          <button
            type="button"
            onClick={handleSubmit}
            disabled={!isAuthenticated || !hasPost || disabled || !content.trim() || uploading}
            className={submitClassName}
          >
            发布
          </button>
        </div>
      </div>
    </div>
  );
};
