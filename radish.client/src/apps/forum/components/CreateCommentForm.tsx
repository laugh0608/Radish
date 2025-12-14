import { useState } from 'react';
import { getOidcLoginUrl } from '@/api/forum';
import styles from './CreateCommentForm.module.css';

interface CreateCommentFormProps {
  isAuthenticated: boolean;
  hasPost: boolean;
  onSubmit: (content: string) => void;
  disabled?: boolean;
}

export const CreateCommentForm = ({
  isAuthenticated,
  hasPost,
  onSubmit,
  disabled = false
}: CreateCommentFormProps) => {
  const [content, setContent] = useState('');

  const handleSubmit = () => {
    if (!content.trim()) {
      return;
    }
    onSubmit(content);
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
      <h5 className={styles.title}>发表评论</h5>

      {!isAuthenticated && (
        <div className={styles.loginPrompt}>
          当前未登录，无法发表评论。
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            去登录
          </button>
        </div>
      )}

      <textarea
        placeholder="评论内容"
        value={content}
        onChange={e => setContent(e.target.value)}
        rows={3}
        className={styles.textarea}
        disabled={!isAuthenticated || !hasPost || disabled}
      />

      <button
        type="button"
        onClick={handleSubmit}
        disabled={!isAuthenticated || !hasPost || disabled || !content.trim()}
        className={styles.submitButton}
      >
        发表评论
      </button>
    </div>
  );
};
