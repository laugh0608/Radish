import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { MarkdownEditor } from '@radish/ui';
import { getOidcLoginUrl } from '@/api/forum';
import { uploadImage } from '@/api/attachment';
import styles from './PublishPostForm.module.css';

interface PublishPostFormProps {
  isAuthenticated: boolean;
  userName?: string;
  onPublish: (title: string, content: string) => void;
  disabled?: boolean;
}

const DRAFT_STORAGE_KEY = 'forum_post_draft';

export const PublishPostForm = ({
  isAuthenticated,
  userName,
  onPublish,
  disabled = false
}: PublishPostFormProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const { t } = useTranslation();

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
      console.error('Failed to load draft:', err);
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
        console.error('Failed to save draft:', err);
      }
    }
  }, [title, content]);

  const handleSubmit = () => {
    if (!title.trim() || !content.trim()) {
      return;
    }
    onPublish(title, content);
    // 发布成功后清空表单和草稿
    setTitle('');
    setContent('');
    try {
      localStorage.removeItem(DRAFT_STORAGE_KEY);
    } catch (err) {
      console.error('Failed to clear draft:', err);
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
        url: result.fileUrl,
        thumbnailUrl: result.thumbnailUrl
      };
    } catch (error) {
      console.error('图片上传失败:', error);
      throw error;
    }
  };

  return (
    <div className={styles.container}>
      <h4 className={styles.title}>发帖</h4>

      {!isAuthenticated && (
        <div className={styles.loginPrompt}>
          当前未登录，无法发帖。
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            去登录
          </button>
        </div>
      )}

      {isAuthenticated && (
        <div className={styles.userInfo}>
          当前用户：{userName || '已登录用户'}
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

      {/* 图片上传选项 */}
      {isAuthenticated && (
        <div className={styles.uploadOptions}>
          <label className={styles.optionLabel}>
            <input
              type="checkbox"
              checked={addWatermark}
              onChange={e => setAddWatermark(e.target.checked)}
              disabled={disabled}
            />
            <span>为上传的图片添加水印</span>
          </label>
          {addWatermark && (
            <input
              type="text"
              placeholder="水印文本"
              value={watermarkText}
              onChange={e => setWatermarkText(e.target.value)}
              className={styles.watermarkInput}
              disabled={disabled}
            />
          )}
          <label className={styles.optionLabel}>
            <input
              type="checkbox"
              checked={generateMultipleSizes}
              onChange={e => setGenerateMultipleSizes(e.target.checked)}
              disabled={disabled}
            />
            <span>生成多尺寸图片（Small/Medium/Large）</span>
          </label>
        </div>
      )}

      <MarkdownEditor
        value={content}
        onChange={setContent}
        placeholder="帖子内容（支持 Markdown）"
        minHeight={200}
        disabled={!isAuthenticated || disabled}
        showToolbar={true}
        onImageUpload={handleImageUpload}
      />

      <button
        type="button"
        onClick={handleSubmit}
        disabled={!isAuthenticated || disabled || !title.trim() || !content.trim()}
        className={styles.submitButton}
      >
        发布帖子
      </button>
    </div>
  );
};
