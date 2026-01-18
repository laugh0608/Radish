import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { Modal, MarkdownEditor } from '@radish/ui';
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

  if (!isAuthenticated) {
    return (
      <Modal isOpen={isOpen} onClose={onClose} title="发布新帖">
        <div className={styles.loginPrompt}>
          <p>请先登录后再发帖</p>
          <button onClick={handleLoginClick} className={styles.loginButton}>
            前往登录
          </button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="发布新帖" size="large">
      <div className={styles.container}>
        <div className={styles.form}>
          {/* 标题输入 */}
          <input
            type="text"
            placeholder="帖子标题"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className={styles.titleInput}
            maxLength={100}
          />

          {/* 图片上传选项 */}
          <div className={styles.options}>
            <label className={styles.checkboxLabel}>
              <input
                type="checkbox"
                checked={addWatermark}
                onChange={(e) => setAddWatermark(e.target.checked)}
              />
              为上传的图片添加水印
            </label>
            {addWatermark && (
              <input
                type="text"
                placeholder="水印文字"
                value={watermarkText}
                onChange={(e) => setWatermarkText(e.target.value)}
                className={styles.watermarkInput}
              />
            )}
            <label className={styles.checkboxLabel}>
              <input
                type="checkbox"
                checked={generateMultipleSizes}
                onChange={(e) => setGenerateMultipleSizes(e.target.checked)}
              />
              生成多尺寸图片（Small/Medium/Large）
            </label>
          </div>

          {/* Markdown 编辑器 */}
          <div className={styles.editorWrapper}>
            <MarkdownEditor
              value={content}
              onChange={setContent}
              placeholder="帖子内容（支持 Markdown）"
              onImageUpload={handleImageUpload}
              onDocumentUpload={handleDocumentUpload}
            />
          </div>

          {/* 提交按钮 */}
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
        </div>
      </div>
    </Modal>
  );
};
