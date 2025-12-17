import { useState, useEffect, useRef } from 'react';
import styles from './UserMention.module.css';

export interface UserMentionOption {
  id: number | string;  // 支持后端long类型序列化为字符串
  userName: string;
  displayName?: string | null;
  avatar?: string | null;
}

export interface UserMentionProps {
  /** 搜索关键词 */
  keyword: string;
  /** 搜索函数（返回用户列表） */
  onSearch: (keyword: string) => Promise<UserMentionOption[]>;
  /** 选择用户的回调 */
  onSelect: (user: UserMentionOption) => void;
  /** 关闭下拉框的回调 */
  onClose: () => void;
  /** 下拉框位置 */
  position: { top: number; left: number };
}

export const UserMention = ({
  keyword,
  onSearch,
  onSelect,
  onClose,
  position,
}: UserMentionProps) => {
  const [users, setUsers] = useState<UserMentionOption[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [loading, setLoading] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  console.log('UserMention渲染:', { keyword, position, users: users.length });

  // 搜索用户
  useEffect(() => {
    let cancelled = false;

    const searchUsers = async () => {
      if (!keyword.trim()) {
        setUsers([]);
        return;
      }

      setLoading(true);
      try {
        const results = await onSearch(keyword);
        if (!cancelled) {
          setUsers(results);
          setSelectedIndex(0);
        }
      } catch (error) {
        console.error('搜索用户失败:', error);
        if (!cancelled) {
          setUsers([]);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    // Debounce 300ms
    const timer = setTimeout(searchUsers, 300);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [keyword, onSearch]);

  // 键盘事件处理
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (users.length === 0) return;

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          setSelectedIndex((prev) => (prev + 1) % users.length);
          break;
        case 'ArrowUp':
          e.preventDefault();
          setSelectedIndex((prev) => (prev - 1 + users.length) % users.length);
          break;
        case 'Enter':
          e.preventDefault();
          if (users[selectedIndex]) {
            onSelect(users[selectedIndex]);
          }
          break;
        case 'Escape':
          e.preventDefault();
          onClose();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [users, selectedIndex, onSelect, onClose]);

  // 点击外部关闭
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [onClose]);

  if (loading) {
    return (
      <div
        ref={containerRef}
        className={styles.container}
        style={{ top: position.top, left: position.left }}
      >
        <div className={styles.loading}>搜索中...</div>
      </div>
    );
  }

  if (users.length === 0) {
    return (
      <div
        ref={containerRef}
        className={styles.container}
        style={{ top: position.top, left: position.left }}
      >
        <div className={styles.empty}>未找到匹配的用户</div>
      </div>
    );
  }

  return (
    <div
      ref={containerRef}
      className={styles.container}
      style={{ top: position.top, left: position.left }}
    >
      <ul className={styles.list}>
        {users.map((user, index) => (
          <li
            key={user.id}
            className={`${styles.item} ${index === selectedIndex ? styles.selected : ''}`}
            onClick={() => onSelect(user)}
            onMouseEnter={() => setSelectedIndex(index)}
          >
            {user.avatar && (
              <img src={user.avatar} alt={user.userName} className={styles.avatar} />
            )}
            <div className={styles.info}>
              <div className={styles.userName}>@{user.userName}</div>
              {user.displayName && (
                <div className={styles.displayName}>{user.displayName}</div>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
};
