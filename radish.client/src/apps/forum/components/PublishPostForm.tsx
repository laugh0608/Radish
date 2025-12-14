import { useState } from 'react';
import { getOidcLoginUrl } from '@/api/forum';
import styles from './PublishPostForm.module.css';

interface PublishPostFormProps {
  isAuthenticated: boolean;
  userName?: string;
  onPublish: (title: string, content: string) => void;
  disabled?: boolean;
}

export const PublishPostForm = ({
  isAuthenticated,
  userName,
  onPublish,
  disabled = false
}: PublishPostFormProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');

  const handleSubmit = () => {
    if (!title.trim() || !content.trim()) {
      return;
    }
    onPublish(title, content);
    setTitle('');
    setContent('');
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

      <textarea
        placeholder="帖子内容（支持 Markdown）"
        value={content}
        onChange={e => setContent(e.target.value)}
        rows={4}
        className={styles.textarea}
        disabled={!isAuthenticated || disabled}
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
