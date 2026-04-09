import type { PostItem } from '@/api/forum';
import type { MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import styles from './PostCard.module.css';

interface PostCardProps {
  post: PostItem;
  displayTimeZone: string;
  onClick: () => void;
  variant?: 'default' | 'publicCompact';
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
  godComment?: {
    authorName: string;
    content?: string | null;
  } | null;
}

export const PostCard = ({
  post,
  displayTimeZone,
  onClick,
  variant = 'default',
  onAuthorClick,
  godComment
}: PostCardProps) => {
  const { t } = useTranslation();
  const isPublicCompact = variant === 'publicCompact';
  const allTags = post.voTags
    ? post.voTags
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
    : [];
  const tagList = allTags.slice(0, 2);
  const remainingTagCount = Math.max(allTags.length - tagList.length, 0);

  const authorName = post.voAuthorName?.trim() || t('forum.postCard.unknownUser');
  const categoryName = post.voCategoryName?.trim() || t('forum.postCard.uncategorized');

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
      ? [{ id: -1, name: t('forum.postCard.stat.comment'), avatarUrl: null }]
      : interactorItems;
  const displayedInteractorsCount = fallbackInteractors.length > 0 ? fallbackInteractors.length : 0;
  const remainingInteractions = Math.max((post.voCommentCount ?? 0) - displayedInteractorsCount, 0);
  const publishedTime = formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone, t('forum.postCard.unknownTime'));
  const godCommentPreview = godComment?.content?.trim() ?? '';
  const godCommentAuthor = godComment?.authorName?.trim() || t('forum.postCard.anonymousUser');

  const handleAuthorClick = (event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    onAuthorClick?.(post.voAuthorId, post.voAuthorName, post.voAuthorAvatarUrl);
  };

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

  const statsContent = (
    <div className={`${styles.statsRow} ${isPublicCompact ? styles.statsRowCompact : ''}`}>
      <div className={styles.statItem}>
        <span className={styles.statLabel}>{t('forum.postCard.stat.like')}</span>
        <span className={styles.statValue}>{post.voLikeCount || 0}</span>
      </div>
      <div className={styles.statItem}>
        <span className={styles.statLabel}>{t('forum.postCard.stat.comment')}</span>
        <span className={styles.statValue}>{post.voCommentCount || 0}</span>
      </div>
      <div className={styles.statItem}>
        <span className={styles.statLabel}>{t('forum.postCard.stat.view')}</span>
        <span className={styles.statValue}>{post.voViewCount || 0}</span>
      </div>
      {post.voHasPoll && (
        <div className={styles.statItem}>
          <span className={styles.statLabel}>{t('forum.postCard.stat.vote')}</span>
          <span className={styles.statValue}>{post.voPollTotalVoteCount || 0}</span>
        </div>
      )}
      {post.voIsQuestion && (
        <div className={styles.statItem}>
          <span className={styles.statLabel}>{t('forum.postCard.stat.answer')}</span>
          <span className={styles.statValue}>{post.voAnswerCount || 0}</span>
        </div>
      )}
      {post.voHasLottery && (
        <div className={styles.statItem}>
          <span className={styles.statLabel}>{t('forum.postCard.stat.lottery')}</span>
          <span className={styles.statValue}>{post.voLotteryParticipantCount || 0}</span>
        </div>
      )}
    </div>
  );

  const interactionContent = (
    <div className={`${styles.interactionRow} ${isPublicCompact ? styles.interactionRowCompact : ''}`}>
      <span className={styles.interactionLabel}>{t('forum.postCard.interaction')}</span>
      <div className={styles.avatarGroup}>
        {fallbackInteractors.length > 0 ? (
          fallbackInteractors.map((item, index) => (
            <span key={`${post.voId}-${item.id}-${item.name}-${index}`} className={styles.miniAvatarWrap}>
              {renderAvatar(
                item.name,
                item.avatarUrl,
                styles.miniAvatar,
                item.name === t('forum.postCard.stat.comment') ? t('forum.postCard.recentCommentInteraction') : item.name
              )}
            </span>
          ))
        ) : (
          <span className={styles.noInteraction}>{t('forum.postCard.noInteraction')}</span>
        )}
        {remainingInteractions > 0 && (
          <span className={styles.moreCount}>+{remainingInteractions}</span>
        )}
      </div>
    </div>
  );

  const authorContent = onAuthorClick ? (
    <button
      type="button"
      className={`${styles.authorLink} ${isPublicCompact ? styles.authorLinkCompact : ''}`}
      onClick={handleAuthorClick}
      title={t('forum.comment.authorProfileTitle', { name: authorName })}
    >
      {renderAvatar(authorName, post.voAuthorAvatarUrl, styles.avatar, authorName)}
      <span className={styles.authorName}>{authorName}</span>
    </button>
  ) : (
    <div className={`${styles.authorStatic} ${isPublicCompact ? styles.authorLinkCompact : ''}`}>
      {renderAvatar(authorName, post.voAuthorAvatarUrl, styles.avatar, authorName)}
      <span className={styles.authorName}>{authorName}</span>
    </div>
  );

  return (
    <article className={`${styles.card} ${isPublicCompact ? styles.cardPublicCompact : ''}`} onClick={onClick}>
      <div className={`${styles.layout} ${isPublicCompact ? styles.layoutPublicCompact : ''}`}>
        <div
          className={`${styles.main} ${!godCommentPreview ? styles.mainWithoutGodComment : ''} ${isPublicCompact ? styles.mainPublicCompact : ''}`}
        >
          {isPublicCompact && (
            <div className={styles.publicMetaTopRow}>
              <div className={styles.authorBlock}>
                {authorContent}
              </div>
              <div className={`${styles.time} ${styles.timeCompact}`}>{publishedTime}</div>
            </div>
          )}

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
              <span className={styles.emptyTag}>{t('forum.postCard.noTags')}</span>
            )}
            {remainingTagCount > 0 && <span className={styles.moreTag}>+{remainingTagCount}</span>}
            {post.voIsEssence && <span className={styles.statusChip}>{t('forum.postCard.essence')}</span>}
            {post.voIsTop && <span className={styles.statusChip}>{t('forum.postCard.top')}</span>}
            {post.voIsQuestion && (
              <>
                <span className={`${styles.statusChip} ${styles.questionChip}`}>{t('forum.postCard.question')}</span>
                <span className={`${styles.statusChip} ${post.voIsSolved ? styles.solvedChip : styles.pendingChip}`}>
                  {post.voIsSolved ? t('forum.filter.solved') : t('forum.filter.pending')}
                </span>
              </>
            )}
            {post.voHasPoll && (
              <span className={`${styles.statusChip} ${styles.pollChip}`}>{t('forum.postCard.poll')}</span>
            )}
            {post.voHasLottery && (
              <>
                <span className={`${styles.statusChip} ${styles.lotteryChip}`}>{t('forum.postCard.lottery')}</span>
                {post.voLotteryIsDrawn && (
                  <span className={`${styles.statusChip} ${styles.lotteryDoneChip}`}>{t('forum.postCard.lotteryDrawn')}</span>
                )}
              </>
            )}
          </div>

          {godCommentPreview ? (
            <div className={styles.godCommentCompact} title={`${godCommentAuthor}：${godCommentPreview}`}>
              <span className={styles.godCommentBadge}>{t('forum.comment.godComment')}</span>
              <span className={styles.godCommentText}>
                {godCommentAuthor}：{godCommentPreview}
              </span>
            </div>
          ) : null}

          {isPublicCompact && (
            <div className={styles.publicMetaBottom}>
              {statsContent}
              {interactionContent}
            </div>
          )}
        </div>

        <aside className={`${styles.side} ${isPublicCompact ? styles.sideHidden : ''}`}>
          <div className={styles.metaTopRow}>
            <div className={styles.authorBlock}>
              {authorContent}
            </div>
            <div className={styles.time}>{publishedTime}</div>
          </div>

          {statsContent}
          {interactionContent}
        </aside>
      </div>
    </article>
  );
};
