import { lazy, Suspense, useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal } from '@radish/ui/modal';
import { Button } from '@radish/ui/button';
import { Icon } from '@radish/ui/icon';
import { getAllTags, type PostDetail } from '@/api/forum';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { uploadDocument, uploadImage } from '@/api/attachment';
import styles from './EditPostModal.module.css';

interface EditPostModalProps {
  isOpen: boolean;
  post: PostDetail | null;
  onClose: () => void;
  onSave: (postId: number, title: string, content: string, tagNames: string[]) => Promise<void>;
}

const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;
const MIN_TAG_COUNT = 1;
const MAX_TAG_COUNT = 5;

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

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

export const EditPostModal = ({ isOpen, post, onClose, onSave }: EditPostModalProps) => {
  const { t } = useTranslation();
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
  const [tagError, setTagError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

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
      setTagInput('');
      setTagError(null);
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
      setTagError(`最多可添加 ${MAX_TAG_COUNT} 个标签`);
      return;
    }

    const exists = allTagNames.some(name => name.toLowerCase() === tagName.toLowerCase());
    if (!exists && !isAdmin) {
      setTagError('标签不存在，暂时仅管理员可创建新标签');
      return;
    }

    setSelectedTags(prev => [...prev, tagName]);
    setTagInput('');
    setTagError(null);
  };

  const removeTag = (tagName: string) => {
    setSelectedTags(prev => prev.filter(tag => tag !== tagName));
    setTagError(null);
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
      setError('标题和内容不能为空');
      return;
    }

    if (selectedTags.length < MIN_TAG_COUNT) {
      setTagError(`请至少添加 ${MIN_TAG_COUNT} 个标签`);
      return;
    }

    if (selectedTags.length > MAX_TAG_COUNT) {
      setTagError(`最多可添加 ${MAX_TAG_COUNT} 个标签`);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave(post.voId, title.trim(), content.trim(), selectedTags);
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
      onClose();
    }
  };

  const handleImageUpload = async (file: File) => {
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
        url: appendImageMeta(result.voUrl, result.voUrl, imageScalePercent),
        thumbnailUrl: result.voThumbnailUrl
      };
    } catch (uploadError) {
      log.error('编辑帖子上传图片失败:', uploadError);
      throw uploadError;
    }
  };

  const handleDocumentUpload = async (file: File) => {
    try {
      const result = await uploadDocument({
        file,
        businessType: 'Post'
      }, t);

      return {
        url: result.voUrl,
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
        <span>水印</span>
      </button>
      <button
        type="button"
        className={`${styles.editorToggle} ${generateMultipleSizes ? styles.editorToggleActive : ''}`}
        onClick={() => setGenerateMultipleSizes(!generateMultipleSizes)}
        aria-pressed={generateMultipleSizes}
      >
        <Icon icon="mdi:aspect-ratio" size={16} />
        <span>多尺寸</span>
      </button>
      <label className={styles.editorScaleLabel}>
        <span>缩放</span>
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
      title="编辑帖子"
      size="large"
      footer={
        <div className={styles.footer}>
          <Button variant="secondary" onClick={handleClose} disabled={saving}>
            取消
          </Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>
            {saving ? '保存中...' : '保存'}
          </Button>
        </div>
      }
    >
      <div className={styles.container}>
        <div className={styles.lead}>
          <h3 className={styles.leadTitle}>编辑帖子</h3>
          <p className={styles.leadHint}>支持完整 Markdown 编辑、图片上传与文档附件</p>
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
            placeholder="请输入帖子标题"
            maxLength={100}
          />
          <span className={styles.titleCount}>{title.length}/100</span>
        </div>

        <div className={styles.tagSection}>
          <div className={styles.tagHeader}>
            <span className={styles.tagLabel}>帖子标签</span>
            <span className={styles.tagHint}>至少 {MIN_TAG_COUNT} 个，最多 {MAX_TAG_COUNT} 个</span>
          </div>
          <div className={styles.tagInputRow}>
            <input
              type="text"
              placeholder="输入标签名后按回车"
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
              添加
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
              {isAdmin ? '未匹配现有标签，保存时将创建新标签。' : '未匹配现有标签，暂时仅管理员可创建。'}
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
                  title="点击移除"
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
          <Suspense fallback={<div className={styles.editorLoading}>编辑器加载中...</div>}>
            <MarkdownEditor
              value={content}
              onChange={setContent}
              placeholder="帖子内容（支持 Markdown）"
              onImageUpload={handleImageUpload}
              onDocumentUpload={handleDocumentUpload}
              minHeight={320}
              className={styles.editor}
              theme="light"
              toolbarExtras={editorToolbarExtras}
              disabled={saving}
            />
          </Suspense>
        </div>

        {addWatermark && (
          <div className={styles.watermarkRow}>
            <span className={styles.watermarkLabel}>水印文字</span>
            <input
              type="text"
              placeholder="输入水印文字"
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
