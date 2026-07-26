import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { ContentReportReceiptVo } from '@radish/http';
import { getMyContentReports } from '@/api/contentModeration';
import { buildMessagesPath } from '@/messages/messagesRouteState';
import { log } from '@/utils/logger';
import styles from './MeReportsPage.module.css';

type ReportFilter = 'all' | 'submitted' | 'resolved';

interface MeReportsPageProps {
  page: number;
  onNavigate: (page: number) => void;
  onBack: () => void;
}

const PAGE_SIZE = 20;

function buildReportTargetHref(report: ContentReportReceiptVo): string | null {
  if (report.voTargetNavigationStatus === 'Unavailable' || report.voTargetNavigationStatus === 'Unsupported') {
    return null;
  }

  if (report.voTargetType === 'Post') {
    return `/forum/post/${encodeURIComponent(report.voTargetPostId ?? report.voTargetContentId)}`;
  }

  if (report.voTargetType === 'Comment' && report.voTargetPostId && report.voTargetCommentId) {
    const query = new URLSearchParams({ commentId: report.voTargetCommentId });
    return `/forum/post/${encodeURIComponent(report.voTargetPostId)}?${query.toString()}`;
  }

  if (report.voTargetType === 'PostQuickReply' && report.voTargetPostId) {
    return `/forum/post/${encodeURIComponent(report.voTargetPostId)}`;
  }

  if (report.voTargetType === 'ChatMessage' && report.voTargetChannelId) {
    return buildMessagesPath({
      channelId: report.voTargetChannelId,
      ...(report.voTargetMessageId ? { messageId: report.voTargetMessageId } : {}),
    });
  }

  if (report.voTargetType === 'Product') {
    return `/shop/product/${encodeURIComponent(report.voTargetContentId)}`;
  }

  return null;
}

export function MeReportsPage({ page, onNavigate, onBack }: MeReportsPageProps) {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [reports, setReports] = useState<ContentReportReceiptVo[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<ReportFilter>('all');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getMyContentReports(page, PAGE_SIZE, t('me.reports.loadFailed'))
      .then((result) => {
        if (cancelled) {
          return;
        }
        setReports(result.voItems);
        setTotal(result.voTotal);
      })
      .catch((loadError) => {
        if (cancelled) {
          return;
        }
        log.error('MeReportsPage', 'Failed to load my content reports:', loadError);
        setError(loadError instanceof Error ? loadError.message : t('me.reports.loadFailed'));
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [page, t]);

  const filteredReports = useMemo(() => reports.filter((report) => {
    if (filter === 'submitted') {
      return report.voReporterState === 'Submitted';
    }
    if (filter === 'resolved') {
      return report.voReporterState === 'Resolved';
    }
    return true;
  }), [filter, reports]);

  const submittedCount = reports.filter((report) => report.voReporterState === 'Submitted').length;
  const resolvedCount = reports.filter((report) => report.voReporterState === 'Resolved').length;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const formatDateTime = (value?: string | null) => {
    if (!value) {
      return '-';
    }
    return new Intl.DateTimeFormat(language.startsWith('zh') ? 'zh-CN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  };

  return (
    <section className={styles.page}>
      <header className={styles.hero}>
        <div>
          <p className={styles.kicker}>/me/reports · {t('me.reports.private')}</p>
          <h1>{t('me.reports.title')}</h1>
          <p className={styles.description}>{t('me.reports.description')}</p>
        </div>
        <button type="button" className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          {t('me.reports.back')}
        </button>
      </header>

      <div className={styles.metrics} aria-label={t('me.reports.metricsLabel')}>
        <article>
          <strong>{total}</strong>
          <span>{t('me.reports.metric.total')}</span>
        </article>
        <article>
          <strong>{submittedCount}</strong>
          <span>{t('me.reports.metric.submitted')}</span>
        </article>
        <article>
          <strong>{resolvedCount}</strong>
          <span>{t('me.reports.metric.resolved')}</span>
        </article>
      </div>

      <nav className={styles.filters} aria-label={t('me.reports.filtersLabel')}>
        {(['all', 'submitted', 'resolved'] as const).map((value) => (
          <button
            key={value}
            type="button"
            data-active={filter === value}
            onClick={() => setFilter(value)}
          >
            {t(`me.reports.filter.${value}`)}
          </button>
        ))}
      </nav>

      {loading ? (
        <div className={styles.statusPanel}>
          <Icon icon="mdi:progress-clock" size={28} />
          <strong>{t('me.reports.loading')}</strong>
        </div>
      ) : null}

      {error ? (
        <div className={styles.errorPanel} role="alert">
          <Icon icon="mdi:alert-circle-outline" size={24} />
          <span>{error}</span>
        </div>
      ) : null}

      {!loading && !error && filteredReports.length === 0 ? (
        <div className={styles.statusPanel}>
          <Icon icon="mdi:shield-search-outline" size={28} />
          <strong>{t('me.reports.empty')}</strong>
          <span>{t('me.reports.emptyDescription')}</span>
        </div>
      ) : null}

      <div className={styles.list}>
        {filteredReports.map((report) => {
          const href = buildReportTargetHref(report);
          const isResolved = report.voReporterState === 'Resolved';
          const resultKey = isResolved
            ? report.voPublicResultCode || 'Resolved'
            : 'Submitted';
          return (
            <article key={report.voReportPublicId} className={styles.card}>
              <div className={styles.cardIcon} data-resolved={isResolved}>
                <Icon icon={isResolved ? 'mdi:shield-check-outline' : 'mdi:shield-clock-outline'} size={22} />
              </div>
              <div className={styles.cardBody}>
                <div className={styles.cardTitleRow}>
                  <div>
                    <span className={styles.targetType}>{t(`me.reports.targetType.${report.voTargetType}`)}</span>
                    <h2>{report.voTargetSnapshotTitle || t('me.reports.untitledTarget')}</h2>
                  </div>
                  <span className={styles.result} data-resolved={isResolved}>
                    {t(`me.reports.result.${resultKey}`)}
                  </span>
                </div>
                {report.voTargetSnapshotSummary ? (
                  <p className={styles.summary}>{report.voTargetSnapshotSummary}</p>
                ) : null}
                <div className={styles.meta}>
                  <span>{t('me.reports.reason')}: {t(`me.reports.reasonType.${report.voReasonType}`, { defaultValue: report.voReasonType })}</span>
                  <span>{t('me.reports.submittedAt')}: {formatDateTime(report.voSubmittedAt)}</span>
                  {report.voResolvedAt ? (
                    <span>{t('me.reports.resolvedAt')}: {formatDateTime(report.voResolvedAt)}</span>
                  ) : null}
                  <span>{report.voReportPublicId}</span>
                </div>
                <div className={styles.cardActions}>
                  {href ? (
                    <a href={href}>{t('me.reports.openTarget')}</a>
                  ) : (
                    <span>{t('me.reports.targetUnavailable')}</span>
                  )}
                  <small>{t('me.reports.scopeNotice')}</small>
                </div>
              </div>
            </article>
          );
        })}
      </div>

      <footer className={styles.pagination}>
        <button type="button" disabled={page <= 1} onClick={() => onNavigate(page - 1)}>
          {t('me.reports.previous')}
        </button>
        <span>{t('me.reports.pageInfo', { current: page, total: totalPages })}</span>
        <button type="button" disabled={page >= totalPages} onClick={() => onNavigate(page + 1)}>
          {t('me.reports.next')}
        </button>
      </footer>

      <aside className={styles.boundary}>
        <Icon icon="mdi:shield-lock-outline" size={22} />
        <p>{t('me.reports.boundary')}</p>
      </aside>
    </section>
  );
}
