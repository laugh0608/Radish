import { useState } from 'react';
import type { CommentNode as CommentNodeType } from '@/types/forum';
import { Icon } from '@radish/ui';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
  currentUserId?: number;
  pageSize?: number; // 每次加载子评论数量
  onDelete?: (commentId: number) => void;
  onLike?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReply?: (commentId: number, authorName: string) => void;
  onLoadMoreChildren?: (parentId: number, pageIndex: number, pageSize: number) => Promise<CommentNodeType[]>;
}

/**
 * 将评论内容中的@用户名高亮显示
 */
const highlightMentions = (content: string): string => {
  // 转义HTML特殊字符以防止XSS攻击
  const escapeHtml = (text: string): string => {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  };

  const escapedContent = escapeHtml(content);

  // 替换@用户名为高亮样式
  return escapedContent.replace(/@([^\s@]+)/g, '<span class="mention">@$1</span>');
};

export const CommentNode = ({
  node,
  level,
  currentUserId = 0,
  pageSize = 10,
  onDelete,
  onLike,
  onReply,
  onLoadMoreChildren
}: CommentNodeProps) => {
  // 判断是否是作者本人
  const isAuthor = currentUserId > 0 && node.authorId === currentUserId;

  // 本地点赞状态（用于乐观更新）
  const [isLiked, setIsLiked] = useState(node.isLiked ?? false);
  const [likeCount, setLikeCount] = useState(node.likeCount ?? 0);
  const [isLiking, setIsLiking] = useState(false);

  // 子评论展开状态
  const [isExpanded, setIsExpanded] = useState(false);
  const [loadedChildren, setLoadedChildren] = useState<CommentNodeType[]>(node.children || []);
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  const hasChildren = (node.childrenTotal && node.childrenTotal > 0) || (node.children && node.children.length > 0);
  const totalChildren = node.childrenTotal ?? node.children?.length ?? 0;
  const loadedCount = loadedChildren.length;
  const hasMore = loadedCount < totalChildren;

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

  // 处理回复
  const handleReply = () => {
    if (onReply) {
      onReply(node.id, node.authorName);
    }
  };

  // 展开/收起子评论
  const handleToggleExpand = async () => {
    if (!isExpanded) {
      // 展开：如果还没有加载完整数据，则加载
      if (loadedCount === 0 && onLoadMoreChildren) {
        setIsLoadingMore(true);
        try {
          const children = await onLoadMoreChildren(node.id, 1, pageSize);
          setLoadedChildren(children);
          setCurrentPage(1);
        } catch (error) {
          console.error('加载子评论失败:', error);
        } finally {
          setIsLoadingMore(false);
        }
      }
      setIsExpanded(true);
    } else {
      // 收起：回到初始状态（只显示最热的1条）
      setIsExpanded(false);
    }
  };

  // 加载更多子评论
  const handleLoadMore = async () => {
    if (!onLoadMoreChildren || isLoadingMore) return;

    setIsLoadingMore(true);
    try {
      const nextPage = currentPage + 1;
      const moreChildren = await onLoadMoreChildren(node.id, nextPage, pageSize);
      setLoadedChildren([...loadedChildren, ...moreChildren]);
      setCurrentPage(nextPage);
    } catch (error) {
      console.error('加载更多子评论失败:', error);
    } finally {
      setIsLoadingMore(false);
    }
  };

  // 决定显示哪些子评论
  const displayChildren = level === 0 && !isExpanded && hasChildren
    ? loadedChildren.slice(0, 1) // 顶级评论未展开：只显示最热的1条
    : loadedChildren; // 已展开或非顶级评论：显示所有已加载的

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

      {/* 渲染内容（纯文本，支持@用户名高亮） */}
      <div
        className={styles.content}
        dangerouslySetInnerHTML={{ __html: highlightMentions(node.content) }}
      />

      {/* 操作按钮区域 */}
      <div className={styles.actions}>
        {/* 点赞按钮 */}
        {onLike && (
          <button
            type="button"
            onClick={handleLike}
            disabled={isLiking}
            className={`${styles.actionButton} ${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            title={isLiked ? '取消点赞' : '点赞'}
          >
            <Icon icon={isLiked ? 'mdi:heart' : 'mdi:heart-outline'} size={16} />
            {likeCount > 0 && <span className={styles.count}>{likeCount}</span>}
          </button>
        )}

        {/* 回复按钮 */}
        {onReply && (
          <button
            type="button"
            onClick={handleReply}
            className={`${styles.actionButton} ${styles.replyButton}`}
            title="回复"
          >
            <Icon icon="mdi:reply" size={16} />
            <span>回复</span>
          </button>
        )}
      </div>

      {/* 子评论区域 */}
      {hasChildren && (
        <div className={styles.childrenSection}>
          {/* 显示子评论 */}
          {displayChildren.length > 0 && (
            <div className={styles.children}>
              {displayChildren.map(child => (
                <CommentNode
                  key={child.id}
                  node={child}
                  level={level + 1}
                  currentUserId={currentUserId}
                  pageSize={pageSize}
                  onDelete={onDelete}
                  onLike={onLike}
                  onReply={onReply}
                  onLoadMoreChildren={onLoadMoreChildren}
                />
              ))}
            </div>
          )}

          {/* 展开/收起按钮（仅顶级评论显示） */}
          {level === 0 && totalChildren > 1 && (
            <div className={styles.expandSection}>
              {!isExpanded ? (
                <button
                  type="button"
                  onClick={handleToggleExpand}
                  disabled={isLoadingMore}
                  className={styles.expandButton}
                >
                  <Icon icon="mdi:chevron-down" size={16} />
                  {isLoadingMore ? '加载中...' : `展开 ${totalChildren - 1} 条回复`}
                </button>
              ) : (
                <>
                  {/* 加载更多按钮 */}
                  {hasMore && (
                    <button
                      type="button"
                      onClick={handleLoadMore}
                      disabled={isLoadingMore}
                      className={styles.expandButton}
                    >
                      <Icon icon="mdi:chevron-down" size={16} />
                      {isLoadingMore ? '加载中...' : `加载更多 (${loadedCount}/${totalChildren})`}
                    </button>
                  )}

                  {/* 收起按钮 */}
                  <button
                    type="button"
                    onClick={handleToggleExpand}
                    className={styles.collapseButton}
                  >
                    <Icon icon="mdi:chevron-up" size={16} />
                    收起回复
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
