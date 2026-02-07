import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { BottomSheet, Icon, MarkdownEditor } from '@radish/ui';
import { getAllTags, getOidcLoginUrl } from '@/api/forum';
import { useUserStore } from '@/stores/userStore';
import { uploadImage, uploadDocument } from '@/api/attachment';
import styles from './PublishPostModal.module.css';

interface PublishPostModalProps {
  isOpen: boolean;
  isAuthenticated: boolean;
  onClose: () => void;
  onPublish: (title: string, content: string, tagNames: string[]) => void;
}

const DRAFT_STORAGE_KEY = 'forum_post_draft';
const MIN_TAG_COUNT = 1;
const MAX_TAG_COUNT = 5;

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
  const [allTagNames, setAllTagNames] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [tagError, setTagError] = useState<string | null>(null);
  const roles = useUserStore(state => state.roles || []);
  const isAdmin = roles.includes('Admin') || roles.includes('System');
  const { t } = useTranslation();

  // 组件打开时恢复草稿
  useEffect(() => {
    if (isOpen) {
      try {
        const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
        if (savedDraft) {
          const draft = JSON.parse(savedDraft);
          if (draft.title || draft.content || draft.tags?.length) {
            setTitle(draft.title || '');
            setContent(draft.content || '');
            setSelectedTags(Array.isArray(draft.tags) ? draft.tags : []);
          }
        }
      } catch (err) {
        log.error('Failed to load draft:', err);
      }
    }
  }, [isOpen]);

  // 自动保存草稿
  useEffect(() => {
    if (isOpen && (title || content || selectedTags.length > 0)) {
      try {
        localStorage.setItem(
          DRAFT_STORAGE_KEY,
          JSON.stringify({ title, content, tags: selectedTags, savedAt: Date.now() })
        );
      } catch (err) {
        log.error('Failed to save draft:', err);
      }
    }
  }, [title, content, selectedTags, isOpen]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const loadTags = async () => {
      try {
        const tags = await getAllTags(t);
        setAllTagNames(tags.map(tag => tag.voName));
      } catch (err) {
        log.warn('加载标签列表失败:', err);
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

  const handleSubmit = async () => {
    if (!title.trim() || !content.trim()) {
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

    setIsSubmitting(true);
    try {
      await onPublish(title, content, selectedTags);
      // 发布成功后清空表单和草稿
      setTitle('');
      setContent('');
      setSelectedTags([]);
      setTagInput('');
      setTagError(null);
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
        disabled={!title.trim() || !content.trim() || selectedTags.length < MIN_TAG_COUNT || isSubmitting}
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
            />
            <button
              type="button"
              className={styles.addTagButton}
              onClick={() => addTag(tagInput)}
              disabled={!tagInput.trim()}
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
                >
                  {tag}
                </button>
              ))}
            </div>
          )}

          {tagInput.trim() && matchedTags.length === 0 && (
            <p className={styles.tagTip}>
              {isAdmin ? '未匹配现有标签，发布时将创建新标签。' : '未匹配现有标签，暂时仅管理员可创建。'}
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
                >
                  #{tag} ×
                </button>
              ))}
            </div>
          )}

          {tagError && <p className={styles.tagError}>{tagError}</p>}
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
