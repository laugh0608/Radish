import { lazy, Suspense, useEffect, useState } from 'react';
import type { PostDetail as PostDetailType, ReactionSummaryVo } from '@/api/forum';
import type { UserFollowStatus } from '@/api/userFollow';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import { ReactionBar, type ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import styles from './PostDetail.module.css';

const MarkdownRenderer = lazy(() =>
  import('@radish/ui/markdown-renderer').then((module) => ({ default: module.MarkdownRenderer }))
);

interface PostDetailProps {
  post: PostDetailType | null;
  loading?: boolean;
  displayTimeZone: string;
  isLiked?: boolean;
  onLike?: (postId: number) => void;
  onVotePoll?: (optionId: number) => Promise<void>;
  isAuthenticated?: boolean;
  currentUserId?: number;
  onEdit?: (postId: number) => void;
  onDelete?: (postId: number) => void;
  onViewHistory?: (postId: number) => void;
  postReactions?: ReactionSummaryVo[];
  reactionLoading?: boolean;
  stickerGroups?: StickerPickerGroup[];
  stickerMap?: MarkdownStickerMap;
  onToggleReaction?: (payload: ReactionTogglePayload) => Promise<void>;
  onRequireReactionLogin?: () => void;
  followStatus?: UserFollowStatus | null;
  followLoading?: boolean;
  onToggleFollow?: (targetUserId: number, isFollowing: boolean) => Promise<void>;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
}

export const PostDetail = ({
  post,
  loading = false,
  displayTimeZone,
  isLiked = false,
  onLike,
  onVotePoll,
  isAuthenticated = false,
  currentUserId = 0,
  onEdit,
  onDelete,
  onViewHistory,
  postReactions = [],
  reactionLoading = false,
  stickerGroups = [],
  stickerMap,
  onToggleReaction,
  onRequireReactionLogin,
  followStatus = null,
  followLoading = false,
  onToggleFollow,
  onAuthorClick,
}: PostDetailProps) => {
  const parsedTags = post?.voTags
    ? post.voTags
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
    : [];
  const tagList = post?.voTagNames && post.voTagNames.length > 0 ? post.voTagNames : parsedTags;
  const [selectedOptionId, setSelectedOptionId] = useState<number | null>(post?.voPoll?.voSelectedOptionId ?? null);
  const [isVoting, setIsVoting] = useState(false);

  const isAuthor = !!post && currentUserId > 0 && String(post.voAuthorId) === String(currentUserId);
  const showFollowAction = isAuthenticated && !!post && !isAuthor && post.voAuthorId > 0;

  useEffect(() => {
    setSelectedOptionId(post?.voPoll?.voSelectedOptionId ?? null);
  }, [post?.voId, post?.voPoll?.voSelectedOptionId]);

  if (loading) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.loadingText}>加载帖子详情中...</p>
      </div>
    );
  }

  if (!post) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.emptyText}>请选择一个帖子查看详情</p>
      </div>
    );
  }

  const poll = post.voPoll;
  const canSubmitPoll = !!poll && !poll.voIsClosed && !poll.voHasVoted && isAuthenticated;
  const pollStatusText = !poll
    ? ''
    : poll.voIsClosed
      ? '投票已结束'
      : poll.voHasVoted
        ? '你已投票，可查看实时结果'
        : isAuthenticated
          ? '选择一个选项后即可提交'
          : '登录后可参与投票';

  const handleVoteSubmit = async () => {
    if (!canSubmitPoll || !selectedOptionId || !onVotePoll) {
      return;
    }

    setIsVoting(true);
    try {
      await onVotePoll(selectedOptionId);
    } finally {
      setIsVoting(false);
    }
  };

  return (
    <div className={styles.container}>
      <h3 className={styles.title}>帖子详情</h3>
      <div className={styles.postContent}>
        <h4 className={styles.postTitle}>{post.voTitle}</h4>
        <div className={styles.postMeta}>
          {post.voAuthorName && (
            <button
              type="button"
              className={styles.authorLink}
              onClick={() => onAuthorClick?.(post.voAuthorId, post.voAuthorName, post.voAuthorAvatarUrl)}
            >
              作者：{post.voAuthorName}
            </button>
          )}
          {post.voCreateTime && <span> · {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}</span>}
          {post.voViewCount !== undefined && <span> · 浏览 {post.voViewCount}</span>}
        </div>
        <Suspense fallback={<div className={styles.postBody}>正文渲染中...</div>}>
          <MarkdownRenderer content={post.voContent} className={styles.postBody} stickerMap={stickerMap} />
        </Suspense>
        {tagList.length > 0 && (
          <div className={styles.postTags}>
            {tagList.map((tag, index) => (
              <span key={index} className={styles.tag}>
                {tag}
              </span>
            ))}
          </div>
        )}

        {poll && (
          <section className={styles.pollCard}>
            <div className={styles.pollHeader}>
              <div>
                <div className={styles.pollTitleRow}>
                  <span className={styles.pollBadge}>投票</span>
                  <h5 className={styles.pollQuestion}>{poll.voQuestion}</h5>
                </div>
                <p className={styles.pollMeta}>
                  共 {poll.voTotalVoteCount} 票
                  {poll.voEndTime ? ` · 截止于 ${formatDateTimeByTimeZone(poll.voEndTime, displayTimeZone)}` : ' · 长期有效'}
                </p>
              </div>
              <span className={`${styles.pollState} ${poll.voIsClosed ? styles.pollStateClosed : ''}`}>
                {poll.voIsClosed ? '已截止' : poll.voHasVoted ? '已投票' : '进行中'}
              </span>
            </div>

            <div className={styles.pollOptions}>
              {poll.voOptions.map((option) => {
                const isSelected = selectedOptionId === option.voOptionId;
                const isVotedOption = poll.voSelectedOptionId === option.voOptionId;
                return (
                  <button
                    key={option.voOptionId}
                    type="button"
                    className={`${styles.pollOption} ${isSelected ? styles.pollOptionSelected : ''} ${isVotedOption ? styles.pollOptionVoted : ''}`}
                    onClick={() => {
                      if (canSubmitPoll) {
                        setSelectedOptionId(option.voOptionId);
                      }
                    }}
                    disabled={!canSubmitPoll}
                  >
                    <div className={styles.pollOptionTop}>
                      <span className={styles.pollOptionText}>{option.voOptionText}</span>
                      <span className={styles.pollOptionValue}>
                        {option.voVoteCount} 票 · {option.voVotePercent.toFixed(2)}%
                      </span>
                    </div>
                    <div className={styles.pollProgressTrack}>
                      <span
                        className={styles.pollProgressValue}
                        style={{ width: `${Math.max(0, Math.min(option.voVotePercent, 100))}%` }}
                      />
                    </div>
                  </button>
                );
              })}
            </div>

            <div className={styles.pollFooter}>
              <span className={styles.pollHint}>{pollStatusText}</span>
              <button
                type="button"
                className={styles.pollSubmitButton}
                onClick={handleVoteSubmit}
                disabled={!canSubmitPoll || !selectedOptionId || isVoting}
              >
                {isVoting ? '提交中...' : '提交投票'}
              </button>
            </div>
          </section>
        )}

        <div className={styles.actions}>
          <button
            type="button"
            onClick={() => onLike?.(post.voId)}
            className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            disabled={!isAuthenticated}
            title={!isAuthenticated ? '请先登录' : isLiked ? '取消点赞' : '点赞'}
          >
            <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
            <span className={styles.likeCount}>{post.voLikeCount || 0}</span>
          </button>
          <span className={styles.commentCount}>
            💬 {post.voCommentCount || 0} 条评论
          </span>

          {showFollowAction && (
            <div className={styles.relationActions}>
              <span className={styles.relationInfo}>
                粉丝 {followStatus?.voFollowerCount ?? 0} · 关注 {followStatus?.voFollowingCount ?? 0}
              </span>
              <button
                type="button"
                className={`${styles.followButton} ${followStatus?.voIsFollowing ? styles.following : ''}`}
                onClick={() => onToggleFollow?.(post.voAuthorId, !!followStatus?.voIsFollowing)}
                disabled={followLoading}
                title={followStatus?.voIsFollowing ? '取消关注' : '关注作者'}
              >
                {followLoading ? '处理中...' : followStatus?.voIsFollowing ? '已关注' : '关注'}
              </button>
            </div>
          )}

          {onToggleReaction && (
            <ReactionBar
              targetType="Post"
              targetId={post.voId}
              items={postReactions}
              isLoggedIn={isAuthenticated}
              loading={reactionLoading}
              stickerGroups={stickerGroups}
              onToggle={onToggleReaction}
              onRequireLogin={onRequireReactionLogin}
            />
          )}

          {isAuthor && (
            <div className={styles.authorActions}>
              <button
                type="button"
                onClick={() => onEdit?.(post.voId)}
                className={styles.editButton}
                title="编辑帖子"
              >
                <Icon icon="mdi:pencil" size={18} />
                编辑
              </button>
              <button
                type="button"
                onClick={() => onDelete?.(post.voId)}
                className={styles.deleteButton}
                title="删除帖子"
              >
                <Icon icon="mdi:delete" size={18} />
                删除
              </button>
              <button
                type="button"
                onClick={() => onViewHistory?.(post.voId)}
                className={styles.historyButton}
                title="查看编辑历史"
              >
                <Icon icon="mdi:history" size={18} />
                历史
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
