import { useState, useEffect, useCallback, useMemo } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import type { UserMentionOption as UiUserMentionOption } from '@radish/ui';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { searchUsersForMention } from '@/api/user';
import { uploadImage, uploadDocument } from '@/api/attachment';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import { redirectToLogin } from '@/services/auth';
import { buildDesktopForumReturnPath } from '@/services/authReturnPath';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import styles from './PublishPostForm.module.css';

interface PublishPostFormProps {
  isAuthenticated: boolean;
  userName?: string;
  onPublish: (title: string, content: string) => void;
  disabled?: boolean;
}

const DRAFT_STORAGE_KEY = 'forum_post_draft';
const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;

export const PublishPostForm = ({
  isAuthenticated,
  onPublish,
  disabled = false
}: PublishPostFormProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [imageScalePercent, setImageScalePercent] = useState<number>(75);
  const [editorUploading, setEditorUploading] = useState(false);
  const { t, i18n } = useTranslation();
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();
  const markdownEditorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );

  const handleSearchUsers = useCallback(async (keyword: string): Promise<UiUserMentionOption[]> => {
    try {
      const users = await searchUsersForMention(keyword, t);
      return users.map((user) => ({
        id: user.voId,
        userName: resolveVisibleUserHandle(user, resolveVisibleUserDisplayName(user, t('common.unknownUser')))
          || resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        displayName: resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        avatar: user.voAvatar
      }));
    } catch (error) {
      log.error(t('forum.mention.searchFailed'), error);
      throw error;
    }
  }, [t]);

  // 组件加载时恢复草稿
  useEffect(() => {
    try {
      const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
      if (savedDraft) {
        const draft = JSON.parse(savedDraft);
        if (draft.title || draft.content) {
          setTitle(draft.title || '');
          setContent(draft.content || '');
        }
      }
    } catch (err) {
      log.error('Failed to load draft:', err);
    }
  }, []);

  // 自动保存草稿（标题或内容变化时）
  useEffect(() => {
    if (title || content) {
      try {
        localStorage.setItem(
          DRAFT_STORAGE_KEY,
          JSON.stringify({ title, content, savedAt: Date.now() })
        );
      } catch (err) {
        log.error('Failed to save draft:', err);
      }
    }
  }, [title, content]);

  const handleSubmit = () => {
    if (editorUploading || !title.trim() || !content.trim()) {
      return;
    }
    onPublish(title, content);
    // 发布成功后清空表单和草稿
    setTitle('');
    setContent('');
    try {
      localStorage.removeItem(DRAFT_STORAGE_KEY);
    } catch (err) {
      log.error('Failed to clear draft:', err);
    }
  };

  const handleLoginClick = () => {
    redirectToLogin({ returnPath: buildDesktopForumReturnPath() });
  };

  // 处理图片上传
  const handleImageUpload = async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownImageUploadResult> => {
    const result = await uploadImage({
      file,
      businessType: 'Post',
      generateThumbnail: true,
      generateMultipleSizes,
      addWatermark,
      watermarkText,
      removeExif: true,
      onProgress: reportProgress,
    }, t);

    return {
      attachmentId: result.voId,
      displayVariant: 'original',
      previewUrl: buildAttachmentAssetUrl(result.voId, 'original'),
      scalePercent: imageScalePercent,
    };
  };

  // 处理文档上传
  const handleDocumentUpload = async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownDocumentUploadResult> => {
    const result = await uploadDocument({
      file,
      businessType: 'Post',
      onProgress: reportProgress,
    }, t);

    return {
      attachmentId: result.voId,
      fileName: result.voOriginalName || file.name
    };
  };

  const handleEditorUploadError = useCallback((kind: 'image' | 'document', error: unknown) => {
    log.error('PublishPostForm', `Markdown ${kind} upload failed:`, error);
  }, []);

  const handleEditorUploadingChange = useCallback((uploading: boolean) => {
    setEditorUploading(uploading);
  }, []);

  return (
    <div className={styles.container}>
      {!isAuthenticated && (
        <div className={styles.loginPrompt}>
          当前未登录，无法发帖。
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            去登录
          </button>
        </div>
      )}

      <input
        type="text"
        placeholder="帖子标题"
        value={title}
        onChange={e => setTitle(e.target.value)}
        className={styles.input}
        disabled={!isAuthenticated || disabled}
      />

      {isAuthenticated && (
        <details className={styles.advancedOptions}>
          <summary>{t('forum.editor.imageOptions')}</summary>
          <div className={styles.uploadOptions}>
            <label className={styles.optionLabel}>
              <input
                type="checkbox"
                checked={addWatermark}
                onChange={e => setAddWatermark(e.target.checked)}
                disabled={disabled || editorUploading}
              />
              <span>{t('forum.editor.addWatermark')}</span>
            </label>
            {addWatermark && (
              <input
                type="text"
                placeholder={t('forum.editor.watermarkPlaceholder')}
                value={watermarkText}
                onChange={e => setWatermarkText(e.target.value)}
                className={styles.watermarkInput}
                disabled={disabled || editorUploading}
              />
            )}
            <label className={styles.optionLabel}>
              <input
                type="checkbox"
                checked={generateMultipleSizes}
                onChange={e => setGenerateMultipleSizes(e.target.checked)}
                disabled={disabled || editorUploading}
              />
              <span>{t('forum.editor.generateMultipleSizes')}</span>
            </label>
            <label className={styles.optionLabel}>
              <span>{t('forum.editor.scale')}</span>
              <select
                className={styles.imageScaleSelect}
                value={imageScalePercent}
                onChange={e => setImageScalePercent(Number(e.target.value))}
                disabled={disabled || editorUploading}
              >
                {IMAGE_SCALE_OPTIONS.map(scale => (
                  <option key={scale} value={scale}>{scale}%</option>
                ))}
              </select>
            </label>
          </div>
        </details>
      )}

      <MarkdownEditor
        value={content}
        onChange={setContent}
        labels={markdownEditorLabels}
        minHeight={300}
        disabled={!isAuthenticated || disabled}
        showToolbar={true}
        onImageUpload={handleImageUpload}
        onDocumentUpload={handleDocumentUpload}
        onUploadError={handleEditorUploadError}
        onUploadingChange={handleEditorUploadingChange}
        stickerGroups={stickerGroups}
        stickerMap={stickerMap}
        onStickerSelect={(selection) => {
          void handleStickerSelect(selection);
        }}
        onUserMentionSearch={handleSearchUsers}
      />



      <button
        type="button"
        onClick={handleSubmit}
        disabled={!isAuthenticated || disabled || editorUploading || !title.trim() || !content.trim()}
        className={styles.submitButton}
      >
        发布帖子
      </button>
    </div>
  );
};
