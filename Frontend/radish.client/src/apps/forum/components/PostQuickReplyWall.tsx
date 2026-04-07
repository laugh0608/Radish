import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent
} from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import { getOidcLoginUrl, type PostQuickReply } from '@/api/forum';
import { resolveMediaUrl } from '@/utils/media';
import styles from './PostQuickReplyWall.module.css';

interface PostQuickReplyWallProps {
  replies: PostQuickReply[];
  total: number;
  loading: boolean;
  isAuthenticated: boolean;
  currentUserId: number;
  onCreate: (content: string) => Promise<void>;
  onDelete: (quickReplyId: number) => Promise<void>;
  onReport: (quickReplyId: number) => void;
}

interface QuickReplyLaneLayout {
  items: PostQuickReply[];
  overflowWidth: number;
  shouldAnimate: boolean;
}

const MAX_LENGTH = 10;
const LANE_COUNT = 3;
const PILL_GAP = 10;
const DEFAULT_WALL_WIDTH = 960;

const buildAvatarText = (name: string): string => {
  const normalized = name.trim();
  if (!normalized) {
    return '?';
  }

  return normalized.charAt(0).toUpperCase();
};

const buildAvatarStyle = (seed: string) => {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = seed.charCodeAt(index) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 72% 92%)`,
    color: `hsl(${hue} 42% 28%)`
  };
};

const estimatePillWidth = (reply: PostQuickReply): number => {
  const authorLength = Math.min(6, reply.voAuthorName?.trim().length ?? 1);
  const contentLength = Math.min(14, reply.voContent.trim().length);
  return 92 + authorLength * 10 + contentLength * 14;
};

const getTrackStyle = (overflowWidth: number): CSSProperties => ({
  '--drift-distance': `${Math.max(0, Math.ceil(overflowWidth))}px`,
  '--drift-duration': `${Math.max(16, Math.min(44, 16 + overflowWidth / 18))}s`
} as CSSProperties);

export const PostQuickReplyWall = ({
  replies,
  total,
  loading,
  isAuthenticated,
  currentUserId,
  onCreate,
  onDelete,
  onReport,
}: PostQuickReplyWallProps) => {
  const { t } = useTranslation();
  const [content, setContent] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [wallWidth, setWallWidth] = useState(0);
  const [measuredWidths, setMeasuredWidths] = useState<Record<number, number>>({});
  const wallRef = useRef<HTMLDivElement>(null);
  const measureRefs = useRef<Record<number, HTMLDivElement | null>>({});

  const normalizedContent = useMemo(
    () => content.trim().replace(/\s+/g, ' '),
    [content]
  );

  useEffect(() => {
    const element = wallRef.current;
    if (!element) {
      return;
    }

    const updateWidth = () => {
      setWallWidth(element.clientWidth);
    };

    updateWidth();

    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', updateWidth);
      return () => window.removeEventListener('resize', updateWidth);
    }

    const observer = new ResizeObserver(() => updateWidth());
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  useLayoutEffect(() => {
    if (replies.length === 0) {
      setMeasuredWidths({});
      measureRefs.current = {};
      return;
    }

    const nextWidths: Record<number, number> = {};
    let changed = false;
    const nextReplyIds = new Set<number>();

    replies.forEach((reply) => {
      nextReplyIds.add(reply.voId);
      const width = measureRefs.current[reply.voId]?.offsetWidth ?? 0;
      if (width > 0) {
        nextWidths[reply.voId] = width;
      }
    });

    Object.keys(measureRefs.current).forEach((key) => {
      const replyId = Number(key);
      if (!nextReplyIds.has(replyId)) {
        delete measureRefs.current[replyId];
      }
    });

    if (Object.keys(nextWidths).length !== Object.keys(measuredWidths).length) {
      changed = true;
    } else {
      changed = replies.some((reply) => measuredWidths[reply.voId] !== nextWidths[reply.voId]);
    }

    if (changed) {
      setMeasuredWidths(nextWidths);
    }
  }, [replies, measuredWidths]);

  const laneLayouts = useMemo<QuickReplyLaneLayout[]>(() => {
    if (replies.length === 0) {
      return [];
    }

    const availableWidth = wallWidth > 0 ? Math.max(320, wallWidth) : DEFAULT_WALL_WIDTH;
    const replyWidths = replies.map((reply) => measuredWidths[reply.voId] ?? estimatePillWidth(reply));

    const lanes = Array.from({ length: LANE_COUNT }, () => [] as PostQuickReply[]);
    const laneWidths = Array.from({ length: LANE_COUNT }, () => 0);

    let replyIndex = 0;
    let laneIndex = 0;

    while (replyIndex < replies.length && laneIndex < LANE_COUNT)
    {
      const reply = replies[replyIndex];
      const nextWidth = replyWidths[replyIndex] + (lanes[laneIndex].length > 0 ? PILL_GAP : 0);

      if (lanes[laneIndex].length === 0 || laneWidths[laneIndex] + nextWidth <= availableWidth)
      {
        lanes[laneIndex].push(reply);
        laneWidths[laneIndex] += nextWidth;
        replyIndex += 1;
        continue;
      }

      laneIndex += 1;
    }

    const hasOverflowItems = replyIndex < replies.length;
    let overflowLaneIndex = 0;

    while (replyIndex < replies.length)
    {
      const reply = replies[replyIndex];
      const nextWidth = replyWidths[replyIndex] + (lanes[overflowLaneIndex].length > 0 ? PILL_GAP : 0);
      lanes[overflowLaneIndex].push(reply);
      laneWidths[overflowLaneIndex] += nextWidth;
      overflowLaneIndex = (overflowLaneIndex + 1) % LANE_COUNT;
      replyIndex += 1;
    }

    return lanes
      .map((items, index) => {
        const contentWidth = Math.max(0, laneWidths[index]);
        const overflowWidth = Math.max(0, contentWidth - availableWidth);

        return {
          items,
          overflowWidth,
          shouldAnimate: hasOverflowItems && overflowWidth > 0 && items.length > 1
        };
      })
      .filter(lane => lane.items.length > 0);
  }, [measuredWidths, replies, wallWidth]);

  const handleLogin = () => {
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  const handleSubmit = async () => {
    if (!normalizedContent || submitting || !isAuthenticated) {
      return;
    }

    setSubmitting(true);
    try {
      await onCreate(normalizedContent);
      setContent('');
      toast.success(t('forum.quickReply.submitSuccess'));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t('forum.quickReply.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (quickReplyId: number) => {
    if (!window.confirm(t('forum.quickReply.deleteConfirm'))) {
      return;
    }

    setDeletingId(quickReplyId);
    try {
      await onDelete(quickReplyId);
      toast.success(t('forum.quickReply.deleteSuccess'));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t('forum.quickReply.deleteFailed'));
    } finally {
      setDeletingId(null);
    }
  };

  const handleComposerKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void handleSubmit();
    }
  };

  return (
    <section className={styles.section} aria-labelledby="forum-quick-reply-title">
      <div className={styles.header}>
        <div className={styles.headerMain}>
          <div className={styles.titleRow}>
            <h3 id="forum-quick-reply-title" className={styles.title}>
              {t('forum.quickReply.title')}
            </h3>
            <span className={styles.total}>{t('forum.quickReply.total', { count: total })}</span>
          </div>
          <p className={styles.subtitle}>{t('forum.quickReply.subtitle')}</p>
        </div>
      </div>

      <div className={styles.composer}>
        <div className={styles.composerShell}>
          <textarea
            className={styles.textarea}
            placeholder={isAuthenticated ? t('forum.quickReply.placeholder') : t('forum.quickReply.loginPrompt')}
            value={content}
            onChange={(event) => setContent(event.target.value.slice(0, MAX_LENGTH))}
            onKeyDown={handleComposerKeyDown}
            rows={1}
            maxLength={MAX_LENGTH}
            readOnly={!isAuthenticated}
            onFocus={() => {
              if (!isAuthenticated) {
                handleLogin();
              }
            }}
            onClick={() => {
              if (!isAuthenticated) {
                handleLogin();
              }
            }}
          />

          {isAuthenticated ? (
            <button
              type="button"
              className={styles.submitButton}
              onClick={() => {
                void handleSubmit();
              }}
              disabled={submitting || !normalizedContent}
            >
              {submitting ? t('forum.quickReply.submitting') : t('forum.quickReply.submit')}
            </button>
          ) : (
            <button type="button" className={styles.loginButton} onClick={handleLogin}>
              {t('forum.quickReply.loginButton')}
            </button>
          )}
        </div>

        <div className={styles.composerMeta}>
          <span className={styles.hint}>{t('forum.quickReply.inputHint')}</span>
          <span className={styles.count}>{t('forum.quickReply.charCount', { count: normalizedContent.length, max: MAX_LENGTH })}</span>
        </div>
      </div>

      <div className={styles.wall} ref={wallRef}>
        {loading ? (
          <div className={styles.placeholder}>{t('forum.quickReply.loading')}</div>
        ) : replies.length === 0 ? (
          <div className={styles.placeholder}>{t('forum.quickReply.empty')}</div>
        ) : (
          <div className={styles.lanes}>
            {laneLayouts.map((lane, laneIndex) => (
              <div
                key={`lane-${laneIndex}`}
                className={styles.lane}
                data-direction={laneIndex % 2 === 0 ? 'left' : 'right'}
              >
                <div
                  className={`${styles.laneTrack} ${lane.shouldAnimate ? styles.laneTrackAnimated : ''}`}
                  style={lane.shouldAnimate ? getTrackStyle(lane.overflowWidth) : undefined}
                >
                  {lane.items.map((reply) => {
                    const avatarUrl = resolveMediaUrl(reply.voAuthorAvatarUrl);
                    const isOwner = String(reply.voAuthorId) === String(currentUserId);
                    const authorName = reply.voAuthorName?.trim() || t('common.unknownUser');

                    return (
                      <article key={reply.voId} className={styles.pill}>
                        <div className={styles.avatar}>
                          {avatarUrl ? (
                            <img src={avatarUrl} alt={authorName} className={styles.avatarImage} />
                          ) : (
                            <span className={styles.avatarFallback} style={buildAvatarStyle(authorName)}>
                              {buildAvatarText(authorName)}
                            </span>
                          )}
                        </div>

                        <span className={styles.author}>{authorName}</span>
                        <span className={styles.content}>{reply.voContent}</span>

                        {isOwner ? (
                          <button
                            type="button"
                            className={styles.actionButton}
                            onClick={() => {
                              void handleDelete(reply.voId);
                            }}
                            disabled={deletingId === reply.voId}
                            title={t('common.delete')}
                          >
                            <Icon icon={deletingId === reply.voId ? 'mdi:loading' : 'mdi:trash-can-outline'} size={15} />
                          </button>
                        ) : (
                          <button
                            type="button"
                            className={styles.actionButton}
                            onClick={() => onReport(reply.voId)}
                            title={t('report.action')}
                          >
                            <Icon icon="mdi:alert-circle-outline" size={15} />
                          </button>
                        )}
                      </article>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className={styles.measureLayer} aria-hidden="true">
        {replies.map((reply) => {
          const authorName = reply.voAuthorName?.trim() || t('common.unknownUser');
          const avatarUrl = resolveMediaUrl(reply.voAuthorAvatarUrl);
          const isOwner = String(reply.voAuthorId) === String(currentUserId);

          return (
            <div
              key={`measure-${reply.voId}`}
              ref={(node) => {
                measureRefs.current[reply.voId] = node;
              }}
              className={`${styles.pill} ${styles.measurePill}`}
            >
              <div className={styles.avatar}>
                {avatarUrl ? (
                  <img src={avatarUrl} alt={authorName} className={styles.avatarImage} />
                ) : (
                  <span className={styles.avatarFallback} style={buildAvatarStyle(authorName)}>
                    {buildAvatarText(authorName)}
                  </span>
                )}
              </div>
              <span className={styles.author}>{authorName}</span>
              <span className={styles.content}>{reply.voContent}</span>
              <button type="button" className={styles.actionButton} tabIndex={-1}>
                <Icon icon={isOwner ? 'mdi:trash-can-outline' : 'mdi:alert-circle-outline'} size={15} />
              </button>
            </div>
          );
        })}
      </div>
    </section>
  );
};
