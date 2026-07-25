import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  ContentModerationAppealStatus,
  ContentModerationAppealVo,
  ContentModerationDecisionNoticeVo,
  ContentModerationReliefScope,
  ContentModerationTargetActionVo,
} from '@radish/http';
import { Icon } from '@radish/ui/icon';
import {
  getMyAppealableDecisions,
  getMyContentModerationAppeal,
  getMyContentModerationAppeals,
  submitContentModerationAppeal,
  withdrawContentModerationAppeal,
} from '@/api/contentModeration';
import { log } from '@/utils/logger';
import type { MeAppealsRoute } from './meRouteState';
import styles from './MeAppealsPage.module.css';

interface MeAppealsPageProps {
  route: MeAppealsRoute;
  onNavigate: (route: MeAppealsRoute) => void;
  onBack: () => void;
}

type AppealView = 'decisions' | 'appeals';

const PAGE_SIZE = 20;

const createOperationKey = (scope: string) => `${scope}:${crypto.randomUUID()}`;

function isWithdrawable(status: ContentModerationAppealStatus): boolean {
  return status === 'Submitted' || status === 'Reviewing';
}

function isDecisionEligible(decision: ContentModerationDecisionNoticeVo): boolean {
  return decision.voCanAppeal;
}

function decisionStateKey(decision: ContentModerationDecisionNoticeVo): string {
  return decision.voCanAppeal
    ? 'Appealable'
    : decision.voIneligibleReason ?? 'NotEligible';
}

function scopeKeys(scope: ContentModerationReliefScope): string[] {
  const result: string[] = [];
  if ((scope & 1) === 1) {
    result.push('target');
  }
  if ((scope & 2) === 2) {
    result.push('mute');
  }
  if ((scope & 4) === 4) {
    result.push('ban');
  }
  return result;
}

function actionTone(status: ContentModerationTargetActionVo['voStatus']): string {
  if (status === 'Succeeded') {
    return 'success';
  }
  if (status === 'Failed') {
    return 'danger';
  }
  if (status === 'Pending') {
    return 'warning';
  }
  return 'neutral';
}

export function MeAppealsPage({ route, onNavigate, onBack }: MeAppealsPageProps) {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [view, setView] = useState<AppealView>(route.appealPublicId ? 'appeals' : 'decisions');
  const [decisions, setDecisions] = useState<ContentModerationDecisionNoticeVo[]>([]);
  const [appeals, setAppeals] = useState<ContentModerationAppealVo[]>([]);
  const [decisionTotal, setDecisionTotal] = useState(0);
  const [appealTotal, setAppealTotal] = useState(0);
  const [selectedAppeal, setSelectedAppeal] = useState<ContentModerationAppealVo | null>(null);
  const [statement, setStatement] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedDecision = useMemo(() => {
    const casePublicId = view === 'appeals'
      ? selectedAppeal?.voCasePublicId ?? route.casePublicId
      : route.casePublicId ?? selectedAppeal?.voCasePublicId;
    if (casePublicId) {
      return decisions.find((item) => item.voCasePublicId === casePublicId) ?? null;
    }
    if (route.appealPublicId) {
      return null;
    }
    return decisions[0] ?? null;
  }, [decisions, route.appealPublicId, route.casePublicId, selectedAppeal?.voCasePublicId, view]);

  const formatDateTime = useCallback((value?: string | null) => {
    if (!value) {
      return '-';
    }
    return new Intl.DateTimeFormat(language.startsWith('zh') ? 'zh-CN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }, [language]);

  const loadPage = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [decisionPage, appealPage] = await Promise.all([
        getMyAppealableDecisions(route.page, PAGE_SIZE, t('me.appeals.loadFailed')),
        getMyContentModerationAppeals(route.page, PAGE_SIZE, t('me.appeals.loadFailed')),
      ]);
      setDecisions(decisionPage.voItems);
      setDecisionTotal(decisionPage.voTotal);
      setAppeals(appealPage.voItems);
      setAppealTotal(appealPage.voTotal);
    } catch (loadError) {
      log.error('MeAppealsPage', 'Failed to load moderation decisions and appeals:', loadError);
      setError(loadError instanceof Error ? loadError.message : t('me.appeals.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [route.page, t]);

  useEffect(() => {
    void loadPage();
  }, [loadPage]);

  useEffect(() => {
    const appealPublicId = route.appealPublicId
      ?? selectedDecision?.voAppealPublicId
      ?? (view === 'appeals' ? appeals[0]?.voAppealPublicId : undefined);
    if (!appealPublicId) {
      setSelectedAppeal(null);
      return;
    }

    let cancelled = false;
    setLoadingDetail(true);
    getMyContentModerationAppeal(appealPublicId, t('me.appeals.loadDetailFailed'))
      .then((detail) => {
        if (!cancelled) {
          setSelectedAppeal(detail);
        }
      })
      .catch((loadError) => {
        if (!cancelled) {
          log.error('MeAppealsPage', 'Failed to load moderation appeal:', loadError);
          setError(loadError instanceof Error ? loadError.message : t('me.appeals.loadDetailFailed'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingDetail(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [appeals, route.appealPublicId, selectedDecision?.voAppealPublicId, t, view]);

  useEffect(() => {
    if (!statement.trim()) {
      return undefined;
    }

    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', warnBeforeUnload);
    return () => {
      window.removeEventListener('beforeunload', warnBeforeUnload);
    };
  }, [statement]);

  const selectDecision = (decision: ContentModerationDecisionNoticeVo) => {
    setView('decisions');
    setSelectedAppeal(null);
    setStatement('');
    onNavigate({
      kind: 'appeals',
      page: route.page,
      casePublicId: decision.voCasePublicId,
      ...(decision.voAppealPublicId ? { appealPublicId: decision.voAppealPublicId } : {}),
    });
  };

  const selectAppeal = (appeal: ContentModerationAppealVo) => {
    setView('appeals');
    setSelectedAppeal(appeal);
    setStatement('');
    onNavigate({
      kind: 'appeals',
      page: route.page,
      casePublicId: appeal.voCasePublicId,
      appealPublicId: appeal.voAppealPublicId,
    });
  };

  const submitAppeal = async () => {
    if (!selectedDecision || !isDecisionEligible(selectedDecision) || statement.trim().length < 20) {
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const appeal = await submitContentModerationAppeal({
        casePublicId: selectedDecision.voCasePublicId,
        statement: statement.trim(),
        operationKey: createOperationKey('moderation-appeal-submit'),
      }, t('me.appeals.submitFailed'));
      setStatement('');
      setSelectedAppeal(appeal);
      setView('appeals');
      await loadPage();
      onNavigate({
        kind: 'appeals',
        page: route.page,
        casePublicId: appeal.voCasePublicId,
        appealPublicId: appeal.voAppealPublicId,
      });
    } catch (submitError) {
      log.error('MeAppealsPage', 'Failed to submit moderation appeal:', submitError);
      setError(submitError instanceof Error ? submitError.message : t('me.appeals.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const withdrawAppeal = async () => {
    if (
      !selectedAppeal
      || !isWithdrawable(selectedAppeal.voStatus)
      || !window.confirm(t('me.appeals.withdrawConfirm'))
    ) {
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const appeal = await withdrawContentModerationAppeal({
        appealPublicId: selectedAppeal.voAppealPublicId,
        expectedVersion: selectedAppeal.voVersion,
        operationKey: createOperationKey('moderation-appeal-withdraw'),
      }, t('me.appeals.withdrawFailed'));
      setSelectedAppeal(appeal);
      await loadPage();
    } catch (withdrawError) {
      log.error('MeAppealsPage', 'Failed to withdraw moderation appeal:', withdrawError);
      setError(withdrawError instanceof Error ? withdrawError.message : t('me.appeals.withdrawFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const pageTotal = view === 'decisions' ? decisionTotal : appealTotal;
  const totalPages = Math.max(1, Math.ceil(pageTotal / PAGE_SIZE));
  const detailCasePublicId = selectedDecision?.voCasePublicId ?? selectedAppeal?.voCasePublicId;
  const detailEligibleScope = selectedDecision?.voEligibleScope ?? selectedAppeal?.voEligibleScope ?? 0;
  const detailEligibleUntil = selectedDecision?.voEligibleUntilUtc ?? selectedAppeal?.voEligibleUntilUtc;
  const detailResultCode = selectedDecision?.voPublicResultCode ?? selectedAppeal?.voPublicResultCode;
  const detailTargetType = selectedDecision?.voTargetType ?? selectedAppeal?.voTargetType;
  const detailTargetTitle = selectedDecision?.voTargetSnapshotTitle ?? selectedAppeal?.voTargetSnapshotTitle;
  const detailTargetSummary = selectedDecision?.voTargetSnapshotSummary ?? selectedAppeal?.voTargetSnapshotSummary;

  return (
    <section className={styles.page}>
      <header className={styles.hero}>
        <div>
          <p className={styles.kicker}>/me/appeals · {t('me.appeals.private')}</p>
          <h1>{t('me.appeals.title')}</h1>
          <p className={styles.description}>{t('me.appeals.description')}</p>
        </div>
        <button type="button" className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          {t('me.appeals.back')}
        </button>
      </header>

      <div className={styles.metrics} aria-label={t('me.appeals.metricsLabel')}>
        <article><strong>{decisionTotal}</strong><span>{t('me.appeals.metric.decisions')}</span></article>
        <article>
          <strong>{appeals.filter((item) => ['Submitted', 'Reviewing', 'ReliefPending', 'ReliefFailed'].includes(item.voStatus)).length}</strong>
          <span>{t('me.appeals.metric.active')}</span>
        </article>
        <article>
          <strong>{appeals.filter((item) => item.voStatus === 'Resolved').length}</strong>
          <span>{t('me.appeals.metric.resolved')}</span>
        </article>
      </div>

      <nav className={styles.viewTabs} aria-label={t('me.appeals.viewTabsLabel')}>
        <button type="button" data-active={view === 'decisions'} onClick={() => setView('decisions')}>
          {t('me.appeals.view.decisions')}
        </button>
        <button type="button" data-active={view === 'appeals'} onClick={() => setView('appeals')}>
          {t('me.appeals.view.appeals')}
        </button>
      </nav>

      {error ? <div className={styles.errorPanel} role="alert"><Icon icon="mdi:alert-circle-outline" size={22} />{error}</div> : null}
      {loading ? <div className={styles.statusPanel}><Icon icon="mdi:progress-clock" size={26} />{t('me.appeals.loading')}</div> : null}

      {!loading ? (
        <div className={styles.workspace}>
          <div className={styles.list} aria-label={t(`me.appeals.view.${view}`)}>
            {(view === 'decisions' ? decisions : appeals).length === 0 ? (
              <div className={styles.statusPanel}>
                <Icon icon="mdi:shield-search-outline" size={28} />
                <strong>{t(`me.appeals.empty.${view}`)}</strong>
              </div>
            ) : null}
            {view === 'decisions' ? decisions.map((decision) => (
              <button
                type="button"
                key={decision.voCasePublicId}
                className={styles.listCard}
                data-active={selectedDecision?.voCasePublicId === decision.voCasePublicId}
                onClick={() => selectDecision(decision)}
              >
                <span className={styles.cardHeading}>
                  <strong>{t(`me.appeals.targetType.${decision.voTargetType}`)}</strong>
                  <em>
                    {decision.voAppealStatus
                      ? t(`me.appeals.status.${decision.voAppealStatus}`)
                      : t(`me.appeals.status.${decisionStateKey(decision)}`)}
                  </em>
                </span>
                <span>{decision.voCasePublicId}</span>
                <small>{formatDateTime(decision.voResolvedAt)}</small>
              </button>
            )) : appeals.map((appeal) => (
              <button
                type="button"
                key={appeal.voAppealPublicId}
                className={styles.listCard}
                data-active={selectedAppeal?.voAppealPublicId === appeal.voAppealPublicId}
                onClick={() => selectAppeal(appeal)}
              >
                <span className={styles.cardHeading}>
                  <strong>{appeal.voAppealPublicId}</strong>
                  <em>{t(`me.appeals.status.${appeal.voStatus}`)}</em>
                </span>
                <span>{appeal.voCasePublicId}</span>
                <small>{formatDateTime(appeal.voSubmittedAt)}</small>
              </button>
            ))}
          </div>

          <article className={styles.detail} aria-busy={loadingDetail}>
            {detailCasePublicId ? (
              <>
                <div className={styles.detailHeader}>
                  <div>
                    <span>{detailCasePublicId}</span>
                    <h2>
                      {detailTargetType
                        ? t(`me.appeals.targetType.${detailTargetType}`)
                        : t('me.appeals.decisionFallback')}
                    </h2>
                  </div>
                  <strong>
                    {selectedAppeal
                      ? t(`me.appeals.status.${selectedAppeal.voStatus}`)
                      : selectedDecision && t(`me.appeals.status.${decisionStateKey(selectedDecision)}`)}
                  </strong>
                </div>

                <dl className={styles.decisionFacts}>
                  <div>
                    <dt>{t(selectedDecision ? 'me.appeals.decisionAt' : 'me.appeals.submittedAt')}</dt>
                    <dd>{formatDateTime(selectedDecision?.voResolvedAt ?? selectedAppeal?.voSubmittedAt)}</dd>
                  </div>
                  <div><dt>{t('me.appeals.deadline')}</dt><dd>{formatDateTime(detailEligibleUntil)}</dd></div>
                  <div><dt>{t('me.appeals.result')}</dt><dd>{detailResultCode ?? '-'}</dd></div>
                </dl>

                {selectedDecision || selectedAppeal ? (
                  <section className={styles.statement}>
                    <h3>{t('me.appeals.targetSummary')}</h3>
                    <strong>
                      {detailTargetTitle
                        || (detailTargetType ? t(`me.appeals.targetType.${detailTargetType}`) : detailCasePublicId)}
                    </strong>
                    <p>{detailTargetSummary || t('me.appeals.targetSummaryUnavailable')}</p>
                  </section>
                ) : null}

                <section className={styles.scopeSection}>
                  <h3>{t('me.appeals.eligibleScope')}</h3>
                  <div>{scopeKeys(detailEligibleScope).map((scope) => (
                    <span key={scope}>{t(`me.appeals.scope.${scope}`)}</span>
                  ))}</div>
                </section>

                {selectedAppeal ? (
                  <>
                    <section className={styles.statement}>
                      <h3>{t('me.appeals.statement')}</h3>
                      <p>{selectedAppeal.voStatement}</p>
                    </section>
                    {selectedAppeal.voPublicResultSummary ? (
                      <section className={styles.resultSummary}>
                        <h3>{t('me.appeals.publicResult')}</h3>
                        <p>{selectedAppeal.voPublicResultSummary}</p>
                      </section>
                    ) : null}
                    <section className={styles.timeline}>
                      <h3>{t('me.appeals.timeline')}</h3>
                      {selectedAppeal.voEvents.map((event) => (
                        <div key={event.voSequence}>
                          <Icon icon="mdi:circle-medium" size={18} />
                          <span>{t(`me.appeals.event.${event.voEventType}`, { defaultValue: event.voEventType })}</span>
                          <small>{formatDateTime(event.voCreateTime)}</small>
                        </div>
                      ))}
                    </section>
                    {selectedAppeal.voTargetActions.length > 0 ? (
                      <section className={styles.actions}>
                        <h3>{t('me.appeals.reliefActions')}</h3>
                        {selectedAppeal.voTargetActions.map((action, index) => (
                          <div key={`${action.voRequestedAt}-${index}`}>
                            <span>{t(`me.appeals.action.${action.voActionType}`)}</span>
                            <strong data-tone={actionTone(action.voStatus)}>
                              {t(`me.appeals.actionStatus.${action.voStatus}`)}
                            </strong>
                          </div>
                        ))}
                      </section>
                    ) : null}
                    {selectedAppeal.voUserActionSummaries.length > 0 ? (
                      <section className={styles.actions}>
                        <h3>{t('me.appeals.userReliefActions')}</h3>
                        {selectedAppeal.voUserActionSummaries.map((action, index) => (
                          <div key={`${action.voCreateTime}-${index}`}>
                            <span>{t(`me.appeals.userAction.${action.voActionType}`)}</span>
                            <strong>
                              {t(`me.appeals.userActionResult.${action.voResultCode ?? 'Recorded'}`, {
                                defaultValue: action.voResultCode ?? t('me.appeals.userActionResult.Recorded'),
                              })}
                            </strong>
                          </div>
                        ))}
                      </section>
                    ) : null}
                    {isWithdrawable(selectedAppeal.voStatus) ? (
                      <button type="button" className={styles.withdrawButton} disabled={submitting} onClick={() => void withdrawAppeal()}>
                        {t('me.appeals.withdraw')}
                      </button>
                    ) : null}
                  </>
                ) : selectedDecision && isDecisionEligible(selectedDecision) ? (
                  <section className={styles.form}>
                    <label htmlFor="appeal-statement">{t('me.appeals.statement')}</label>
                    <textarea
                      id="appeal-statement"
                      value={statement}
                      maxLength={1000}
                      onChange={(event) => setStatement(event.target.value)}
                      placeholder={t('me.appeals.statementPlaceholder')}
                    />
                    <small>{t('me.appeals.statementCount', { count: statement.trim().length })}</small>
                    <button
                      type="button"
                      disabled={submitting || statement.trim().length < 20}
                      onClick={() => void submitAppeal()}
                    >
                      {submitting ? t('me.appeals.submitting') : t('me.appeals.submit')}
                    </button>
                  </section>
                ) : (
                  <section className={styles.resultSummary}>
                    <h3>{t(`me.appeals.status.${selectedDecision ? decisionStateKey(selectedDecision) : 'NotEligible'}`)}</h3>
                    <p>
                      {t(`me.appeals.ineligibleDescription.${selectedDecision?.voIneligibleReason ?? 'NotEligible'}`)}
                    </p>
                  </section>
                )}

                <div className={styles.detailActions}>
                  <span><Icon icon="mdi:shield-lock-outline" size={17} />{t('me.appeals.privacy')}</span>
                </div>
              </>
            ) : <div className={styles.statusPanel}>{t('me.appeals.noSelection')}</div>}
          </article>
        </div>
      ) : null}

      <footer className={styles.pagination}>
        <button
          type="button"
          disabled={route.page <= 1}
          onClick={() => onNavigate({ kind: 'appeals', page: route.page - 1 })}
        >
          {t('me.appeals.previous')}
        </button>
        <span>{t('me.appeals.pageInfo', { current: route.page, total: totalPages })}</span>
        <button
          type="button"
          disabled={route.page >= totalPages}
          onClick={() => onNavigate({ kind: 'appeals', page: route.page + 1 })}
        >
          {t('me.appeals.next')}
        </button>
      </footer>
    </section>
  );
}
