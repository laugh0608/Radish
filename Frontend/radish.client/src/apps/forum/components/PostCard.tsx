import type { PostItem } from '@/api/forum';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import styles from './PostCard.module.css';

interface PostCardProps {
  post: PostItem;
  displayTimeZone: string;
  onClick: () => void;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
  godComment?: {
    authorName: string;
    content?: string | null;
  } | null;
}

export const PostCard = ({ post, displayTimeZone, onClick, onAuthorClick, godComment }: PostCardProps) => {
  const allTags = post.voTags
    ? post.voTags
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
    : [];
  const tagList = allTags.slice(0, 2);
  const remainingTagCount = Math.max(allTags.length - tagList.length, 0);

  const authorName = post.voAuthorName?.trim() || '未知用户';
  const categoryName = post.voCategoryName?.trim() || '未分类';

  const buildAvatarText = (name: string) => {
    const source = name.trim();
    if (!source) return '?';
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

  const interactorSource =
    post.voLatestInteractors?.filter(
      (item): item is NonNullable<PostItem['voLatestInteractors']>[number] =>
        Boolean(item && item.voUserName?.trim())
    ) ?? [];

  const interactorItems =
    interactorSource.length > 0
      ? interactorSource
          .filter(item => item.voUserId !== post.voAuthorId && item.voUserName.trim() !== authorName)
          .slice(0, 3)
          .map(item => ({
            id: item.voUserId,
            name: item.voUserName.trim(),
            avatarUrl: item.voAvatarUrl?.trim() || null
          }))
      : [godComment?.authorName]
          .filter((name): name is string => Boolean(name && name.trim()))
          .map(name => ({
            id: 0,
            name: name.trim(),
            avatarUrl: null
          }))
          .filter(item => item.name !== authorName)
          .slice(0, 3);

  const fallbackInteractors =
    interactorItems.length === 0 && (post.voCommentCount ?? 0) > 0
      ? [{ id: -1, name: '评', avatarUrl: null }]
      : interactorItems;
  const displayedInteractorsCount = fallbackInteractors.length > 0 ? fallbackInteractors.length : 0;
  const remainingInteractions = Math.max((post.voCommentCount ?? 0) - displayedInteractorsCount, 0);
  const publishedTime = formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone, '未知时间');
  const godCommentPreview = godComment?.content?.trim() ?? '';
  const godCommentAuthor = godComment?.authorName?.trim() || '匿名用户';

  const renderAvatar = (
    name: string,
    avatarUrl: string | null | undefined,
    className: string,
    title?: string
  ) => {
    const normalizedUrl = avatarUrl?.trim();
    return (
      <span
        className={className}
        style={normalizedUrl ? undefined : buildAvatarStyle(name)}
        title={title ?? name}
      >
        {normalizedUrl ? (
          <img src={normalizedUrl} alt={name} className={styles.avatarImage} loading="lazy" />
        ) : (
          buildAvatarText(name)
        )}
      </span>
    );
  };

  return (
    <article className={styles.card} onClick={onClick}>
      <div className={styles.layout}>
        <div
          className={`${styles.main} ${!godCommentPreview ? styles.mainWithoutGodComment : ''}`}
        >
          {/* 帖子标题 */}
          <h3 className={styles.title}>{post.voTitle}</h3>

          <div className={styles.metaRow}>
            <span className={styles.categoryChip}>{categoryName}</span>
            {tagList.length > 0 ? (
              tagList.map(tag => (
                <span key={`${post.voId}-${tag}`} className={styles.tagChip}>
                  #{tag}
                </span>
              ))
            ) : (
              <span className={styles.emptyTag}>暂无标签</span>
            )}
            {remainingTagCount > 0 && <span className={styles.moreTag}>+{remainingTagCount}</span>}
            {post.voIsEssence && <span className={styles.statusChip}>精华</span>}
            {post.voIsTop && <span className={styles.statusChip}>置顶</span>}
          </div>

          {godCommentPreview ? (
            <div className={styles.godCommentCompact} title={`${godCommentAuthor}：${godCommentPreview}`}>
              <span className={styles.godCommentBadge}>神评</span>
              <span className={styles.godCommentText}>
                {godCommentAuthor}：{godCommentPreview}
              </span>
            </div>
          ) : null}
        </div>

        <aside className={styles.side}>
          <div className={styles.metaTopRow}>
            <div className={styles.authorBlock}>
              <button
                type="button"
                className={styles.authorLink}
                onClick={(event) => {
                  event.stopPropagation();
                  onAuthorClick?.(post.voAuthorId, post.voAuthorName, post.voAuthorAvatarUrl);
                }}
                title={`查看 ${authorName} 的主页`}
              >
                {renderAvatar(authorName, post.voAuthorAvatarUrl, styles.avatar, authorName)}
                <span className={styles.authorName}>{authorName}</span>
              </button>
            </div>
            <div className={styles.time}>{publishedTime}</div>
          </div>

          <div className={styles.statsRow}>
            <div className={styles.statItem}>
              <span className={styles.statLabel}>赞</span>
              <span className={styles.statValue}>{post.voLikeCount || 0}</span>
            </div>
            <div className={styles.statItem}>
              <span className={styles.statLabel}>评</span>
              <span className={styles.statValue}>{post.voCommentCount || 0}</span>
            </div>
            <div className={styles.statItem}>
              <span className={styles.statLabel}>阅</span>
              <span className={styles.statValue}>{post.voViewCount || 0}</span>
            </div>
          </div>

          <div className={styles.interactionRow}>
            <span className={styles.interactionLabel}>互动</span>
            <div className={styles.avatarGroup}>
              {fallbackInteractors.length > 0 ? (
                fallbackInteractors.map((item, index) => (
                  <span key={`${post.voId}-${item.id}-${item.name}-${index}`} className={styles.miniAvatarWrap}>
                    {renderAvatar(
                      item.name,
                      item.avatarUrl,
                      styles.miniAvatar,
                      item.name === '评' ? '最近有评论互动' : item.name
                    )}
                  </span>
                ))
              ) : (
                <span className={styles.noInteraction}>暂无</span>
              )}
              {remainingInteractions > 0 && (
                <span className={styles.moreCount}>+{remainingInteractions}</span>
              )}
            </div>
          </div>
        </aside>
      </div>
    </article>
  );
};
