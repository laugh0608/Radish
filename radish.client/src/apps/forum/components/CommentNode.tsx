import type { CommentNode as CommentNodeType } from '@/types/forum';
import { MarkdownRenderer, Icon } from '@radish/ui';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
  currentUserId?: number;
  onDelete?: (commentId: number) => void;
}

export const CommentNode = ({ node, level, currentUserId = 0, onDelete }: CommentNodeProps) => {
  // 判断是否是作者本人
  const isAuthor = currentUserId > 0 && node.authorId === currentUserId;

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
      {node.children && node.children.length > 0 && (
        <div className={styles.children}>
          {node.children.map(child => (
            <CommentNode
              key={child.id}
              node={child}
              level={level + 1}
              currentUserId={currentUserId}
              onDelete={onDelete}
            />
          ))}
        </div>
      )}
    </div>
  );
};
