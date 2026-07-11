import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  getTopCategories,
  publishPost,
  type Category,
  type CreateLotteryRequest,
  type CreatePollRequest,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { redirectToLogin } from '@/services/auth';
import { hydrateAuthUser } from '@/services/authBootstrap';
import { buildPublicForumComposeReturnPath } from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import {
  createClientSubmissionState,
  type ClientSubmissionState,
} from '@/utils/clientSubmission';
import { log } from '@/utils/logger';
import { PublishPostModal } from '@/apps/forum/components/PublishPostModal';
import { buildPostSubmissionFingerprint } from '@/apps/forum/utils/forumSubmissionFingerprint';
import {
  buildPublicForumPath,
  type PublicForumBrowseRoute,
  type PublicForumRoute,
} from '../forumRouteState';
import { PublicStatusCard } from './PublicStatusCard';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';
import styles from './PublicForumApp.module.css';

interface PublicForumComposeProps {
  categoryId?: string | null;
  fallbackBrowseRoute: PublicForumBrowseRoute;
  onBack: () => void;
  onNavigate: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
}

export function PublicForumCompose({
  categoryId,
  fallbackBrowseRoute,
  onBack,
  onNavigate
}: PublicForumComposeProps) {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const authAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const userId = useUserStore((state) => state.userId);
  const isAuthenticated = authAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const categoryRequestRef = useRef(0);
  const publishSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const publishedPostIdRef = useRef<string | null>(null);
  const browseHref = buildPublicForumPath(fallbackBrowseRoute);
  const loginReturnPath = useMemo(
    () => buildPublicForumComposeReturnPath({ categoryId }) ?? '/forum/compose',
    [categoryId]
  );
  const selectedCategoryId = useMemo<LongId | null>(() => {
    if (!categoryId) {
      return null;
    }

    return categories.find((category) => String(category.voId) === categoryId)?.voId ?? categoryId;
  }, [categories, categoryId]);
  const selectedCategoryLabel = useMemo(() => {
    if (!categoryId) {
      return t('forum.public.composeRailCategoryAll');
    }

    const selectedCategory = categories.find((category) => String(category.voId) === categoryId);
    return selectedCategory?.voName?.trim() || t('forum.public.composeRailCategoryPreset');
  }, [categories, categoryId, t]);
  const questionHref = buildPublicForumPath({ kind: 'question', sortBy: 'newest', page: 1 });
  const pollHref = buildPublicForumPath({ kind: 'poll', sortBy: 'newest', page: 1 });
  const lotteryHref = buildPublicForumPath({ kind: 'lottery', sortBy: 'newest', page: 1 });

  useEffect(() => {
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('PublicForumCompose', '公开论坛发帖页登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [apiBaseUrl]);

  const loadCategories = useCallback(async () => {
    const requestId = ++categoryRequestRef.current;
    setCategoriesLoading(true);
    setCategoriesError(null);

    try {
      const result = await getTopCategories(t);
      if (requestId !== categoryRequestRef.current) {
        return;
      }

      setCategories(result);
    } catch (error) {
      if (requestId !== categoryRequestRef.current) {
        return;
      }

      const message = error instanceof Error ? error.message : String(error);
      setCategories([]);
      setCategoriesError(message);
    } finally {
      if (requestId === categoryRequestRef.current) {
        setCategoriesLoading(false);
      }
    }
  }, [t]);

  useEffect(() => {
    if (!authReady || !isAuthenticated) {
      return;
    }

    void loadCategories();
  }, [authReady, isAuthenticated, loadCategories]);

  useEffect(() => {
    if (!authReady || isAuthenticated || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({ returnPath: loginReturnPath });
  }, [authReady, isAuthenticated, loginReturnPath, redirecting]);

  const handlePublish = useCallback(async (
    title: string,
    content: string,
    nextCategoryId: LongId,
    tagNames: string[],
    isQuestion?: boolean,
    poll?: CreatePollRequest | null,
    lottery?: CreateLotteryRequest | null
  ) => {
    const submissionState = createClientSubmissionState(
      publishSubmissionRef.current,
      'forum-post',
      buildPostSubmissionFingerprint(title, content, nextCategoryId, tagNames, isQuestion, poll, lottery)
    );
    publishSubmissionRef.current = submissionState;

    try {
      const postId = await publishPost({
        title,
        content,
        clientSubmissionId: submissionState.clientSubmissionId,
        categoryId: nextCategoryId,
        tagNames,
        isQuestion: Boolean(isQuestion),
        poll: poll ?? undefined,
        lottery: lottery ?? undefined
      }, t);

      publishSubmissionRef.current = null;
      publishedPostIdRef.current = String(postId);
      toast.success(t('forum.public.composePublished'));
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message);
      throw error;
    }
  }, [t]);

  const handleCloseComposer = useCallback(() => {
    const publishedPostId = publishedPostIdRef.current;
    if (publishedPostId) {
      publishedPostIdRef.current = null;
      onNavigate({ kind: 'detail', postId: publishedPostId }, { replace: true });
      return;
    }

    onBack();
  }, [onBack, onNavigate]);

  const canOpenComposer = authReady && isAuthenticated && !categoriesLoading && !categoriesError;

  return (
    <section className={`${styles.sectionCard} ${styles.composeSectionCard}`}>
      <header className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('forum.public.composeKicker')}</p>
          <h1 className={styles.pageTitle}>{t('forum.public.composeTitle')}</h1>
          <p className={styles.pageIntro}>{t('forum.public.composeDescription')}</p>
        </div>
      </header>

      <div className={styles.composeSummaryGrid} aria-label={t('forum.public.composeSummaryLabel')}>
        <div className={styles.composeSummaryCard}>
          <span className={styles.composeSummaryIcon}>
            <Icon icon={isAuthenticated ? 'mdi:account-check-outline' : 'mdi:account-clock-outline'} size={20} />
          </span>
          <strong>{t(isAuthenticated ? 'forum.public.composeMetricAuthReady' : 'forum.public.composeMetricAuthPending')}</strong>
          <span>{t('forum.public.composeMetricAuth')}</span>
        </div>
        <div className={styles.composeSummaryCard}>
          <span className={styles.composeSummaryIcon}>
            <Icon icon={categoriesError ? 'mdi:alert-circle-outline' : categoriesLoading ? 'mdi:progress-clock' : 'mdi:shape-outline'} size={20} />
          </span>
          <strong>{categoriesError ? t('forum.public.composeMetricCategoriesError') : categories.length}</strong>
          <span>{t('forum.public.composeMetricCategories')}</span>
        </div>
        <div className={styles.composeSummaryCard}>
          <span className={styles.composeSummaryIcon}>
            <Icon icon="mdi:fingerprint" size={20} />
          </span>
          <strong>{t('forum.public.composeMetricSubmissionReady')}</strong>
          <span>{t('forum.public.composeMetricSubmission')}</span>
        </div>
      </div>

      <div className={styles.composeWorkspace}>
        <div className={styles.composePanel}>
          <a
            className={styles.backButton}
            href={browseHref}
            onClick={(event) => handlePublicForumLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{t('forum.public.composeBackToForum')}</span>
          </a>

          {!authReady && (
            <PublicStatusCard
              tone="loading"
              title={t('forum.public.composeLoadingTitle')}
              description={t('forum.public.composeLoadingDescription')}
              compact={true}
            />
          )}

          {authReady && !isAuthenticated && (
            <PublicStatusCard
              tone="info"
              title={t('forum.public.composeLoginTitle')}
              description={t('forum.public.composeLoginDescription')}
              compact={true}
            />
          )}

          {authReady && isAuthenticated && categoriesLoading && (
            <PublicStatusCard
              tone="loading"
              title={t('forum.public.composeCategoriesLoadingTitle')}
              description={t('forum.public.composeCategoriesLoadingDescription')}
              compact={true}
            />
          )}

          {authReady && isAuthenticated && categoriesError && (
            <PublicStatusCard
              tone="error"
              title={t('forum.public.composeCategoriesErrorTitle')}
              description={categoriesError || t('forum.public.composeCategoriesErrorDescription')}
              compact={true}
              primaryAction={{
                label: t('common.retry'),
                onClick: () => void loadCategories()
              }}
              secondaryAction={{
                label: t('forum.public.composeBackToForum'),
                href: browseHref,
                onClick: onBack
              }}
            />
          )}
        </div>

        <aside className={styles.composeRail} aria-label={t('forum.public.composeRailLabel')}>
          <section className={styles.sidePanel}>
            <p className={styles.sidePanelKicker}>{t('forum.public.composeRailContextTitle')}</p>
            <p className={styles.sidePanelText}>{t('forum.public.composeRailContextDescription')}</p>
            <div className={styles.railChipList}>
              <span className={styles.railChip}>
                <Icon icon="mdi:shape-outline" size={16} />
                <span>{selectedCategoryLabel}</span>
              </span>
              <span className={styles.railChip}>
                <Icon icon={canOpenComposer ? 'mdi:check-circle-outline' : 'mdi:progress-clock'} size={16} />
                <span>{t(canOpenComposer ? 'forum.public.composeRailReady' : 'forum.public.composeRailWaiting')}</span>
              </span>
            </div>
          </section>

          <section className={styles.sidePanel}>
            <p className={styles.sidePanelKicker}>{t('forum.public.composeRailBoundaryTitle')}</p>
            <ul className={styles.sideRuleList}>
              <li>{t('forum.public.composeRailBoundaryEditor')}</li>
              <li>{t('forum.public.composeRailBoundaryCategory')}</li>
              <li>{t('forum.public.composeRailBoundarySubmission')}</li>
            </ul>
          </section>

          <section className={styles.sidePanel}>
            <p className={styles.sidePanelKicker}>{t('forum.public.composeRailRoutesTitle')}</p>
            <p className={styles.sidePanelText}>{t('forum.public.composeRailRoutesDescription')}</p>
            <div className={styles.railActionList}>
              <a
                href={browseHref}
                className={styles.railActionLink}
                onClick={(event) => handlePublicForumLinkClick(event, onBack)}
              >
                <Icon icon="mdi:format-list-bulleted" size={18} />
                <span>{t('forum.public.composeRailForumHome')}</span>
              </a>
              <a
                href={questionHref}
                className={styles.railActionLink}
                onClick={(event) => handlePublicForumLinkClick(event, () => onNavigate({ kind: 'question', sortBy: 'newest', page: 1 }))}
              >
                <Icon icon="mdi:comment-question-outline" size={18} />
                <span>{t('forum.public.composeRailQuestionFeed')}</span>
              </a>
              <a
                href={pollHref}
                className={styles.railActionLink}
                onClick={(event) => handlePublicForumLinkClick(event, () => onNavigate({ kind: 'poll', sortBy: 'newest', page: 1 }))}
              >
                <Icon icon="mdi:poll" size={18} />
                <span>{t('forum.public.composeRailPollFeed')}</span>
              </a>
              <a
                href={lotteryHref}
                className={styles.railActionLink}
                onClick={(event) => handlePublicForumLinkClick(event, () => onNavigate({ kind: 'lottery', sortBy: 'newest', page: 1 }))}
              >
                <Icon icon="mdi:gift-outline" size={18} />
                <span>{t('forum.public.composeRailLotteryFeed')}</span>
              </a>
            </div>
          </section>
        </aside>
      </div>

      <PublishPostModal
        isOpen={canOpenComposer}
        isAuthenticated={isAuthenticated}
        categories={categories}
        selectedCategoryId={selectedCategoryId}
        loginReturnPath={loginReturnPath}
        onClose={handleCloseComposer}
        onPublish={handlePublish}
      />
    </section>
  );
}
