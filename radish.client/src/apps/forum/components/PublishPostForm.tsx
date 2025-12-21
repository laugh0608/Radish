import { useState, useEffect } from 'react';
import { MarkdownEditor } from '@radish/ui';
import { getOidcLoginUrl } from '@/api/forum';
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

      <MarkdownEditor
        value={content}
        onChange={setContent}
        placeholder="帖子内容（支持 Markdown）"
        minHeight={200}
        disabled={!isAuthenticated || disabled}
        showToolbar={true}
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
