import { lazy, Suspense, useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal } from '@radish/ui/modal';
import { Button } from '@radish/ui/button';
import { Icon } from '@radish/ui/icon';
import { log } from '@/utils/logger';
import { uploadDocument, uploadImage } from '@/api/attachment';
import type { PostDetail } from '@/api/forum';
import styles from './EditPostModal.module.css';

interface EditPostModalProps {
  isOpen: boolean;
  post: PostDetail | null;
  onClose: () => void;
  onSave: (postId: number, title: string, content: string) => Promise<void>;
}

const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;

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
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [imageScalePercent, setImageScalePercent] = useState<number>(75);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 当 post 改变时更新表单
  useEffect(() => {
    if (post) {
      setTitle(post.voTitle);
      setContent(post.voContent);
    }
  }, [post]);

  const handleSave = async () => {
    if (!post) return;

    if (!title.trim() || !content.trim()) {
      setError('标题和内容不能为空');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave(post.voId, title.trim(), content.trim());
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
