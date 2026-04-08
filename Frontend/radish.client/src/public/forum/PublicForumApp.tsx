import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getCurrentGodCommentsBatch,
  getPostById,
  getPostList,
  getPostQuickReplyWall,
  getRootCommentsPage,
  getTopCategories,
  type Category,
  type CommentHighlight,
  type CommentNode,
  type PostDetail,
  type PostItem,
  type PostQuickReply,
} from '@/api/forum';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import { PostDetail as ForumPostDetail } from '@/apps/forum/components/PostDetail';
import { PostQuickReplyWall } from '@/apps/forum/components/PostQuickReplyWall';
import { CommentTree } from '@/apps/forum/components/CommentTree';
import type { PublicForumRoute } from '../PublicEntry';
import styles from './PublicForumApp.module.css';

interface PublicForumAppProps {
  route: PublicForumRoute;
  onNavigate: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
}

type PublicListSort = 'newest' | 'hottest';
type RootCommentSort = 'newest' | 'hottest' | null;

function buildActiveSectionTitle(categories: Category[], selectedCategoryId: number | null, fallback: string): string {
  if (!selectedCategoryId) {
    return fallback;
  }

  return categories.find((item) => item.voId === selectedCategoryId)?.voName || fallback;
}

export const PublicForumApp = ({ route, onNavigate }: PublicForumAppProps) => {
  const { t } = useTranslation();
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));

  useEffect(() => {
    const nextTitle = route.kind === 'detail'
      ? `${t('desktop.apps.forum.name')} · ${t('forum.postDetail.title')}`
      : `${t('desktop.apps.forum.name')} · ${t('forum.allPosts')}`;

    document.title = nextTitle;
  }, [route.kind, t]);

  return (
    <div className={styles.page}>
      <header className={styles.hero}>
        <div className={styles.heroInner}>
          <button
            type="button"
            className={styles.brand}
            onClick={() => onNavigate({ kind: 'list' })}
          >
            <span className={styles.brandMark}>R</span>
            <span className={styles.brandText}>
              <span className={styles.brandName}>{t('desktop.apps.forum.name')}</span>
              <span className={styles.brandSubline}>Public Content Shell</span>
            </span>
          </button>
          <a className={styles.desktopLink} href="/">
            <Icon icon="mdi:view-dashboard-outline" size={18} />
            <span>WebOS</span>
          </a>
        </div>
      </header>

      <main className={styles.main}>
        {route.kind === 'detail' && route.postId ? (
          <PublicForumDetail
            key={`detail-${route.postId}`}
            postId={route.postId}
            displayTimeZone={displayTimeZone}
            onBack={() => onNavigate({ kind: 'list' })}
          />
        ) : (
          <PublicForumList
            key="list"
            displayTimeZone={displayTimeZone}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
          />
        )}
      </main>
    </div>
  );
};

interface PublicForumListProps {
  displayTimeZone: string;
  onOpenPost: (postId: number) => void;
}

const PublicForumList = ({ displayTimeZone, onOpenPost }: PublicForumListProps) => {
  const { t } = useTranslation();
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [sortBy, setSortBy] = useState<PublicListSort>('newest');
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<number, CommentHighlight>>(new Map());
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const postsRequestIdRef = useRef(0);

  useEffect(() => {
    let cancelled = false;

    const loadCategories = async () => {
      setLoadingCategories(true);
      try {
        const data = await getTopCategories(t);
        if (!cancelled) {
          setCategories(data);
        }
      } catch (err) {
        if (!cancelled) {
          const message = err instanceof Error ? err.message : String(err);
          setError(message);
        }
      } finally {
        if (!cancelled) {
          setLoadingCategories(false);
        }
      }
    };

    void loadCategories();
    return () => {
      cancelled = true;
    };
  }, [t]);

  useEffect(() => {
    const requestId = ++postsRequestIdRef.current;

    const loadPosts = async () => {
      setLoadingPosts(true);
      setError(null);
      try {
        const pageModel = await getPostList(
          selectedCategoryId,
          t,
          currentPage,
          20,
          sortBy
        );

        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        setPosts(pageModel.data);
        setTotalPages(pageModel.pageCount);

        if (!pageModel.data.length) {
          setPostGodComments(new Map());
          return;
        }

        const highlights = await getCurrentGodCommentsBatch(pageModel.data.map((item) => item.voId), t);
        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        const nextMap = new Map<number, CommentHighlight>();
        for (const [postIdText, highlight] of Object.entries(highlights)) {
          const postId = Number(postIdText);
          if (!Number.isNaN(postId) && highlight) {
            nextMap.set(postId, highlight);
          }
        }
        setPostGodComments(nextMap);
      } catch (err) {
        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setPosts([]);
        setPostGodComments(new Map());
        setTotalPages(0);
        setError(message);
      } finally {
        if (requestId === postsRequestIdRef.current) {
          setLoadingPosts(false);
        }
      }
    };

    void loadPosts();
  }, [currentPage, selectedCategoryId, sortBy, t]);

  const activeTitle = useMemo(
    () => buildActiveSectionTitle(categories, selectedCategoryId, t('forum.allPosts')),
    [categories, selectedCategoryId, t]
  );

  const visiblePages = useMemo(() => {
    if (totalPages <= 7) {
      return Array.from({ length: totalPages }, (_, index) => index + 1);
    }

    if (currentPage <= 4) {
      return [1, 2, 3, 4, 5, 6, 7];
    }

    if (currentPage >= totalPages - 3) {
      return Array.from({ length: 7 }, (_, index) => totalPages - 6 + index);
    }

    return Array.from({ length: 7 }, (_, index) => currentPage - 3 + index);
  }, [currentPage, totalPages]);

  return (
    <section className={styles.sectionCard}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>Phase 2-2</p>
          <h1 className={styles.pageTitle}>{activeTitle}</h1>
          <p className={styles.pageIntro}>
            公开内容壳层先承载论坛阅读入口，当前首批只覆盖公开列表与帖子详情，不引入桌面窗口交互。
          </p>
        </div>

        <div className={styles.toolbar}>
          <div className={styles.segmented}>
            <button
              type="button"
              className={`${styles.segmentButton} ${!selectedCategoryId ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }}
            >
              {t('forum.allPosts')}
            </button>
            <button
              type="button"
              className={`${styles.segmentButton} ${sortBy === 'newest' ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSortBy('newest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.newest')}
            </button>
            <button
              type="button"
              className={`${styles.segmentButton} ${sortBy === 'hottest' ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSortBy('hottest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.hottest')}
            </button>
          </div>
        </div>
      </div>

      <div className={styles.categoryRail}>
        {loadingCategories ? (
          <span className={styles.categoryHint}>{t('forum.category.loading')}</span>
        ) : categories.length === 0 ? (
          <span className={styles.categoryHint}>{t('forum.category.empty')}</span>
        ) : (
          categories.map((category) => (
            <button
              key={category.voId}
              type="button"
              className={`${styles.categoryChip} ${selectedCategoryId === category.voId ? styles.categoryChipActive : ''}`}
              onClick={() => {
                setSelectedCategoryId((current) => (current === category.voId ? null : category.voId));
                setCurrentPage(1);
              }}
            >
              {category.voName}
            </button>
          ))
        )}
      </div>

      {error && <p className={styles.errorText}>{t('common.errorPrefix')}{error}</p>}

      <div className={styles.postList}>
        {loadingPosts ? (
          <p className={styles.placeholder}>{t('forum.loadingPosts')}</p>
        ) : posts.length === 0 ? (
          <p className={styles.placeholder}>{t('forum.emptyPosts')}</p>
        ) : (
          posts.map((post) => {
            const godComment = postGodComments.get(post.voId);
            return (
              <PostCard
                key={post.voId}
                post={post}
                displayTimeZone={displayTimeZone}
                onClick={() => onOpenPost(post.voId)}
                godComment={godComment ? {
                  authorName: godComment.voAuthorName,
                  content: godComment.voContentSnapshot
                } : null}
              />
            );
          })
        )}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
            className={styles.paginationButton}
            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          {visiblePages.map((page) => (
            <button
              key={page}
              type="button"
              className={`${styles.paginationButton} ${page === currentPage ? styles.paginationButtonActive : ''}`}
              onClick={() => setCurrentPage(page)}
            >
              {page}
            </button>
          ))}
          <button
            type="button"
            className={styles.paginationButton}
            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
            disabled={currentPage === totalPages}
          >
            ›
          </button>
        </div>
      )}
    </section>
  );
};

interface PublicForumDetailProps {
  postId: number;
  displayTimeZone: string;
  onBack: () => void;
}

const PublicForumDetail = ({ postId, displayTimeZone, onBack }: PublicForumDetailProps) => {
  const { t } = useTranslation();
  const [post, setPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);
  const [quickReplies, setQuickReplies] = useState<PostQuickReply[]>([]);
  const [quickReplyTotal, setQuickReplyTotal] = useState(0);
  const [commentTotal, setCommentTotal] = useState(0);
  const [loadedCommentPages, setLoadedCommentPages] = useState(0);
  const [commentSortBy, setCommentSortBy] = useState<RootCommentSort>(null);
  const [loadingPost, setLoadingPost] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingQuickReplies, setLoadingQuickReplies] = useState(false);
  const [loadingMoreComments, setLoadingMoreComments] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);
  const commentPageSize = 20;

  useEffect(() => {
    const requestId = ++requestIdRef.current;

    const loadDetail = async () => {
      setLoadingPost(true);
      setLoadingComments(true);
      setLoadingQuickReplies(true);
      setError(null);

      try {
        const [postDetail, rootComments, replyWall] = await Promise.all([
          getPostById(postId, t),
          getRootCommentsPage(postId, 1, commentPageSize, commentSortBy || 'default', t),
          getPostQuickReplyWall(postId, t)
        ]);

        if (requestId !== requestIdRef.current) {
          return;
        }

        setPost(postDetail);
        setComments(rootComments.voItems ?? []);
        setCommentTotal(rootComments.voTotal ?? 0);
        setLoadedCommentPages((rootComments.voItems?.length ?? 0) > 0 ? 1 : 0);
        setQuickReplies(replyWall.voItems ?? []);
        setQuickReplyTotal(replyWall.voTotal ?? 0);
      } catch (err) {
        if (requestId !== requestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setPost(null);
        setComments([]);
        setQuickReplies([]);
        setQuickReplyTotal(0);
        setCommentTotal(0);
        setLoadedCommentPages(0);
        setError(message);
      } finally {
        if (requestId === requestIdRef.current) {
          setLoadingPost(false);
          setLoadingComments(false);
          setLoadingQuickReplies(false);
        }
      }
    };

    void loadDetail();
  }, [commentSortBy, postId, t]);

  const handleLoadMoreComments = async () => {
    if (loadingMoreComments || loadingComments || comments.length >= commentTotal) {
      return;
    }

    setLoadingMoreComments(true);
    try {
      const nextPage = loadedCommentPages + 1;
      const pageData = await getRootCommentsPage(postId, nextPage, commentPageSize, commentSortBy || 'default', t);
      const nextItems = pageData.voItems ?? [];

      setComments((current) => {
        const existingIds = new Set(current.map((item) => item.voId));
        const appended = nextItems.filter((item) => !existingIds.has(item.voId));
        return [...current, ...appended];
      });
      setCommentTotal((current) => pageData.voTotal ?? current);
      if (nextItems.length > 0) {
        setLoadedCommentPages(nextPage);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingMoreComments(false);
    }
  };

  return (
    <section className={styles.sectionCard}>
      <div className={styles.detailTopbar}>
        <button type="button" className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{t('forum.backToList')}</span>
        </button>
      </div>

      {error && <p className={styles.errorText}>{t('common.errorPrefix')}{error}</p>}

      <div className={styles.detailStack}>
        <ForumPostDetail
          post={post}
          loading={loadingPost}
          displayTimeZone={displayTimeZone}
          mode="readOnly"
          isAuthenticated={false}
        />

        {post && (
          <>
            <PostQuickReplyWall
              replies={quickReplies}
              total={quickReplyTotal}
              loading={loadingQuickReplies}
              isAuthenticated={false}
              currentUserId={0}
              mode="readOnly"
            />

            <section className={styles.commentSection}>
              <div className={styles.commentHeading}>
                <h2 className={styles.commentTitle}>{t('forum.commentTree.title')}</h2>
                <p className={styles.commentIntro}>{t('forum.quickReply.discussionSubtitle')}</p>
              </div>

              <CommentTree
                comments={comments}
                loading={loadingComments}
                loadingMoreRootComments={loadingMoreComments}
                hasPost={true}
                displayTimeZone={displayTimeZone}
                currentUserId={0}
                rootCommentTotal={commentTotal}
                loadedRootCommentCount={comments.length}
                rootCommentPageSize={commentPageSize}
                sortBy={commentSortBy}
                onSortChange={setCommentSortBy}
                onLoadMoreRootComments={handleLoadMoreComments}
              />
            </section>
          </>
        )}
      </div>
    </section>
  );
};
