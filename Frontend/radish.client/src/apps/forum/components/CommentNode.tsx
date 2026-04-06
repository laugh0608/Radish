import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { buildAttachmentAssetUrl, parseAttachmentMarkdownUrl, resolveConfiguredMediaUrl } from '@radish/ui';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import type { CommentNode as CommentNodeType, CommentReplyTarget, ReactionSummaryVo } from '@/api/forum';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { Icon } from '@radish/ui/icon';
import { ImageLightbox } from '@radish/ui/image-lightbox';
import { ReactionBar, type ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import styles from './CommentNode.module.css';

interface CommentNodeProps {
  node: CommentNodeType;
  level: number;
  displayTimeZone: string;
  currentUserId?: number;
  pageSize?: number; // 每次加载子评论数量
  isGodComment?: boolean; // 是否是神评
  onDelete?: (commentId: number) => void;
  onEdit?: (commentId: number, newContent: string) => Promise<void>;
  onViewHistory?: (commentId: number) => void;
  onLike?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReply?: (target: CommentReplyTarget) => void;
  onLoadMoreChildren?: (parentId: number, pageIndex: number, pageSize: number) => Promise<CommentNodeType[]>;
  stickerMap?: MarkdownStickerMap;
  reactionMap?: Record<number, ReactionSummaryVo[]>;
  isAuthenticated?: boolean;
  stickerGroups?: StickerPickerGroup[];
  onToggleReaction?: (commentId: number, payload: ReactionTogglePayload) => Promise<void>;
  isReactionPending?: (commentId: number) => boolean;
  onRequireReactionLogin?: () => void;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
  onReport?: (commentId: number) => void;
}

/**
 * 将评论内容中的@用户名高亮显示
 */
const escapeHtml = (text: string): string => {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
};

const highlightMentions = (escapedText: string): string => {
  return escapedText.replace(/@([^\s@]+)/g, '<span class="mention">@$1</span>');
};

interface CommentImageItem {
  displaySrc: string;
  fullSrc: string;
  alt: string;
}

interface ParsedStickerUri {
  groupCode: string;
  stickerCode: string;
  fallbackImageUrl?: string;
  fallbackThumbnailUrl?: string;
}

const normalizeStickerCode = (value: string): string => value.trim().toLowerCase();

const normalizeStickerKey = (groupCode: string, stickerCode: string): string =>
  `${normalizeStickerCode(groupCode)}/${normalizeStickerCode(stickerCode)}`;

const isSafeStickerUrl = (value?: string | null): value is string => {
  if (typeof value !== 'string') {
    return false;
  }

  const normalized = value.trim();
  if (!normalized) {
    return false;
  }

  // 允许站内相对路径（如 /uploads/...）和 http(s) 绝对路径
  return normalized.startsWith('/') || /^https?:\/\//i.test(normalized);
};

const parseStickerUri = (rawSrc: string): ParsedStickerUri | null => {
  const src = rawSrc.trim();
  if (!src.startsWith('sticker://')) {
    return null;
  }

  const withoutProtocol = src.slice('sticker://'.length);
  const [pathPart, hashPart] = withoutProtocol.split('#');
  const [groupCodeRaw, stickerCodeRaw] = pathPart.split('/');
  const groupCode = decodeURIComponent(groupCodeRaw || '').trim();
  const stickerCode = decodeURIComponent(stickerCodeRaw || '').trim();

  if (!groupCode || !stickerCode) {
    return null;
  }

  const parsed: ParsedStickerUri = {
    groupCode,
    stickerCode,
  };

  if (hashPart && hashPart.startsWith('radish:')) {
    const params = new URLSearchParams(hashPart.slice('radish:'.length));
    const image = params.get('image');
    const thumbnail = params.get('thumbnail');

    if (isSafeStickerUrl(image)) {
      parsed.fallbackImageUrl = image.trim();
    }
    if (isSafeStickerUrl(thumbnail)) {
      parsed.fallbackThumbnailUrl = thumbnail.trim();
    }
  }

  return parsed;
};

const parseImageMeta = (src: string) => {
  const attachmentMeta = parseAttachmentMarkdownUrl(src);
  if (attachmentMeta) {
    return {
      displaySrc: buildAttachmentAssetUrl(attachmentMeta.attachmentId, attachmentMeta.displayVariant),
      fullSrc: buildAttachmentAssetUrl(attachmentMeta.attachmentId, 'original'),
    };
  }

  const [baseSrc, hash] = src.split('#');
  const normalizedBaseSrc = resolveConfiguredMediaUrl(baseSrc || src);
  if (!hash || !hash.startsWith('radish:')) {
    return {
      displaySrc: normalizedBaseSrc,
      fullSrc: normalizedBaseSrc,
    };
  }

  const params = new URLSearchParams(hash.slice('radish:'.length));
  return {
    displaySrc: normalizedBaseSrc,
    fullSrc: resolveConfiguredMediaUrl(params.get('full') || baseSrc || src),
  };
};

const extractCommentImages = (content: string): CommentImageItem[] => {
  const regex = /!\[([^\]]*)\]\(([^)]+)\)/g;
  const images: CommentImageItem[] = [];
  let match: RegExpExecArray | null;

  while ((match = regex.exec(content)) !== null) {
    const alt = match[1] || 'comment-image';
    const src = match[2];
    if (!src) continue;
    if (parseStickerUri(src)) continue;
    const parsed = parseImageMeta(src);
    images.push({
      displaySrc: parsed.displaySrc,
      fullSrc: parsed.fullSrc,
      alt,
    });
  }

  return images;
};

const buildAvatarText = (name: string): string => {
  const source = name.trim();
  if (!source) {
    return '?';
  }

  return source.charAt(0).toUpperCase();
};

const buildAvatarStyle = (seed: string) => {
  let hash = 0;
  for (let i = 0; i < seed.length; i += 1) {
    hash = seed.charCodeAt(i) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 80% 92%)`,
    color: `hsl(${hue} 45% 30%)`
  };
};

const renderCommentHtml = (content: string, stickerMap?: MarkdownStickerMap): string => {
  const regex = /!\[([^\]]*)\]\(([^)]+)\)/g;
  let html = '';
  let cursor = 0;
  let match: RegExpExecArray | null;

  while ((match = regex.exec(content)) !== null) {
    const segment = content.slice(cursor, match.index);
    if (segment) {
      html += highlightMentions(escapeHtml(segment));
    }

    const alt = (match[1] || '').trim();
    const src = (match[2] || '').trim();
    const stickerMeta = parseStickerUri(src);
    if (stickerMeta) {
      const stickerKey = normalizeStickerKey(stickerMeta.groupCode, stickerMeta.stickerCode);
      const mapped = stickerMap?.[stickerKey];
      const resolvedSrc = mapped?.imageUrl || stickerMeta.fallbackImageUrl || stickerMeta.fallbackThumbnailUrl;
      const stickerTitle = alt || mapped?.name || `${stickerMeta.groupCode}/${stickerMeta.stickerCode}`;

      if (isSafeStickerUrl(resolvedSrc)) {
        const safeSrc = escapeHtml(resolveConfiguredMediaUrl(resolvedSrc));
        const safeTitle = escapeHtml(stickerTitle);
        html += `<img src="${safeSrc}" alt="${safeTitle}" title="${safeTitle}" class="stickerInline" loading="lazy" draggable="false" />`;
      } else {
        const fallbackText = `:${stickerMeta.groupCode}/${stickerMeta.stickerCode}:`;
        html += `<span class="stickerMissing">${escapeHtml(fallbackText)}</span>`;
      }
    }

    cursor = regex.lastIndex;
  }

  const tail = content.slice(cursor);
  if (tail) {
    html += highlightMentions(escapeHtml(tail));
  }

  return html.trim();
};

export const CommentNode = ({
  node,
  level,
  displayTimeZone,
  currentUserId = 0,
  pageSize = 10,
  isGodComment = false,
  onDelete,
  onEdit,
  onViewHistory,
  onLike,
  onReply,
  onLoadMoreChildren,
  stickerMap,
  reactionMap = {},
  isAuthenticated = false,
  stickerGroups = [],
  onToggleReaction,
  isReactionPending,
  onRequireReactionLogin,
  onAuthorClick,
  onReport,
}: CommentNodeProps) => {
  const { t } = useTranslation();
  // 判断是否是作者本人
  const isAuthor = currentUserId > 0 && String(node.voAuthorId) === String(currentUserId);

  // 编辑权限交给后端最终判定（时间窗口/次数限制由服务端配置控制）
  const canEdit = isAuthor;

  // 编辑状态
  const [isEditing, setIsEditing] = useState(false);
  const [editContent, setEditContent] = useState('');
  const [editError, setEditError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState(0);

  // 本地点赞状态（用于乐观更新）
  const [isLiked, setIsLiked] = useState(node.voIsLiked ?? false);
  const [likeCount, setLikeCount] = useState(node.voLikeCount ?? 0);
  const [isLiking, setIsLiking] = useState(false);

  // 子评论展开状态
  const [isExpanded, setIsExpanded] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [childSortBy, setChildSortBy] = useState<'newest' | 'hottest' | null>(null); // null表示默认排序(时间升序)
  const [hasPreloadedChildren, setHasPreloadedChildren] = useState(false);

  const commentImages = useMemo(() => extractCommentImages(node.voContent), [node.voContent]);
  const commentHtml = useMemo(() => renderCommentHtml(node.voContent, stickerMap), [node.voContent, stickerMap]);
  const replyToUserName = useMemo(() => node.voReplyToUserName?.trim() || '', [node.voReplyToUserName]);
  const authorAvatarUrl = useMemo(() => resolveMediaUrl(node.voAuthorAvatarUrl), [node.voAuthorAvatarUrl]);

  // 初始化已加载的子评论（默认时间升序）
  const [loadedChildren, setLoadedChildren] = useState<CommentNodeType[]>(() => {
    const children = Array.isArray(node.voChildren) ? node.voChildren : [];
    // 默认按时间升序排序
    return [...children].sort((a, b) =>
      new Date(a.voCreateTime || 0).getTime() - new Date(b.voCreateTime || 0).getTime()
    );
  });

  // 找出所有沙发（后端标记的）
  const sofaComments = loadedChildren.filter(c => c.voIsSofa);

  // 找出当前点赞数最高的沙发（用于置顶显示）
  const topSofaComment = sofaComments.length > 0
    ? [...sofaComments].sort((a, b) => {
        // 先按点赞数降序
        if ((b.voLikeCount || 0) !== (a.voLikeCount || 0)) {
          return (b.voLikeCount || 0) - (a.voLikeCount || 0);
        }
        // 点赞数相同时按时间降序（最新的在前）
        return new Date(b.voCreateTime || 0).getTime() - new Date(a.voCreateTime || 0).getTime();
      })[0]
    : null;

  const hasChildren = (node.voChildrenTotal && node.voChildrenTotal > 0) || (node.voChildren && node.voChildren.length > 0);
  const totalChildren = node.voChildrenTotal ?? node.voChildren?.length ?? 0;
  const loadedCount = loadedChildren.length;
  const hasMore = loadedCount < totalChildren;
  const reactionItems = reactionMap[node.voId] || [];
  const reactionLoading = isReactionPending ? isReactionPending(node.voId) : false;

  // 监听 node.voChildren 变化,重新初始化子评论列表
  useEffect(() => {
    const children = Array.isArray(node.voChildren) ? node.voChildren : [];
    const sorted = [...children].sort((a, b) =>
      new Date(a.voCreateTime || 0).getTime() - new Date(b.voCreateTime || 0).getTime()
    );
    setLoadedChildren(sorted);
    setChildSortBy(null); // 重置排序方式为默认值
  }, [node.voChildren]);

  // 父评论或子评论总数变化时，重置预加载状态
  useEffect(() => {
    setHasPreloadedChildren(false);
  }, [node.voId, node.voChildrenTotal]);

  // 若后端只返回 childrenTotal（不带 children 列表），为了“收起态也能看到一条回复”，这里自动预加载第一页子评论
  useEffect(() => {
    if (level !== 0) return;
    if (!hasChildren) return;
    if (loadedChildren.length > 0) return;
    if (!onLoadMoreChildren) return;
    if (isLoadingMore) return;
    if (hasPreloadedChildren) return;

    setHasPreloadedChildren(true);
    setIsLoadingMore(true);
    onLoadMoreChildren(node.voId, 1, pageSize)
      .then(children => {
        const normalized = Array.isArray(children) ? children : [];
        setLoadedChildren(normalized);
        setCurrentPage(1);
      })
      .catch(error => {
        log.error(t('forum.comment.preloadChildrenFailed'), error);
      })
      .finally(() => {
        setIsLoadingMore(false);
      });
  }, [hasChildren, isLoadingMore, level, loadedChildren.length, node.voId, onLoadMoreChildren, pageSize, t]);

  // 处理点赞
  const handleLike = async () => {
    if (!onLike || isLiking) return;

    setIsLiking(true);
    try {
      const result = await onLike(node.voId);
      // 更新本地状态（直接使用后端返回字段）
      setIsLiked(result.isLiked);
      setLikeCount(result.likeCount);
    } catch (error) {
      log.error(t('forum.comment.likeFailed'), error);
      // 发生错误时保持原状态
    } finally {
      setIsLiking(false);
    }
  };

  // 处理回复
  const handleReply = () => {
    if (onReply) {
      const parentCommentId = level === 0
        ? node.voId
        : node.voRootId ?? node.voParentId ?? node.voId;

      onReply({
        parentCommentId,
        targetCommentId: node.voId,
        authorName: node.voAuthorName
      });
    }
  };

  // 处理编辑
  const handleEdit = () => {
    setEditContent(node.voContent);
    setEditError(null);
    setIsEditing(true);
  };

  // 保存编辑
  const handleSaveEdit = async () => {
    if (!onEdit || !editContent.trim()) return;

    setIsSubmitting(true);
    try {
      await onEdit(node.voId, editContent.trim());
      setEditError(null);
      setIsEditing(false);
    } catch (error) {
      log.error(t('forum.comment.editFailed'), error);
      const message = error instanceof Error ? error.message : t('forum.comment.editFailedRetry');
      setEditError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  // 取消编辑
  const handleCancelEdit = () => {
    setIsEditing(false);
    setEditContent('');
    setEditError(null);
  };

  // 展开/收起子评论
  const handleToggleExpand = async () => {
    if (!isExpanded) {
      // 展开：如果还没有加载完整数据，则加载
      if (loadedCount === 0 && onLoadMoreChildren) {
        setIsLoadingMore(true);
        try {
          const children = await onLoadMoreChildren(node.voId, 1, pageSize);
          const normalized = Array.isArray(children) ? children : [];
          setLoadedChildren(normalized);
          setCurrentPage(1);
        } catch (error) {
          log.error(t('forum.comment.loadMoreChildrenFailed'), error);
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
      const moreChildren = await onLoadMoreChildren(node.voId, nextPage, pageSize);
      const normalized = Array.isArray(moreChildren) ? moreChildren : [];
      setLoadedChildren([...loadedChildren, ...normalized]);
      setCurrentPage(nextPage);
    } catch (error) {
      log.error(t('forum.comment.loadMoreChildrenFailed'), error);
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
        if (b.voLikeCount !== a.voLikeCount) {
          return (b.voLikeCount || 0) - (a.voLikeCount || 0);
        }
        // 点赞数相同时按时间降序
        return new Date(b.voCreateTime || 0).getTime() - new Date(a.voCreateTime || 0).getTime();
      } else {
        // 最新：按创建时间降序
        return new Date(b.voCreateTime || 0).getTime() - new Date(a.voCreateTime || 0).getTime();
      }
    });
    setLoadedChildren(sorted);
  };

  // 决定显示哪些子评论
  const displayChildren = (() => {
    if (level !== 0 || !hasChildren) {
      return loadedChildren;
    }

    // 收起状态下：优先展示“沙发”（如果有），否则展示当前已加载子评论里最热的一条
    const collapsedPreview = (() => {
      if (topSofaComment) return topSofaComment;
      if (loadedChildren.length === 0) return null;

      return [...loadedChildren].sort((a, b) => {
        const likeDiff = (b.voLikeCount || 0) - (a.voLikeCount || 0);
        if (likeDiff !== 0) return likeDiff;
        return new Date(b.voCreateTime || 0).getTime() - new Date(a.voCreateTime || 0).getTime();
      })[0];
    })();

    if (!isExpanded) {
      return collapsedPreview ? [collapsedPreview] : [];
    }

    if (childSortBy === null && topSofaComment) {
      // 展开且未手动排序：当前点赞数最高的沙发置顶 + 其他按时间升序
      const others = loadedChildren.filter(c => c.voId !== topSofaComment.voId);
      return [topSofaComment, ...others];
    }

    // 展开且手动排序：按排序结果显示
    return loadedChildren;
  })();

  return (
    <div className={styles.container} style={{ marginLeft: level * 16 }}>
      <div className={styles.header}>
        <button
          type="button"
          className={styles.authorButton}
          onClick={() => onAuthorClick?.(node.voAuthorId, node.voAuthorName, node.voAuthorAvatarUrl)}
          title={t('forum.comment.authorProfileTitle', { name: node.voAuthorName })}
        >
          <span
            className={styles.authorAvatar}
            style={authorAvatarUrl ? undefined : buildAvatarStyle(node.voAuthorName)}
            aria-hidden="true"
          >
            {authorAvatarUrl ? (
              <img
                className={styles.authorAvatarImage}
                src={authorAvatarUrl}
                alt={node.voAuthorName}
                loading="lazy"
              />
            ) : (
              buildAvatarText(node.voAuthorName)
            )}
          </span>
          <span className={styles.author}>{node.voAuthorName}</span>
        </button>
        {node.voCreateTime && (
          <span className={styles.time}> · {formatDateTimeByTimeZone(node.voCreateTime, displayTimeZone)}</span>
        )}
        {/* 神评标识（仅父评论） */}
        {level === 0 && isGodComment && (
          <span className={styles.godCommentBadge}>{t('forum.comment.godComment')}</span>
        )}
        {/* 沙发标识（仅子评论） */}
        {level === 1 && node.voIsSofa && (
          <span className={styles.sofaBadge}>{t('forum.comment.sofa')}</span>
        )}
        {isAuthor && (
          <div className={styles.authorActions}>
            {canEdit && onEdit && (
              <button
                type="button"
                onClick={handleEdit}
                className={styles.editButton}
                title={t('forum.comment.edit')}
                disabled={isEditing}
              >
                <Icon icon="mdi:pencil" size={14} />
              </button>
            )}
            {onViewHistory && (
              <button
                type="button"
                onClick={() => onViewHistory(node.voId)}
                className={styles.historyButton}
                title={t('forum.comment.viewHistory')}
                disabled={isEditing}
              >
                <Icon icon="mdi:history" size={14} />
              </button>
            )}
            {onDelete && (
              <button
                type="button"
                onClick={() => onDelete(node.voId)}
                className={styles.deleteButton}
                title={t('forum.comment.delete')}
              >
                <Icon icon="mdi:delete" size={14} />
              </button>
            )}
          </div>
        )}
      </div>

      {/* 渲染内容（支持 @ 高亮、图片和 sticker://） */}
      {isEditing ? (
        <div className={styles.editForm}>
          <textarea
            value={editContent}
            onChange={(e) => {
              setEditContent(e.target.value);
              if (editError) {
                setEditError(null);
              }
            }}
            className={styles.editTextarea}
            placeholder={t('forum.comment.editPlaceholder')}
            disabled={isSubmitting}
            autoFocus
          />
          <div className={styles.editActions}>
            <button
              type="button"
              onClick={handleSaveEdit}
              className={styles.saveButton}
              disabled={isSubmitting || !editContent.trim()}
            >
              {isSubmitting ? t('forum.comment.saving') : t('forum.comment.save')}
            </button>
            <button
              type="button"
              onClick={handleCancelEdit}
              className={styles.cancelButton}
              disabled={isSubmitting}
            >
              {t('forum.comment.cancel')}
            </button>
          </div>
          {editError && <div className={styles.editError}>{editError}</div>}
        </div>
      ) : (
        <div className={styles.contentWrapper}>
          {replyToUserName && (
            <div className={styles.replyMeta}>
              {t('forum.comment.replyPrefix')}
              <span className={styles.replyTarget}>@{replyToUserName}</span>
            </div>
          )}
          {commentHtml && (
            <div
              className={styles.content}
              dangerouslySetInnerHTML={{ __html: commentHtml }}
            />
          )}

          {commentImages.length > 0 && (
            <div className={styles.imageGrid}>
              {commentImages.map((img, idx) => (
                <button
                  type="button"
                  key={`${img.fullSrc}-${idx}`}
                  className={styles.imageButton}
                  onClick={() => {
                    setLightboxIndex(idx);
                    setLightboxOpen(true);
                  }}
                  title={t('forum.comment.viewOriginalImage')}
                >
                  <img src={img.displaySrc} alt={img.alt} className={styles.imageThumb} />
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 操作按钮区域 */}
      <div className={styles.actions}>
        {onToggleReaction && (
          <ReactionBar
            targetType="Comment"
            targetId={node.voId}
            items={reactionItems}
            isLoggedIn={isAuthenticated}
            loading={reactionLoading}
            stickerGroups={stickerGroups}
            onToggle={(payload) => onToggleReaction(node.voId, payload)}
            onRequireLogin={onRequireReactionLogin}
          />
        )}

        {/* 点赞按钮 */}
        {onLike && (
          <button
            type="button"
            onClick={handleLike}
            disabled={isLiking}
            className={`${styles.actionButton} ${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            title={isLiked ? t('forum.comment.unlike') : t('forum.comment.like')}
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
            title={t('forum.comment.reply')}
          >
            <Icon icon="mdi:reply" size={16} />
            <span>{t('forum.comment.reply')}</span>
          </button>
        )}

        {!!onReport && !isAuthor && (
          <button
            type="button"
            onClick={() => onReport(node.voId)}
            className={`${styles.actionButton} ${styles.reportButton}`}
            title={t('report.action')}
          >
            <Icon icon="mdi:flag-outline" size={16} />
            <span>{t('report.action')}</span>
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
                {t('forum.sort.newest')}
              </button>
              <button
                type="button"
                className={`${styles.childSortButton} ${childSortBy === 'hottest' ? styles.active : ''}`}
                onClick={() => handleChildSortChange('hottest')}
              >
                {t('forum.sort.hottest')}
              </button>
            </div>
          )}

          {/* 显示子评论 */}
          {displayChildren.length > 0 && (
            <div className={styles.children}>
              {displayChildren.map(child => (
                <CommentNode
                  key={child.voId}
                  node={child}
                  level={1}
                  displayTimeZone={displayTimeZone}
                  currentUserId={currentUserId}
                  pageSize={pageSize}
                  isGodComment={false} // 子评论不可能是神评，沙发标识通过 node.voIsSofa 判断
                  onDelete={onDelete}
                  onEdit={onEdit}
                  onLike={onLike}
                  onReply={onReply}
                  onLoadMoreChildren={undefined} // 2级结构，子评论不再加载更多
                  stickerMap={stickerMap}
                  reactionMap={reactionMap}
                  isAuthenticated={isAuthenticated}
                  stickerGroups={stickerGroups}
                  onToggleReaction={onToggleReaction}
                  isReactionPending={isReactionPending}
                  onRequireReactionLogin={onRequireReactionLogin}
                  onAuthorClick={onAuthorClick}
                  onReport={onReport}
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
                  {isLoadingMore ? t('forum.loadingPosts') : t('forum.comment.expandReplies', { count: totalChildren - 1 })}
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
                      {isLoadingMore ? t('forum.loadingPosts') : t('forum.comment.loadMoreReplies', { loaded: loadedCount, total: totalChildren })}
                    </button>
                  )}

                  {/* 收起按钮 */}
                  <button
                    type="button"
                    onClick={handleToggleExpand}
                    className={styles.collapseButton}
                  >
                    <Icon icon="mdi:chevron-up" size={16} />
                    {t('forum.comment.collapseReplies')}
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      )}

      <ImageLightbox
        isOpen={lightboxOpen}
        images={commentImages.map(item => ({ src: item.fullSrc, alt: item.alt }))}
        initialIndex={lightboxIndex}
        onClose={() => setLightboxOpen(false)}
      />
    </div>
  );
};
