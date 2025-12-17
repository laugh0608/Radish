import { useState, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { getOidcLoginUrl } from '@/api/forum';
import { searchUsersForMention } from '@/api/user';
import { UserMention, type UserMentionOption } from '@radish/ui';
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
  const { t } = useTranslation();
  const [content, setContent] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // @提及功能状态
  const [showMention, setShowMention] = useState(false);
  const [mentionKeyword, setMentionKeyword] = useState('');
  const [mentionPosition, setMentionPosition] = useState({ top: 0, left: 0 });
  const [mentionStartPos, setMentionStartPos] = useState(0);

  const handleSubmit = () => {
    if (!content.trim()) {
      return;
    }
    onSubmit(content);
    setContent('');
    setShowMention(false);
  };

  const handleLoginClick = () => {
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  // 检测@符号并触发用户搜索
  const handleTextChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newContent = e.target.value;
    const cursorPos = e.target.selectionStart;

    setContent(newContent);

    // 查找光标前最近的@符号
    const textBeforeCursor = newContent.substring(0, cursorPos);
    const lastAtIndex = textBeforeCursor.lastIndexOf('@');

    // 如果找到@符号，并且@符号后面没有空格
    if (lastAtIndex !== -1) {
      const textAfterAt = textBeforeCursor.substring(lastAtIndex + 1);

      // @符号后面不能有空格或换行
      if (!/[\s\n]/.test(textAfterAt)) {
        setMentionKeyword(textAfterAt);
        setMentionStartPos(lastAtIndex);
        setShowMention(true);

        // 计算下拉框位置
        if (textareaRef.current) {
          const rect = textareaRef.current.getBoundingClientRect();
          setMentionPosition({
            top: rect.bottom,
            left: rect.left
          });
        }
        return;
      }
    }

    // 没有匹配到@，关闭下拉框
    setShowMention(false);
  };

  // 搜索用户
  const handleSearchUsers = useCallback(async (keyword: string): Promise<UserMentionOption[]> => {
    try {
      const users = await searchUsersForMention(keyword, t);
      return users;
    } catch (error) {
      console.error('搜索用户失败:', error);
      return [];
    }
  }, [t]);

  // 选择用户
  const handleSelectUser = (user: UserMentionOption) => {
    if (!textareaRef.current) return;

    // 替换@和关键词为@用户名
    const beforeMention = content.substring(0, mentionStartPos);
    const afterMention = content.substring(textareaRef.current.selectionStart);
    const newContent = `${beforeMention}@${user.userName} ${afterMention}`;

    setContent(newContent);
    setShowMention(false);

    // 设置光标位置到@用户名后面
    setTimeout(() => {
      if (textareaRef.current) {
        const cursorPos = mentionStartPos + user.userName.length + 2; // @ + 用户名 + 空格
        textareaRef.current.selectionStart = cursorPos;
        textareaRef.current.selectionEnd = cursorPos;
        textareaRef.current.focus();
      }
    }, 0);
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

      <div className={styles.textareaWrapper}>
        <textarea
          ref={textareaRef}
          placeholder="评论内容（输入 @ 可以提及用户）"
          value={content}
          onChange={handleTextChange}
          rows={3}
          className={styles.textarea}
          disabled={!isAuthenticated || !hasPost || disabled}
        />

        {showMention && (
          <UserMention
            keyword={mentionKeyword}
            onSearch={handleSearchUsers}
            onSelect={handleSelectUser}
            onClose={() => setShowMention(false)}
            position={mentionPosition}
          />
        )}
      </div>

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
