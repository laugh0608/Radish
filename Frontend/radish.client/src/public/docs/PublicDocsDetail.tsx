import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { isApiResponseNotFoundError } from '@radish/http';
import { Icon } from '@radish/ui/icon';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import type { WikiDocumentDetailVo, WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { WikiDocumentVisibility } from '@/apps/wiki/types/wiki';
import { canUseDocsAuthorTools, isBuiltInWikiDocument } from '@/docs/docsAuthorAccess';
import { createDocsProtectedAttachmentOptions } from '@/docs/docsProtectedAttachments';
import { buildDocsAuthorPath } from '@/docs/docsAuthorRouteState';
import { useUserStore } from '@/stores/userStore';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import {
  buildPublicDocsPath,
  resolvePublicDocsRouteFromHref,
  rewritePublicDocsHref,
  type PublicDocsDetailRoute,
  type PublicDocsRoute,
} from '../docsRouteState';
import { buildLocalizedPublicRouteHead, buildPublicShareUrl } from '../publicHead';
import { usePublicHeadSnapshot } from '../publicHeadLifecycleContext';
import { isCurrentDocsHeadSource } from '../publicHeadSourceIdentity';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { getPublicWikiDocumentBySlug } from './publicDocsApi';
import { PublicDocsDetailRail } from './PublicDocsRails';
import { PublicDocsStatusCard } from './PublicDocsStatusCard';
import { toSourceText, toStatusText, toVisibilityText } from './publicDocsFormat';
import { buildPublicDocsHeadSnapshot } from './publicDocsHead';
import {
  handlePublicDocsLinkClick,
  type PublicDocsDiagnosticCopyHandler,
} from './publicDocsViewSupport';
import styles from './PublicDocsApp.module.css';

interface PublicDocsDetailProps {
  route: PublicDocsDetailRoute;
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  relatedDocuments: WikiDocumentVo[];
  getDiagnosticActionLabel: () => string;
  onCopyDiagnostics: PublicDocsDiagnosticCopyHandler;
  onBack: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
  onOpenDocument: (slug: string) => void;
}

function normalizeMarkdownHeadingText(value: string): string {
  return value.replace(/[ \t]+#+[ \t]*$/, '').trim();
}

function stripDuplicateLeadingMarkdownTitle(markdown: string, title: string): string {
  const normalizedTitle = normalizeMarkdownHeadingText(title);
  if (!markdown || !normalizedTitle) {
    return markdown;
  }

  const leadingTitleMatch = markdown.match(/^(\uFEFF?(?:[ \t]*(?:\r?\n))*[ \t]{0,3})#(?!#)[ \t]+([^\r\n]*?)(?:\r?\n|$)/);
  if (!leadingTitleMatch) {
    return markdown;
  }

  const leadingTitle = normalizeMarkdownHeadingText(leadingTitleMatch[2] ?? '');
  if (leadingTitle !== normalizedTitle) {
    return markdown;
  }

  return markdown.slice(leadingTitleMatch[0].length).replace(/^[ \t]*(?:\r?\n)/, '');
}

function getCurrentOrigin(): string {
  if (typeof window !== 'undefined' && window.location.origin) {
    return window.location.origin;
  }

  return 'https://localhost:5000';
}

export const PublicDocsDetail = ({
  route,
  displayTimeZone,
  backLabel,
  backHref,
  relatedDocuments,
  getDiagnosticActionLabel,
  onCopyDiagnostics,
  onBack,
  onNavigate,
  onOpenDocument
}: PublicDocsDetailProps) => {
  const { t } = useTranslation();
  const userId = useUserStore((state) => state.userId);
  const userRoleScope = useUserStore((state) => (state.roles || []).join(','));
  const userPermissionScope = useUserStore((state) => (state.permissions || []).join(','));
  const [documentDetail, setDocumentDetail] = useState<WikiDocumentDetailVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);
  const protectedAttachments = useMemo(
    () => createDocsProtectedAttachmentOptions(
      t,
      `reader:${userId || 'anonymous'}:${userRoleScope}:${userPermissionScope}:${route.slug}:${documentDetail?.voId ?? 'none'}:${documentDetail?.voVersion ?? 0}:${documentDetail?.voVisibility ?? 0}:${reloadToken}`,
    ),
    [
      documentDetail?.voId,
      documentDetail?.voVersion,
      documentDetail?.voVisibility,
      reloadToken,
      route.slug,
      t,
      userId,
      userPermissionScope,
      userRoleScope,
    ],
  );
  const articleBodyRef = useRef<HTMLDivElement>(null);
  const currentDocsPath = buildPublicDocsPath({
    kind: 'detail',
    slug: documentDetail?.voSlug || route.slug,
    anchor: route.anchor
  });
  const articleMarkdownContent = useMemo(
    () => stripDuplicateLeadingMarkdownTitle(
      documentDetail?.voMarkdownContent || '',
      documentDetail?.voTitle || ''
    ),
    [documentDetail?.voMarkdownContent, documentDetail?.voTitle]
  );
  const resolveArticleLinkHref = useCallback(
    (href: string) => rewritePublicDocsHref(href, getCurrentOrigin(), currentDocsPath),
    [currentDocsPath]
  );

  useEffect(() => {
    let cancelled = false;

    const loadDetail = async () => {
      setLoading(true);
      setError(null);
      setNotFound(false);

      try {
        const detail = await getPublicWikiDocumentBySlug(route.slug);
        if (!cancelled) {
          setDocumentDetail(detail);
        }
      } catch (err) {
        if (!cancelled) {
          setDocumentDetail(null);
          setNotFound(isApiResponseNotFoundError(err));
          setError(err instanceof Error ? err.message : String(err));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadDetail();
    return () => {
      cancelled = true;
    };
  }, [reloadToken, route.slug]);

  const publicHeadSnapshot = useMemo(() => {
    if (
      !documentDetail
      || documentDetail.voVisibility !== WikiDocumentVisibility.Public
      || !isCurrentDocsHeadSource(route, documentDetail)
    ) {
      return null;
    }

    const canonicalRoute: PublicDocsRoute = {
      kind: 'detail',
      slug: documentDetail.voSlug,
      anchor: route.anchor,
    };
    const routeHead = buildLocalizedPublicRouteHead({ app: 'docs', route: canonicalRoute }, t);
    return buildPublicDocsHeadSnapshot(documentDetail, route.anchor, {
      appName: t('desktop.apps.document.name'),
      routeHead,
    });
  }, [documentDetail, route, t]);
  usePublicHeadSnapshot(publicHeadSnapshot);

  useEffect(() => {
    if (!documentDetail?.voSlug) {
      return;
    }

    const canonicalRoute: PublicDocsRoute = {
      kind: 'detail',
      slug: documentDetail.voSlug,
      anchor: route.anchor
    };

    if (buildPublicDocsPath(route) === buildPublicDocsPath(canonicalRoute)) {
      return;
    }

    onNavigate(canonicalRoute, { replace: true, preserveSourceState: true });
  }, [documentDetail?.voSlug, onNavigate, route]);

  useEffect(() => {
    if (!route.anchor || !documentDetail || typeof globalThis.document === 'undefined') {
      return;
    }

    const anchorTarget = globalThis.document.getElementById(route.anchor)
      ?? globalThis.document.getElementsByName(route.anchor)[0];
    if (!anchorTarget || !articleBodyRef.current?.contains(anchorTarget)) {
      return;
    }

    anchorTarget.scrollIntoView({ block: 'start' });
  }, [documentDetail, route.anchor]);

  const handleMarkdownLinkClick = (event: MouseEvent<HTMLDivElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const anchor = target.closest<HTMLAnchorElement>('a[href]');
    if (!anchor || (anchor.target && anchor.target !== '_self')) {
      return;
    }

    const href = anchor.getAttribute('href') ?? anchor.href;
    if (!href) {
      return;
    }

    const nextRoute = resolvePublicDocsRouteFromHref(href, getCurrentOrigin(), currentDocsPath);
    if (!nextRoute) {
      return;
    }

    event.preventDefault();
    onNavigate(nextRoute);
  };

  const detailState = loading
    ? 'loading'
    : documentDetail
      ? 'ready'
      : notFound
        ? 'notFound'
        : error
          ? 'error'
          : 'loading';

  const buildDocsShareUrl = useCallback(() => {
    const shareRoute: PublicDocsRoute = {
      kind: 'detail',
      slug: documentDetail?.voSlug || route.slug,
      anchor: route.anchor
    };
    return buildPublicShareUrl(buildPublicDocsPath(shareRoute));
  }, [documentDetail?.voSlug, route.anchor, route.slug]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildDocsShareUrl,
  });
  const canEditDocument = documentDetail !== null
    && canUseDocsAuthorTools(userId)
    && !documentDetail.voIsDeleted
    && !isBuiltInWikiDocument(documentDetail);
  const editHref = documentDetail
    ? buildDocsAuthorPath({ kind: 'edit', documentId: documentDetail.voId })
    : buildDocsAuthorPath({ kind: 'mine' });

  return (
    <section className={styles.sectionCard} data-public-docs-view="detail">
      <div className={styles.detailTopbar}>
        <div className={styles.detailTopbarActions}>
          <a
            className={styles.secondaryButton}
            href={backHref}
            onClick={(event) => handlePublicDocsLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{backLabel}</span>
          </a>
          <button type="button" className={styles.secondaryButton} onClick={() => void copyShareLink()} disabled={shareBusy}>
            <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
            <span>{shareBusy ? t('wiki.public.shareSubmitting') : t('wiki.public.shareAction')}</span>
          </button>
          {canEditDocument ? (
            <a className={styles.primaryButton} href={editHref}>
              <Icon icon="mdi:pencil-outline" size={18} />
              <span>{t('wiki.public.detailRailAuthorAction')}</span>
            </a>
          ) : null}
        </div>
        {shareState !== 'idle' && (
          <p className={styles.shareFeedback} data-state={shareState}>
            {shareState === 'success' ? t('wiki.public.shareSuccess') : t('wiki.public.shareFailed')}
          </p>
        )}
      </div>

      <div className={styles.contentWrap}>
        {detailState === 'loading' && (
          <PublicDocsStatusCard
            tone="loading"
            title={t('wiki.public.detailLoadingTitle')}
            description={t('wiki.public.detailLoadingDescription')}
          />
        )}

        {detailState === 'notFound' && (
          <PublicDocsStatusCard
            tone="notFound"
            title={t('wiki.public.notFoundTitle')}
            description={t('wiki.public.notFoundDescription')}
            secondaryAction={{
              label: backLabel,
              href: backHref,
              onClick: onBack
            }}
          />
        )}

        {detailState === 'error' && (
          <PublicDocsStatusCard
            tone="error"
            title={t('wiki.public.detailErrorTitle')}
            description={error || t('wiki.public.detailLoadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: backLabel,
              href: backHref,
              onClick: onBack
            }}
            diagnosticAction={{
              label: getDiagnosticActionLabel(),
              onClick: () => {
                void onCopyDiagnostics('detail-load', error);
              }
            }}
          />
        )}

        {detailState === 'ready' && documentDetail && (
          <div className={styles.docsArticleGrid}>
            <div className={styles.articleMainColumn}>
              <article className={styles.articleCard}>
                <div className={styles.articleHeader}>
                  <div className={styles.articleHeaderMain}>
                    <h1 className={styles.articleTitle}>{documentDetail.voTitle}</h1>
                    {documentDetail.voSummary?.trim() ? (
                      <p className={styles.articleSummary}>{documentDetail.voSummary}</p>
                    ) : null}
                  </div>
                </div>

                <div className={styles.articleMetaGrid}>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailAccessLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>{toVisibilityText(t, documentDetail.voVisibility)}</span>
                      <span className={styles.metaChip}>{toStatusText(t, documentDetail.voStatus)}</span>
                    </div>
                  </section>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailDocumentLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>{t('wiki.meta.slug', { value: documentDetail.voSlug })}</span>
                      <span className={styles.metaChip}>{t('wiki.meta.source', { value: toSourceText(t, documentDetail.voSourceType) })}</span>
                    </div>
                  </section>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailTimelineLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>
                        {t('wiki.meta.updated', { value: formatDateTimeByTimeZone(documentDetail.voModifyTime || documentDetail.voCreateTime, displayTimeZone) })}
                      </span>
                      <span className={styles.metaChip}>
                        {t('wiki.meta.created', { value: formatDateTimeByTimeZone(documentDetail.voCreateTime, displayTimeZone) })}
                      </span>
                    </div>
                  </section>
                  <p className={styles.articleBoundaryNote}>{t('wiki.public.detailBoundaryNote')}</p>
                </div>

                <div ref={articleBodyRef} className={styles.articleBody} onClick={handleMarkdownLinkClick}>
                  {documentDetail.voCoverAttachmentId ? (
                    <MarkdownRenderer
                      content={`![${documentDetail.voTitle}](attachment://${documentDetail.voCoverAttachmentId})`}
                      className={styles.markdownContent}
                      protectedAttachments={
                        documentDetail.voVisibility === WikiDocumentVisibility.Public
                          ? undefined
                          : protectedAttachments
                      }
                    />
                  ) : null}
                  <MarkdownRenderer
                    content={articleMarkdownContent}
                    className={styles.markdownContent}
                    resolveLinkHref={resolveArticleLinkHref}
                    protectedAttachments={
                      documentDetail.voVisibility === WikiDocumentVisibility.Public
                        ? undefined
                        : protectedAttachments
                    }
                  />
                </div>
              </article>
            </div>

            <PublicDocsDetailRail
              document={documentDetail}
              relatedDocuments={relatedDocuments}
              onOpenDocument={onOpenDocument}
            />
          </div>
        )}
      </div>
    </section>
  );
};
