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
