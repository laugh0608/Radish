import { useState, useEffect } from 'react';
import { Modal, Button } from '@radish/ui';
import type { PostDetail } from '@/types/forum';
import styles from './EditPostModal.module.css';

interface EditPostModalProps {
  isOpen: boolean;
  post: PostDetail | null;
  onClose: () => void;
  onSave: (postId: number, title: string, content: string) => Promise<void>;
}

export const EditPostModal = ({ isOpen, post, onClose, onSave }: EditPostModalProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 当 post 改变时更新表单
  useEffect(() => {
    if (post) {
      setTitle(post.title);
      setContent(post.content);
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
      await onSave(post.id, title, content);
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
        {error && <div className={styles.error}>{error}</div>}
        <div className={styles.formGroup}>
          <label htmlFor="edit-title" className={styles.label}>
            标题
          </label>
          <input
            id="edit-title"
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            className={styles.input}
            disabled={saving}
            placeholder="请输入帖子标题"
          />
        </div>
        <div className={styles.formGroup}>
          <label htmlFor="edit-content" className={styles.label}>
            内容（支持 Markdown）
          </label>
          <textarea
            id="edit-content"
            value={content}
            onChange={e => setContent(e.target.value)}
            className={styles.textarea}
            disabled={saving}
            placeholder="请输入帖子内容"
            rows={15}
          />
        </div>
      </div>
    </Modal>
  );
};
