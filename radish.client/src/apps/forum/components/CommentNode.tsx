import { useState } from 'react';
import type { CommentNode as CommentNodeType } from '@/types/forum';
import { MarkdownRenderer, Icon } from '@radish/ui';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
  currentUserId?: number;
  onDelete?: (commentId: number) => void;
  onLike?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
}

export const CommentNode = ({ node, level, currentUserId = 0, onDelete, onLike }: CommentNodeProps) => {
  // 判断是否是作者本人
  const isAuthor = currentUserId > 0 && node.authorId === currentUserId;

  // 本地点赞状态（用于乐观更新）
  const [isLiked, setIsLiked] = useState(node.isLiked ?? false);
  const [likeCount, setLikeCount] = useState(node.likeCount ?? 0);
  const [isLiking, setIsLiking] = useState(false);

  // 处理点赞
  const handleLike = async () => {
    if (!onLike || isLiking) return;

    setIsLiking(true);
    try {
      const result = await onLike(node.id);
      // 更新本地状态
      setIsLiked(result.isLiked);
      setLikeCount(result.likeCount);
    } catch (error) {
      console.error('点赞失败:', error);
      // 发生错误时保持原状态
    } finally {
      setIsLiking(false);
    }
  };

  return (
    <div className={styles.container} style={{ marginLeft: level * 16 }}>
      <div className={styles.header}>
        <span className={styles.author}>{node.authorName}</span>
        {node.createTime && <span className={styles.time}> · {node.createTime}</span>}
        {isAuthor && onDelete && (
          <button
            type="button"
            onClick={() => onDelete(node.id)}
            className={styles.deleteButton}
            title="删除评论"
          >
            <Icon icon="mdi:delete" size={14} />
          </button>
        )}
      </div>
      <MarkdownRenderer content={node.content} className={styles.content} />

      {/* 操作按钮区域 */}
      <div className={styles.actions}>
        {onLike && (
          <button
            type="button"
            onClick={handleLike}
            disabled={isLiking}
            className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            title={isLiked ? '取消点赞' : '点赞'}
          >
            <Icon
              icon={isLiked ? 'mdi:heart' : 'mdi:heart-outline'}
              size={16}
            />
            {likeCount > 0 && <span className={styles.likeCount}>{likeCount}</span>}
          </button>
        )}
      </div>

      {node.children && node.children.length > 0 && (
        <div className={styles.children}>
          {node.children.map(child => (
            <CommentNode
              key={child.id}
              node={child}
              level={level + 1}
              currentUserId={currentUserId}
              onDelete={onDelete}
              onLike={onLike}
            />
          ))}
        </div>
      )}
    </div>
  );
};
