import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { BottomSheet, Icon, MarkdownEditor } from '@radish/ui';
import { getOidcLoginUrl } from '@/api/forum';
import { uploadImage, uploadDocument } from '@/api/attachment';
import styles from './PublishPostModal.module.css';

interface PublishPostModalProps {
  isOpen: boolean;
  isAuthenticated: boolean;
  onClose: () => void;
  onPublish: (title: string, content: string) => void;
}

const DRAFT_STORAGE_KEY = 'forum_post_draft';

export const PublishPostModal = ({
  isOpen,
  isAuthenticated,
  onClose,
  onPublish
}: PublishPostModalProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const { t } = useTranslation();

  // 组件打开时恢复草稿
  useEffect(() => {
    if (isOpen) {
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
    }
  }, [isOpen]);

  // 自动保存草稿
  useEffect(() => {
    if (isOpen && (title || content)) {
      try {
        localStorage.setItem(
          DRAFT_STORAGE_KEY,
          JSON.stringify({ title, content, savedAt: Date.now() })
        );
      } catch (err) {
        log.error('Failed to save draft:', err);
      }
    }
  }, [title, content, isOpen]);

  const handleSubmit = async () => {
    if (!title.trim() || !content.trim()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onPublish(title, content);
      // 发布成功后清空表单和草稿
      setTitle('');
      setContent('');
      localStorage.removeItem(DRAFT_STORAGE_KEY);
      onClose();
    } catch (err) {
      log.error('发布失败:', err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLoginClick = () => {
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  // 处理图片上传
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
        url: result.url,
        thumbnailUrl: result.thumbnailUrl
      };
    } catch (error) {
      log.error('图片上传失败:', error);
      throw error;
    }
  };

  // 处理文档上传
  const handleDocumentUpload = async (file: File) => {
    try {
      const result = await uploadDocument({
        file,
        businessType: 'Post'
      }, t);

      return {
        url: result.url,
        fileName: file.name
      };
    } catch (error) {
      log.error('文档上传失败:', error);
      throw error;
    }
  };

  const footer = (
    <div className={styles.footer}>
      <button
        onClick={handleSubmit}
        disabled={!title.trim() || !content.trim() || isSubmitting}
        className={styles.publishButton}
      >
        {isSubmitting ? '发布中...' : '发布帖子'}
      </button>
      <button onClick={onClose} className={styles.cancelButton}>
        取消
      </button>
    </div>
  );

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
    </div>
  );

  if (!isAuthenticated) {
    return (
      <BottomSheet isOpen={isOpen} onClose={onClose} title="发布新帖" height="70%">
        <div className={styles.loginPrompt}>
          <p>请先登录后再发帖</p>
          <button onClick={handleLoginClick} className={styles.loginButton}>
            前往登录
          </button>
        </div>
      </BottomSheet>
    );
  }

  return (
    <BottomSheet isOpen={isOpen} onClose={onClose} title="发布新帖" height="70%" footer={footer}>
      <div className={styles.container}>
        <div className={styles.lead}>
          <h3 className={styles.leadTitle}>写下你的新发现</h3>
          <p className={styles.leadHint}>清晰的标题与排版会获得更多互动</p>
        </div>

        <div className={styles.titleRow}>
          <input
            type="text"
            placeholder="帖子标题"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className={styles.titleInput}
            maxLength={100}
          />
          <span className={styles.titleCount}>{title.length}/100</span>
        </div>

        <div className={styles.editorWrapper}>
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
          />
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
            />
          </div>
        )}
      </div>
    </BottomSheet>
  );
};
