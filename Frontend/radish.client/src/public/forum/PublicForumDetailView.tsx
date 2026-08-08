import type { ComponentProps, ReactNode, RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { PostDetail, PostQuickReply } from '@/api/forum';
import { CommentTree } from '@/apps/forum/components/CommentTree';
import { CreateCommentForm } from '@/apps/forum/components/CreateCommentForm';
import { PostDetail as ForumPostDetail } from '@/apps/forum/components/PostDetail';
import { PostQuickReplyWall } from '@/apps/forum/components/PostQuickReplyWall';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';
import type {
  PublicForumDetailLoadState,
  PublicForumReadSectionState,
} from './publicForumViewState';
import { PublicStatusCard } from './PublicStatusCard';
import styles from './PublicForumApp.module.css';

const POST_SECTION_ID = 'public-forum-post';
const QUICK_REPLY_SECTION_ID = 'public-forum-quick-replies';
const COMMENT_SECTION_ID = 'public-forum-comments';

interface DetailActionLink {
  href: string;
  label: string;
  icon: string;
  onActivate: () => void;
  primary?: boolean;
  disabled?: boolean;
}

interface PublicForumDetailViewProps {
  detailState: PublicForumDetailLoadState;
  post: PostDetail | null;
  backLabel: string;
  backHref: string;
  onBack: () => void;
  navigationLocked: boolean;
  shareBusy: boolean;
  shareState: 'idle' | 'success' | 'error';
  onCopyShareLink: () => void;
  onRetry: () => void;
  quickReplyTotal: number;
  commentTotal: number;
  postDetailProps: Omit<ComponentProps<typeof ForumPostDetail>, 'post' | 'loading'>;
  actionLinks: DetailActionLink[];
  categoriesError: string | null;
  quickReplySectionState: PublicForumReadSectionState;
  quickReplyError: string | null;
  quickReplies: PostQuickReply[];
  quickReplyProps: Omit<
    ComponentProps<typeof PostQuickReplyWall>,
    'sectionId' | 'replies' | 'total' | 'loading'
  > & { loading: boolean };
  commentSectionState: PublicForumReadSectionState;
  commentError: string | null;
  commentPagingError: string | null;
  commentNavigationNotice: string | null;
  commentNoticeRef: RefObject<HTMLDivElement | null>;
  commentTypingText: string | null;
  loadedCommentCount: number;
  commentSortLabel: string;
  commentComposerProps: ComponentProps<typeof CreateCommentForm>;
  commentTreeProps: ComponentProps<typeof CommentTree>;
  dialogs: ReactNode;
}

const SectionLinks = ({
  quickReplyTotal,
  commentTotal,
  mobile = false,
}: {
  quickReplyTotal: number;
  commentTotal: number;
  mobile?: boolean;
}) => {
  const { t } = useTranslation();
  const className = mobile ? styles.detailMobileNav : styles.detailRailNav;
  const linkClassName = mobile ? styles.detailMobileNavLink : styles.detailRailNavLink;

  return (
    <nav className={className} aria-label={t('forum.public.detailThreadNavTitle')}>
      <a className={linkClassName} href={`#${POST_SECTION_ID}`}>
        <Icon icon="mdi:file-document-outline" size={17} />
        <span>{t('forum.public.detailReadSection')}</span>
      </a>
      <a className={linkClassName} href={`#${QUICK_REPLY_SECTION_ID}`}>
        <Icon icon="mdi:message-flash-outline" size={17} />
        <span>{t('forum.quickReply.title')}</span>
        <span className={styles.detailRailCount}>{quickReplyTotal}</span>
      </a>
      <a className={linkClassName} href={`#${COMMENT_SECTION_ID}`}>
        <Icon icon="mdi:comment-text-outline" size={17} />
        <span>{t('forum.commentTree.title')}</span>
        <span className={styles.detailRailCount}>{commentTotal}</span>
      </a>
    </nav>
  );
};

export const PublicForumDetailView = ({
  detailState,
  post,
  backLabel,
  backHref,
  onBack,
  navigationLocked,
  shareBusy,
  shareState,
  onCopyShareLink,
  onRetry,
  quickReplyTotal,
  commentTotal,
  postDetailProps,
  actionLinks,
  categoriesError,
  quickReplySectionState,
  quickReplyError,
  quickReplies,
  quickReplyProps,
  commentSectionState,
  commentError,
  commentPagingError,
  commentNavigationNotice,
  commentNoticeRef,
  commentTypingText,
  loadedCommentCount,
  commentSortLabel,
  commentComposerProps,
  commentTreeProps,
  dialogs,
}: PublicForumDetailViewProps) => {
  const { t } = useTranslation();

  return (
    <div className={`${styles.forumGrid} ${styles.detailForumGrid}`}>
      <aside className={styles.detailCommunityRail} aria-label={t('forum.public.detailCommunityNavTitle')}>
        <div className={styles.detailRailPanel}>
          <p className={styles.detailRailKicker}>{t('forum.public.detailCommunityNavTitle')}</p>
          <a
            className={styles.detailRailBackLink}
            href={backHref}
            onClick={(event) => handlePublicForumLinkClick(event, onBack)}
            aria-disabled={navigationLocked}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{backLabel}</span>
          </a>
          <SectionLinks quickReplyTotal={quickReplyTotal} commentTotal={commentTotal} />
        </div>
      </aside>

      <main className={`${styles.sectionCard} ${styles.detailSectionCard}`}>
        <div className={styles.detailTopbar}>
          <div className={styles.detailTopbarActions}>
            <a
              className={styles.backButton}
              href={backHref}
              onClick={(event) => handlePublicForumLinkClick(event, onBack)}
              aria-disabled={navigationLocked}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{backLabel}</span>
            </a>
            <button type="button" className={styles.secondaryButton} onClick={onCopyShareLink} disabled={shareBusy}>
              <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
              <span>{shareBusy ? t('forum.public.shareSubmitting') : t('forum.public.shareAction')}</span>
            </button>
          </div>
          {shareState !== 'idle' && (
            <p className={styles.shareFeedback} data-state={shareState}>
              {shareState === 'success' ? t('forum.public.shareSuccess') : t('forum.public.shareFailed')}
            </p>
          )}
        </div>

        <div className={styles.detailMobileContext}>
          <SectionLinks quickReplyTotal={quickReplyTotal} commentTotal={commentTotal} mobile={true} />
        </div>

        <div className={styles.detailStack}>
          {detailState.kind === 'loading' && (
            <PublicStatusCard
              tone="loading"
              title={t('forum.public.loadingTitle')}
              description={t('forum.public.loadingDescription')}
            />
          )}
          {detailState.kind === 'notFound' && (
            <PublicStatusCard
              tone="notFound"
              title={t('forum.public.postNotFoundTitle')}
              description={t('forum.public.postNotFoundDescription')}
              secondaryAction={{ label: backLabel, href: backHref, onClick: onBack }}
            />
          )}
          {detailState.kind === 'error' && (
            <PublicStatusCard
              tone="error"
              title={t('forum.public.postErrorTitle')}
              description={detailState.message}
              primaryAction={{ label: t('common.retry'), onClick: onRetry }}
              secondaryAction={{ label: backLabel, href: backHref, onClick: onBack }}
            />
          )}

          {detailState.kind === 'ready' && post && (
            <>
              <div className={styles.detailMetaRail}>
                <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
                <span className={styles.detailMetaChip}>{t('forum.postDetail.views', { count: post.voViewCount ?? 0 })}</span>
                <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
                <span className={styles.detailMetaChip}>{t('forum.postDetail.commentCount', { count: commentTotal })}</span>
              </div>

              <article id={POST_SECTION_ID} className={styles.detailArticle}>
                <ForumPostDetail post={post} loading={false} {...postDetailProps} />
              </article>

              {actionLinks.length > 0 && (
                <nav className={styles.detailActionBand} aria-label={t('forum.public.workspaceActionTitle')}>
                  {actionLinks.map((action) => (
                    <a
                      key={`${action.href}:${action.label}`}
                      href={action.href}
                      className={`${styles.detailActionLink} ${action.primary ? styles.detailActionLinkPrimary : ''}`}
                      aria-disabled={action.disabled}
                      onClick={(event) => {
                        if (action.disabled) {
                          event.preventDefault();
                          return;
                        }
                        handlePublicForumLinkClick(event, action.onActivate);
                      }}
                    >
                      <Icon icon={action.disabled ? 'mdi:progress-clock' : action.icon} size={18} />
                      <span>{action.label}</span>
                    </a>
                  ))}
                </nav>
              )}

              {categoriesError && (
                <div className={styles.inlineNotice} data-tone="warning">
                  <span className={styles.inlineNoticeText}>{categoriesError}</span>
                </div>
              )}

              <div className={styles.detailInlineThreadNav}>
                <p>{t('forum.public.detailThreadNavTitle')}</p>
                <SectionLinks quickReplyTotal={quickReplyTotal} commentTotal={commentTotal} mobile={true} />
              </div>

              {quickReplySectionState === 'error' ? (
                <section id={QUICK_REPLY_SECTION_ID} className={styles.sectionShell}>
                  <div className={styles.sectionShellHeader}>
                    <h2 className={styles.sectionShellTitle}>{t('forum.quickReply.title')}</h2>
                    <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
                  </div>
                  <PublicStatusCard
                    tone="error"
                    compact={true}
                    title={t('forum.public.partialErrorTitle')}
                    description={quickReplyError || t('forum.public.quickRepliesErrorDescription')}
                    primaryAction={{ label: t('common.retry'), onClick: onRetry }}
                  />
                </section>
              ) : (
                <>
                  {quickReplyError && (
                    <div className={styles.inlineNotice} data-tone="warning">
                      <span className={styles.inlineNoticeText}>{t('forum.public.quickRepliesErrorDescription')}</span>
                    </div>
                  )}
                  <PostQuickReplyWall
                    sectionId={QUICK_REPLY_SECTION_ID}
                    replies={quickReplies}
                    total={quickReplyTotal}
                    {...quickReplyProps}
                  />
                </>
              )}

              <section id={COMMENT_SECTION_ID} className={styles.commentSection} aria-labelledby={`${COMMENT_SECTION_ID}-title`}>
                <div className={styles.commentHeading}>
                  <div>
                    <h2 id={`${COMMENT_SECTION_ID}-title`} className={styles.commentTitle}>{t('forum.commentTree.title')}</h2>
                    <p className={styles.commentIntro}>{t('forum.quickReply.discussionSubtitle')}</p>
                  </div>
                  <div className={styles.commentSummary}>
                    <span className={styles.commentSummaryChip}>
                      {t('forum.public.loadedComments', { loaded: loadedCommentCount, total: commentTotal })}
                    </span>
                    <span className={styles.commentSummaryChip}>{t('forum.public.commentOrder', { label: commentSortLabel })}</span>
                  </div>
                </div>

                {commentPagingError && <div className={styles.inlineNotice} data-tone="warning"><span className={styles.inlineNoticeText}>{t('forum.public.commentPagingErrorDescription')}</span></div>}
                {commentNavigationNotice && <div ref={commentNoticeRef} className={styles.inlineNotice} data-tone="warning"><span className={styles.inlineNoticeText}>{commentNavigationNotice}</span></div>}
                {commentTypingText && <div className={styles.inlineNotice}><span className={styles.inlineNoticeText}>{commentTypingText}</span></div>}

                <div className={styles.commentComposerPanel}>
                  <CreateCommentForm {...commentComposerProps} />
                </div>

                {commentSectionState === 'error' ? (
                  <PublicStatusCard
                    tone="error"
                    compact={true}
                    title={t('forum.public.partialErrorTitle')}
                    description={commentError || t('forum.public.commentsErrorDescription')}
                    primaryAction={{ label: t('common.retry'), onClick: onRetry }}
                  />
                ) : (
                  <>
                    {commentError && <div className={styles.inlineNotice} data-tone="warning"><span className={styles.inlineNoticeText}>{t('forum.public.commentsErrorDescription')}</span></div>}
                    <CommentTree {...commentTreeProps} />
                  </>
                )}
              </section>
              {dialogs}
            </>
          )}
        </div>
      </main>

      {detailState.kind === 'ready' && (
        <aside className={styles.detailThreadRail} aria-label={t('forum.public.detailThreadNavTitle')}>
          <div className={styles.detailRailPanel}>
            <p className={styles.detailRailKicker}>{t('forum.public.detailThreadNavTitle')}</p>
            <SectionLinks quickReplyTotal={quickReplyTotal} commentTotal={commentTotal} />
          </div>
        </aside>
      )}
    </div>
  );
};
