import type { CommentNode as CommentNodeType } from '@/types/forum';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
}

export const CommentNode = ({ node, level }: CommentNodeProps) => {
  return (
    <div className={styles.container} style={{ marginLeft: level * 16 }}>
      <div className={styles.header}>
        <span className={styles.author}>{node.authorName}</span>
        {node.createTime && <span className={styles.time}> · {node.createTime}</span>}
      </div>
      <div className={styles.content}>{node.content}</div>
      {node.children && node.children.length > 0 && (
        <div className={styles.children}>
          {node.children.map(child => (
            <CommentNode key={child.id} node={child} level={level + 1} />
          ))}
        </div>
      )}
    </div>
  );
};
