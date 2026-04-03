import { lazy, Suspense, useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal } from '@radish/ui/modal';
import { Button } from '@radish/ui/button';
import { Icon } from '@radish/ui/icon';
import type { UserMentionOption as UiUserMentionOption } from '@radish/ui';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { getAllTags, type Category, type PostDetail } from '@/api/forum';
import { searchUsersForMention } from '@/api/user';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import styles from './EditPostModal.module.css';

interface EditPostModalProps {
  isOpen: boolean;
  post: PostDetail | null;
  categories: Category[];
  onClose: () => void;
  onSave: (postId: number, title: string, content: string, categoryId: number, tagNames: string[]) => Promise<void>;
}

const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;
const MIN_TAG_COUNT = 1;
const MAX_TAG_COUNT = 5;

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

export const EditPostModal = ({ isOpen, post, categories, onClose, onSave }: EditPostModalProps) => {
  const { t } = useTranslation();
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();
  const roles = useUserStore(state => state.roles || []);
  const isAdmin = roles.some(role => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [imageScalePercent, setImageScalePercent] = useState<number>(75);
  const [saving, setSaving] = useState(false);
  const [allTagNames, setAllTagNames] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [tagError, setTagError] = useState<string | null>(null);
  const [categoryError, setCategoryError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleSearchUsers = useCallback(async (keyword: string): Promise<UiUserMentionOption[]> => {
    try {
      const users = await searchUsersForMention(keyword, t);
      return users.map((user) => ({
        id: user.voId,
        userName: user.voUserName,
        displayName: user.voDisplayName,
        avatar: user.voAvatar
      }));
    } catch (searchError) {
      log.error('EditPostModal', '搜索提及用户失败:', searchError);
      return [];
    }
  }, [t]);

  // 当 post 改变时更新表单
  useEffect(() => {
    if (post) {
      setTitle(post.voTitle);
      setContent(post.voContent);
      const initialTags = post.voTagNames && post.voTagNames.length > 0
        ? post.voTagNames
        : (post.voTags || '')
            .split(',')
            .map(tag => tag.trim())
            .filter(Boolean);

      setSelectedTags(initialTags);
      setCategoryId(post.voCategoryId ?? null);
      setTagInput('');
      setTagError(null);
      setCategoryError(null);
      setError(null);
    }
  }, [post]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const loadTags = async () => {
      try {
        const tags = await getAllTags(t);
        setAllTagNames(tags.map(tag => tag.voName));
      } catch (loadError) {
        log.warn('编辑帖子加载标签失败:', loadError);
        setAllTagNames([]);
      }
    };

    void loadTags();
  }, [isOpen, t]);

  const normalizeTagName = (name: string) => name.trim();

  const addTag = (rawTagName: string) => {
    const tagName = normalizeTagName(rawTagName);
    if (!tagName) {
      return;
    }

    if (selectedTags.some(tag => tag.toLowerCase() === tagName.toLowerCase())) {
      setTagInput('');
      return;
    }

    if (selectedTags.length >= MAX_TAG_COUNT) {
      setTagError(t('forum.editPost.tagMaxError', { max: MAX_TAG_COUNT }));
      return;
    }

    const exists = allTagNames.some(name => name.toLowerCase() === tagName.toLowerCase());
    if (!exists && !isAdmin) {
      setTagError(t('forum.editPost.tagCreateRestricted'));
      return;
    }

    setSelectedTags(prev => [...prev, tagName]);
    setTagInput('');
    setTagError(null);
    setCategoryError(null);
  };

  const removeTag = (tagName: string) => {
    setSelectedTags(prev => prev.filter(tag => tag !== tagName));
    setTagError(null);
    setCategoryError(null);
  };

  const matchedTags = tagInput.trim()
    ? allTagNames
        .filter(name =>
          name.toLowerCase().includes(tagInput.trim().toLowerCase()) &&
          !selectedTags.some(selected => selected.toLowerCase() === name.toLowerCase())
        )
        .slice(0, 8)
    : [];

  const handleSave = async () => {
    if (!post) return;

    if (!title.trim() || !content.trim()) {
      setError(t('forum.editPost.requiredFields'));
      return;
    }

    if (selectedTags.length < MIN_TAG_COUNT) {
      setTagError(t('forum.editPost.tagMinError', { min: MIN_TAG_COUNT }));
      return;
    }

    if (selectedTags.length > MAX_TAG_COUNT) {
      setTagError(t('forum.editPost.tagMaxError', { max: MAX_TAG_COUNT }));
      return;
    }

    if (!categoryId || categoryId <= 0) {
      setCategoryError(t('forum.editPost.categoryRequired'));
      return;
    }

    setSaving(true);
    setError(null);
    setCategoryError(null);
    try {
      await onSave(post.voId, title.trim(), content.trim(), categoryId, selectedTags);
      onClose();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    if (!saving) {
      setError(null);
      setCategoryError(null);
      onClose();
    }
  };

  const handleImageUpload = async (file: File): Promise<MarkdownImageUploadResult> => {
    try {
      const result = await uploadImage({
        file,
        businessType: 'Post',
        generateThumbnail: true,
        generateMultipleSizes,
        addWatermark,
        watermarkText,
        removeExif: true
      }, t);

      return {
        attachmentId: result.voId,
        displayVariant: 'original',
        previewUrl: buildAttachmentAssetUrl(result.voId, 'original'),
        scalePercent: imageScalePercent,
      };
    } catch (uploadError) {
      log.error('编辑帖子上传图片失败:', uploadError);
      throw uploadError;
    }
  };

  const handleDocumentUpload = async (file: File): Promise<MarkdownDocumentUploadResult> => {
    try {
      const result = await uploadDocument({
        file,
        businessType: 'Post'
      }, t);

      return {
        attachmentId: result.voId,
        fileName: result.voOriginalName || file.name
      };
    } catch (uploadError) {
      log.error('编辑帖子上传文档失败:', uploadError);
      throw uploadError;
    }
  };

  const editorToolbarExtras = (
    <div className={styles.editorToggles}>
      <button
        type="button"
        className={`${styles.editorToggle} ${addWatermark ? styles.editorToggleActive : ''}`}
        onClick={() => setAddWatermark(!addWatermark)}
        aria-pressed={addWatermark}
      >
        <Icon icon="mdi:watermark" size={16} />
        <span>{t('forum.editPost.watermark')}</span>
      </button>
      <button
        type="button"
        className={`${styles.editorToggle} ${generateMultipleSizes ? styles.editorToggleActive : ''}`}
        onClick={() => setGenerateMultipleSizes(!generateMultipleSizes)}
        aria-pressed={generateMultipleSizes}
      >
        <Icon icon="mdi:aspect-ratio" size={16} />
        <span>{t('forum.editPost.multiSize')}</span>
      </button>
      <label className={styles.editorScaleLabel}>
        <span>{t('forum.editPost.scale')}</span>
        <select
          value={imageScalePercent}
          onChange={(e) => setImageScalePercent(Number(e.target.value))}
          className={styles.editorScaleSelect}
        >
          {IMAGE_SCALE_OPTIONS.map(scale => (
            <option key={scale} value={scale}>{scale}%</option>
          ))}
        </select>
      </label>
    </div>
  );

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title={t('forum.editPost.title')}
      size="large"
      footer={
        <div className={styles.footer}>
          <Button variant="secondary" onClick={handleClose} disabled={saving}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>
            {saving ? t('forum.editPost.saving') : t('common.save')}
          </Button>
        </div>
      }
    >
      <div className={styles.container}>
        <div className={styles.lead}>
          <h3 className={styles.leadTitle}>{t('forum.editPost.title')}</h3>
          <p className={styles.leadHint}>{t('forum.editPost.leadHint')}</p>
        </div>

        {error && <div className={styles.error}>{error}</div>}

        <div className={styles.titleRow}>
          <input
            id="edit-title"
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            className={styles.titleInput}
            disabled={saving}
            placeholder={t('forum.editPost.titlePlaceholder')}
            maxLength={100}
          />
          <span className={styles.titleCount}>{title.length}/100</span>
        </div>

        <div className={styles.categorySection}>
          <div className={styles.categoryHeader}>
            <span className={styles.categoryLabel}>{t('forum.editPost.categoryLabel')}</span>
            <span className={styles.categoryHint}>{t('forum.editPost.categoryHint')}</span>
          </div>
          <select
            value={categoryId ?? ''}
            onChange={e => {
              const value = e.target.value;
              setCategoryId(value ? Number(value) : null);
              setCategoryError(null);
            }}
            className={styles.categorySelect}
            disabled={saving || categories.length === 0}
          >
            <option value="">{t('forum.editPost.categoryPlaceholder')}</option>
            {categories.map(category => (
              <option key={category.voId} value={category.voId}>
                {category.voName}
              </option>
            ))}
          </select>
          {categoryError && <p className={styles.categoryError}>{categoryError}</p>}
          {!categoryError && categories.length === 0 && (
            <p className={styles.categoryError}>{t('forum.editPost.categoryEmpty')}</p>
          )}
        </div>

        <div className={styles.tagSection}>
          <div className={styles.tagHeader}>
            <span className={styles.tagLabel}>{t('forum.editPost.tagLabel')}</span>
            <span className={styles.tagHint}>{t('forum.editPost.tagHint', { min: MIN_TAG_COUNT, max: MAX_TAG_COUNT })}</span>
          </div>
          <div className={styles.tagInputRow}>
            <input
              type="text"
              placeholder={t('forum.editPost.tagPlaceholder')}
              value={tagInput}
              onChange={e => {
                setTagInput(e.target.value);
                if (tagError) {
                  setTagError(null);
                }
              }}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ',') {
                  e.preventDefault();
                  addTag(tagInput);
                }
              }}
              className={styles.tagInput}
              maxLength={50}
              disabled={saving}
            />
            <button
              type="button"
              className={styles.addTagButton}
              onClick={() => addTag(tagInput)}
              disabled={!tagInput.trim() || saving}
            >
              {t('forum.editPost.tagAdd')}
            </button>
          </div>

          {matchedTags.length > 0 && (
            <div className={styles.suggestionWrap}>
              {matchedTags.map(tag => (
                <button
                  key={tag}
                  type="button"
                  className={styles.suggestionTag}
                  onClick={() => addTag(tag)}
                  disabled={saving}
                >
                  {tag}
                </button>
              ))}
            </div>
          )}

          {tagInput.trim() && matchedTags.length === 0 && (
            <p className={styles.tagTip}>
              {isAdmin ? t('forum.editPost.tagCreateHintAdmin') : t('forum.editPost.tagCreateHintMember')}
            </p>
          )}

          {selectedTags.length > 0 && (
            <div className={styles.selectedTagWrap}>
              {selectedTags.map(tag => (
                <button
                  key={tag}
                  type="button"
                  className={styles.selectedTag}
                  onClick={() => removeTag(tag)}
                  title={t('forum.editPost.tagRemove')}
                  disabled={saving}
                >
                  #{tag} ×
                </button>
              ))}
            </div>
          )}

          {tagError && <p className={styles.tagError}>{tagError}</p>}
        </div>

        <div className={styles.editorWrapper}>
          <Suspense fallback={<div className={styles.editorLoading}>{t('forum.editPost.editorLoading')}</div>}>
            <MarkdownEditor
              value={content}
              onChange={setContent}
              placeholder={t('forum.editPost.contentPlaceholder')}
              onImageUpload={handleImageUpload}
              onDocumentUpload={handleDocumentUpload}
              stickerGroups={stickerGroups}
              stickerMap={stickerMap}
              onStickerSelect={(selection) => {
                void handleStickerSelect(selection);
              }}
              minHeight={320}
              defaultMode="edit"
              className={styles.editor}
              theme="light"
              toolbarExtras={editorToolbarExtras}
              onUserMentionSearch={handleSearchUsers}
              disabled={saving}
            />
          </Suspense>
        </div>

        {addWatermark && (
          <div className={styles.watermarkRow}>
            <span className={styles.watermarkLabel}>{t('forum.editPost.watermarkLabel')}</span>
            <input
              type="text"
              placeholder={t('forum.editPost.watermarkPlaceholder')}
              value={watermarkText}
              onChange={(e) => setWatermarkText(e.target.value)}
              className={styles.watermarkInput}
              disabled={saving}
            />
          </div>
        )}
      </div>
    </Modal>
  );
};
