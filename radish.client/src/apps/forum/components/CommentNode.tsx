import { useState, useEffect } from 'react';
import type { CommentNode as CommentNodeType } from '@/types/forum';
import { Icon } from '@radish/ui';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
  currentUserId?: number;
  pageSize?: number; // 每次加载子评论数量
  isGodComment?: boolean; // 是否是神评
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
  isGodComment = false,
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
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [childSortBy, setChildSortBy] = useState<'newest' | 'hottest' | null>(null); // null表示默认排序(时间升序)

  // 初始化已加载的子评论（默认时间升序）
  const [loadedChildren, setLoadedChildren] = useState<CommentNodeType[]>(() => {
    const children = node.children || [];
    // 默认按时间升序排序
    return [...children].sort((a, b) =>
      new Date(a.createTime || 0).getTime() - new Date(b.createTime || 0).getTime()
    );
  });

  // 找出沙发（点赞数最多的子评论，如果点赞数相同则按时间最新）
  const sofaComment = node.children && node.children.length > 0
    ? [...node.children].sort((a, b) => {
        // 先按点赞数降序
        if ((b.likeCount || 0) !== (a.likeCount || 0)) {
          return (b.likeCount || 0) - (a.likeCount || 0);
        }
        // 点赞数相同时按时间降序（最新的在前）
        return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
      })[0]
    : null;

  const hasChildren = (node.childrenTotal && node.childrenTotal > 0) || (node.children && node.children.length > 0);
  const totalChildren = node.childrenTotal ?? node.children?.length ?? 0;
  const loadedCount = loadedChildren.length;
  const hasMore = loadedCount < totalChildren;

  // 监听 node.children 变化,重新初始化子评论列表
  useEffect(() => {
    const children = node.children || [];
    const sorted = [...children].sort((a, b) =>
      new Date(a.createTime || 0).getTime() - new Date(b.createTime || 0).getTime()
    );
    setLoadedChildren(sorted);
    setChildSortBy(null); // 重置排序方式为默认值
  }, [node.children]);

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

  // 处理子评论排序切换
  const handleChildSortChange = (newSortBy: 'newest' | 'hottest') => {
    setChildSortBy(newSortBy);
    // 立即对已加载的子评论重新排序
    const sorted = [...loadedChildren].sort((a, b) => {
      if (newSortBy === 'hottest') {
        // 最热：按点赞数降序
        if (b.likeCount !== a.likeCount) {
          return (b.likeCount || 0) - (a.likeCount || 0);
        }
        // 点赞数相同时按时间降序
        return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
      } else {
        // 最新：按创建时间降序
        return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
      }
    });
    setLoadedChildren(sorted);
  };

  // 决定显示哪些子评论
  const displayChildren = (() => {
    if (level !== 0 || !hasChildren) {
      return loadedChildren;
    }

    if (!isExpanded) {
      // 未展开：只显示沙发
      return sofaComment ? [sofaComment] : [];
    }

    if (childSortBy === null && sofaComment) {
      // 展开且未手动排序：沙发置顶 + 其他按时间升序
      const others = loadedChildren.filter(c => c.id !== sofaComment.id);
      return [sofaComment, ...others];
    }

    // 展开且手动排序：按排序结果显示
    return loadedChildren;
  })();

  return (
    <div className={styles.container} style={{ marginLeft: level * 16 }}>
      <div className={styles.header}>
        <span className={styles.author}>{node.authorName}</span>
        {node.createTime && <span className={styles.time}> · {node.createTime}</span>}
        {/* 沙发标识（仅子评论） */}
        {level === 1 && isGodComment && (
          <span className={styles.sofaBadge}>沙发</span>
        )}
        {/* 神评标识（仅父评论） */}
        {level === 0 && isGodComment && (
          <span className={styles.godCommentBadge}>神评</span>
        )}
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

      {/* 子评论区域（仅顶级评论显示，限制2级结构） */}
      {level === 0 && hasChildren && (
        <div className={styles.childrenSection}>
          {/* 子评论排序按钮（仅展开时显示） */}
          {isExpanded && totalChildren > 1 && (
            <div className={styles.childSortButtons}>
              <button
                type="button"
                className={`${styles.childSortButton} ${childSortBy === 'newest' ? styles.active : ''}`}
                onClick={() => handleChildSortChange('newest')}
              >
                最新
              </button>
              <button
                type="button"
                className={`${styles.childSortButton} ${childSortBy === 'hottest' ? styles.active : ''}`}
                onClick={() => handleChildSortChange('hottest')}
              >
                最热
              </button>
            </div>
          )}

          {/* 显示子评论 */}
          {displayChildren.length > 0 && (
            <div className={styles.children}>
              {displayChildren.map(child => (
                <CommentNode
                  key={child.id}
                  node={child}
                  level={1}
                  currentUserId={currentUserId}
                  pageSize={pageSize}
                  isGodComment={sofaComment !== null && child.id === sofaComment.id}
                  onDelete={onDelete}
                  onLike={onLike}
                  onReply={onReply}
                  onLoadMoreChildren={undefined} // 2级结构，子评论不再加载更多
                />
              ))}
            </div>
          )}

          {/* 展开/收起按钮 */}
          {totalChildren > 1 && (
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
