import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import type {
  PublicForumBrowseRoute,
  PublicForumRoute,
} from '../forumRouteState';
import {
  getPublicDetailBackLabelKey,
  type PublicDetailBackMode,
} from '../publicRouteNavigation';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { PublicForumDetail } from './PublicForumDetail';
import { PublicForumList } from './PublicForumList';
import { PublicForumSearch } from './PublicForumSearch';
import { PublicForumTag } from './PublicForumTag';
import { PublicForumTypeFeed } from './PublicForumTypeFeed';
import {
  buildBrowseRouteKey,
} from './publicForumUtils';
import styles from './PublicForumApp.module.css';

interface PublicForumAppProps {
  route: PublicForumRoute;
  fallbackBrowseRoute: PublicForumBrowseRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
  onNavigateToDiscover?: () => void;
  onNavigateToProfile?: (userId: string) => void;
  onNavigateToSearch?: (keyword?: string) => void;
  onNavigateToTag?: (tagSlug: string) => void;
  onNavigateToQuestion?: () => void;
  onNavigateToPoll?: () => void;
  onNavigateToLottery?: () => void;
}

export const PublicForumApp = ({
  route,
  fallbackBrowseRoute,
  detailBackAction,
  onNavigate,
  onNavigateToDiscover,
  onNavigateToProfile,
  onNavigateToSearch,
  onNavigateToTag,
  onNavigateToQuestion,
  onNavigateToPoll,
  onNavigateToLottery
}: PublicForumAppProps) => {
  const { t } = useTranslation();
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const pageRef = useRef<HTMLDivElement>(null);
  const previousRouteRef = useRef<PublicForumRoute>(route);
  const browseScrollSnapshotRef = useRef<{ routeKey: string; scrollTop: number } | null>(null);
  const [pendingRestoreScrollTop, setPendingRestoreScrollTop] = useState<number | null>(null);
  const routeBrowseKey = useMemo(() => (
    route.kind !== 'detail' ? buildBrowseRouteKey(route) : null
  ), [route]);
  const detailBackLabelKey = getPublicDetailBackLabelKey(detailBackAction?.mode);
  const detailBackLabel = detailBackLabelKey ? t(detailBackLabelKey) : t('forum.backToList');
  const handleForumDetailBack = detailBackAction?.onBack ?? (() => onNavigate(fallbackBrowseRoute));

  useEffect(() => {
    const titleKey = route.kind === 'detail'
      ? 'forum.postDetail.title'
      : route.kind === 'search'
        ? 'forum.public.searchTitle'
        : route.kind === 'tag'
          ? 'forum.public.tagTitle'
          : route.kind === 'question'
            ? 'forum.public.questionTitle'
            : route.kind === 'poll'
              ? 'forum.public.pollTitle'
              : route.kind === 'lottery'
                ? 'forum.public.lotteryTitle'
                : 'forum.allPosts';
    const nextTitle = `${t('desktop.apps.forum.name')} · ${t(titleKey)}`;

    document.title = nextTitle;
  }, [route.kind, t]);

  useEffect(() => {
    const page = pageRef.current;
    const previousRoute = previousRouteRef.current;

    if (!page) {
      previousRouteRef.current = route;
      return;
    }

    if (previousRoute.kind !== 'detail' && route.kind === 'detail') {
      browseScrollSnapshotRef.current = {
        routeKey: buildBrowseRouteKey(previousRoute),
        scrollTop: page.scrollTop
      };
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
    } else if (route.kind !== 'detail') {
      if (routeBrowseKey && browseScrollSnapshotRef.current?.routeKey === routeBrowseKey) {
        setPendingRestoreScrollTop(browseScrollSnapshotRef.current.scrollTop);
      } else {
        setPendingRestoreScrollTop(null);
        page.scrollTo({ top: 0, behavior: 'auto' });
      }
    } else {
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
    }

    previousRouteRef.current = route;
  }, [route, routeBrowseKey]);

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="论"
        brandName={t('desktop.apps.forum.name')}
        brandSubline="Public Content Shell"
        onBrandClick={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
        onNavigateToDiscover={onNavigateToDiscover}
        discoverLabel={t('public.shell.discoverAction')}
      />

      <main className={styles.main}>
        {route.kind === 'detail' ? (
          <PublicForumDetail
            key={`detail-${route.postId}-${route.commentId ?? 'none'}`}
            postId={route.postId}
            commentId={route.commentId}
            displayTimeZone={displayTimeZone}
            backLabel={detailBackLabel}
            onBack={handleForumDetailBack}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'search' ? (
          <PublicForumSearch
            key="search"
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'tag' ? (
          <PublicForumTag
            key={`tag-${route.tagSlug}`}
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'question' || route.kind === 'poll' || route.kind === 'lottery' ? (
          <PublicForumTypeFeed
            key={`${route.kind}-${route.sortBy}-${route.page}`}
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'list' ? (
          <PublicForumList
            key="list"
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : null}
      </main>
    </div>
  );
};
