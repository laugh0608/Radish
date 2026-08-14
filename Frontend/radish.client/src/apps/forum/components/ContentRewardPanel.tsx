import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ApiResponseError,
  ContentRewardErrorCode,
  ContentRewardReasonCodes,
  type ContentRewardReasonCode,
  type ContentRewardRecordVo,
  type ContentRewardTargetStateVo,
  type ContentRewardTargetType,
} from '@radish/http';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import { Modal } from '@radish/ui/modal';
import { toast } from '@radish/ui/toast';
import {
  createContentReward,
  getContentRewardTargetRewards,
} from '@/api/contentReward';
import { getBalance } from '@/api/coin';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import { createContentRewardIdempotencyKey } from '../utils/contentRewardState';
import styles from './ContentRewardPanel.module.css';

const MOBILE_QUERY = '(max-width: 768px)';
const DEFAULT_RECORD_PAGE_SIZE = 3;
const EXPANDED_RECORD_PAGE_SIZE = 20;

const REASON_CODES = [
  ContentRewardReasonCodes.Helpful,
  ContentRewardReasonCodes.Insightful,
  ContentRewardReasonCodes.WellWritten,
  ContentRewardReasonCodes.Detailed,
  ContentRewardReasonCodes.Warm,
] as const;

interface ContentRewardPanelProps {
  targetType: ContentRewardTargetType;
  targetId: string | number;
  state?: ContentRewardTargetStateVo;
  displayTimeZone: string;
  isAuthenticated: boolean;
  variant?: 'summary' | 'compact';
  onRequireLogin: () => void;
  onStateChange: (state: ContentRewardTargetStateVo) => void;
}

const useMobileDialog = (): boolean => {
  const [isMobile, setIsMobile] = useState(() => (
    typeof window !== 'undefined' && window.matchMedia(MOBILE_QUERY).matches
  ));

  useEffect(() => {
    const media = window.matchMedia(MOBILE_QUERY);
    const handleChange = () => setIsMobile(media.matches);
    handleChange();
    media.addEventListener('change', handleChange);
    return () => media.removeEventListener('change', handleChange);
  }, []);

  return isMobile;
};

const normalizeCount = (value: string | number): bigint => {
  const normalized = String(value).trim();
  return /^\d+$/.test(normalized) ? BigInt(normalized) : 0n;
};

export const ContentRewardPanel = ({
  targetType,
  targetId,
  state,
  displayTimeZone,
  isAuthenticated,
  variant = 'summary',
  onRequireLogin,
  onStateChange,
}: ContentRewardPanelProps) => {
  const { t, i18n } = useTranslation();
  const isMobile = useMobileDialog();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedReason, setSelectedReason] = useState<ContentRewardReasonCode>(
    ContentRewardReasonCodes.Helpful,
  );
  const [availableBalance, setAvailableBalance] = useState<string | null>(null);
  const [balanceLoading, setBalanceLoading] = useState(false);
  const [balanceError, setBalanceError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [actionErrorCode, setActionErrorCode] = useState<string | null>(null);
  const [records, setRecords] = useState<ContentRewardRecordVo[]>([]);
  const [recordsLoading, setRecordsLoading] = useState(false);
  const [recordsError, setRecordsError] = useState(false);
  const [recordsExpanded, setRecordsExpanded] = useState(false);
  const [recordPageIndex, setRecordPageIndex] = useState(1);
  const operationKeyRef = useRef<string | null>(null);
  const recordsRequestRef = useRef(0);
  const totalCount = normalizeCount(state?.voTotalCount ?? 0);
  const locale = i18n.resolvedLanguage?.startsWith('zh') ? 'zh-CN' : 'en-US';
  const totalCountDisplay = new Intl.NumberFormat(locale).format(totalCount);

  const reasonLabel = useCallback((reasonCode: ContentRewardReasonCode): string => (
    t(`forum.contentReward.reason.${reasonCode}`)
  ), [t]);

  const actionError = useMemo(() => {
    switch (actionErrorCode) {
      case ContentRewardErrorCode.InsufficientBalance:
        return t('forum.contentReward.error.insufficientBalance');
      case ContentRewardErrorCode.AlreadyRewarded:
        return t('forum.contentReward.error.alreadyRewarded');
      case ContentRewardErrorCode.Processing:
        return t('forum.contentReward.error.processing');
      case ContentRewardErrorCode.IdempotencyConflict:
        return t('forum.contentReward.error.idempotencyConflict');
      case ContentRewardErrorCode.ReplayUnavailable:
        return t('forum.contentReward.error.replayUnavailable');
      case ContentRewardErrorCode.DailyLimitExceeded:
        return t('forum.contentReward.error.dailyLimit');
      case ContentRewardErrorCode.SelfNotAllowed:
        return t('forum.contentReward.error.selfNotAllowed');
      case ContentRewardErrorCode.TargetUnavailable:
        return t('forum.contentReward.error.targetUnavailable');
      case ContentRewardErrorCode.AccountUnavailable:
        return t('forum.contentReward.error.accountUnavailable');
      case ContentRewardErrorCode.InteractionUnavailable:
        return t('forum.contentReward.error.interactionUnavailable');
      case ContentRewardErrorCode.RelationshipTemporarilyUnavailable:
      case ContentRewardErrorCode.ConcurrentConflict:
        return t('forum.contentReward.error.retryable');
      default:
        return actionErrorCode ? t('forum.contentReward.error.generic') : null;
    }
  }, [actionErrorCode, t]);

  const loadRecords = useCallback(async (pageIndex: number, expanded: boolean) => {
    const requestId = ++recordsRequestRef.current;
    setRecordsLoading(true);
    setRecordsError(false);
    try {
      const page = await getContentRewardTargetRewards(
        { targetType, targetId },
        pageIndex,
        expanded ? EXPANDED_RECORD_PAGE_SIZE : DEFAULT_RECORD_PAGE_SIZE,
        t,
      );
      if (requestId !== recordsRequestRef.current) {
        return;
      }

      setRecords(page.voItems);
      setRecordPageIndex(page.voPageIndex);
      onStateChange({
        voTargetType: page.voTargetType,
        voTargetId: page.voTargetId,
        voTotalCount: page.voTotalCount,
        voViewerRewarded: page.voViewerRewarded,
        voCreateEnabled: page.voCreateEnabled,
      });
    } catch (error) {
      if (requestId !== recordsRequestRef.current) {
        return;
      }
      setRecordsError(true);
      log.error('ContentRewardPanel', '加载内容赞赏公开记录失败', error);
    } finally {
      if (requestId === recordsRequestRef.current) {
        setRecordsLoading(false);
      }
    }
  }, [onStateChange, t, targetId, targetType]);

  useEffect(() => {
    setRecords([]);
    setRecordsExpanded(false);
    setRecordPageIndex(1);
    setRecordsError(false);
    recordsRequestRef.current += 1;
    operationKeyRef.current = null;
    setDialogOpen(false);
    setAvailableBalance(null);
    setBalanceError(false);

    if (variant === 'summary' && totalCount > 0n) {
      void loadRecords(1, false);
    }
  }, [loadRecords, targetId, targetType, totalCount, variant]);

  const loadAvailableBalance = useCallback(async () => {
    setBalanceLoading(true);
    setBalanceError(false);
    try {
      const balance = await getBalance(t);
      setAvailableBalance(String(balance.voBalance));
    } catch (error) {
      setBalanceError(true);
      setAvailableBalance(null);
      log.error('ContentRewardPanel', '加载内容赞赏可用余额失败', error);
    } finally {
      setBalanceLoading(false);
    }
  }, [t]);

  const handleOpenDialog = () => {
    if (!state?.voCreateEnabled || state.voViewerRewarded) {
      return;
    }
    if (!isAuthenticated) {
      onRequireLogin();
      return;
    }

    setSelectedReason(ContentRewardReasonCodes.Helpful);
    setActionErrorCode(null);
    setDialogOpen(true);
    void loadAvailableBalance();
  };

  const handleCloseDialog = () => {
    if (!submitting) {
      setDialogOpen(false);
      setActionErrorCode(null);
    }
  };

  const handleSubmit = async () => {
    if (!state?.voCreateEnabled || submitting || balanceLoading || balanceError) {
      return;
    }

    const idempotencyKey = operationKeyRef.current ?? createContentRewardIdempotencyKey();
    operationKeyRef.current = idempotencyKey;
    setSubmitting(true);
    setActionErrorCode(null);
    try {
      const mutation = await createContentReward({
        targetType,
        targetId,
        reasonCode: selectedReason,
        idempotencyKey,
      }, t);
      operationKeyRef.current = null;
      setAvailableBalance(String(mutation.voSenderAvailableBalance));
      onStateChange({
        voTargetType: mutation.voTargetType,
        voTargetId: mutation.voTargetId,
        voTotalCount: mutation.voTotalCount,
        voViewerRewarded: mutation.voViewerRewarded,
        voCreateEnabled: state.voCreateEnabled,
      });
      setDialogOpen(false);
      toast.success(t('forum.contentReward.success'));
      if (variant === 'compact' && recordsExpanded) {
        void loadRecords(1, true);
      }
    } catch (error) {
      const code = error instanceof ApiResponseError ? error.code ?? null : null;
      const keepOperationKey = code === ContentRewardErrorCode.Processing
        || code === ContentRewardErrorCode.ConcurrentConflict
        || code === ContentRewardErrorCode.RelationshipTemporarilyUnavailable
        || !(error instanceof ApiResponseError);
      if (!keepOperationKey) {
        operationKeyRef.current = null;
      }
      if (code === ContentRewardErrorCode.AlreadyRewarded) {
        onStateChange({ ...state, voViewerRewarded: true });
      }
      if (code === ContentRewardErrorCode.Unavailable) {
        onStateChange({ ...state, voCreateEnabled: false });
      }
      setActionErrorCode(code ?? 'ContentReward.Unknown');
      log.error('ContentRewardPanel', '提交内容赞赏失败', error);
    } finally {
      setSubmitting(false);
    }
  };

  const toggleRecords = () => {
    const nextExpanded = !recordsExpanded;
    setRecordsExpanded(nextExpanded);
    if (nextExpanded) {
      void loadRecords(1, true);
    } else if (variant === 'summary') {
      void loadRecords(1, false);
    }
  };

  if (!state || (!state.voCreateEnabled && totalCount === 0n)) {
    return null;
  }

  const canGoPrevious = recordsExpanded && recordPageIndex > 1;
  const canGoNext = recordsExpanded
    && BigInt(recordPageIndex * EXPANDED_RECORD_PAGE_SIZE) < totalCount;
  const dialogFooter = (
    <div className={styles.dialogActions}>
      <button
        type="button"
        className={styles.secondaryButton}
        onClick={handleCloseDialog}
        disabled={submitting}
      >
        {t('common.cancel')}
      </button>
      <button
        type="button"
        className={styles.primaryButton}
        onClick={() => void handleSubmit()}
        disabled={submitting || balanceLoading || balanceError}
      >
        <Icon icon={submitting ? 'mdi:progress-clock' : 'mdi:sprout'} size={17} />
        {submitting
          ? t('forum.contentReward.submitting')
          : t('forum.contentReward.confirm')}
      </button>
    </div>
  );
  const dialogBody = (
    <div className={styles.dialogBody}>
      <div className={styles.costCard}>
        <div>
          <span className={styles.costLabel}>{t('forum.contentReward.costLabel')}</span>
          <strong className={styles.costValue}>{t('forum.contentReward.amount')}</strong>
        </div>
        <div className={styles.balanceBlock}>
          <span>{t('forum.contentReward.balanceLabel')}</span>
          <strong>
            {balanceLoading
              ? t('common.loading')
              : availableBalance ?? '—'}
          </strong>
        </div>
      </div>

      {balanceError && (
        <div className={styles.inlineError} role="alert">
          <span>{t('forum.contentReward.error.balance')}</span>
          <button type="button" onClick={() => void loadAvailableBalance()}>
            {t('common.retry')}
          </button>
        </div>
      )}

      <fieldset className={styles.reasonFieldset} disabled={submitting}>
        <legend>{t('forum.contentReward.reasonLabel')}</legend>
        <div className={styles.reasonGrid}>
          {REASON_CODES.map((reasonCode) => (
            <label
              key={reasonCode}
              className={`${styles.reasonOption} ${selectedReason === reasonCode ? styles.reasonOptionSelected : ''}`}
            >
              <input
                type="radio"
                name={`content-reward-reason-${targetType}-${String(targetId)}`}
                value={reasonCode}
                checked={selectedReason === reasonCode}
                onChange={() => setSelectedReason(reasonCode)}
              />
              <span>{reasonLabel(reasonCode)}</span>
            </label>
          ))}
        </div>
      </fieldset>

      {actionError && (
        <div className={styles.inlineError} role="alert">
          <span>{actionError}</span>
          {actionErrorCode === ContentRewardErrorCode.InsufficientBalance && (
            <a href="/me/assets">{t('forum.contentReward.viewAssets')}</a>
          )}
        </div>
      )}
    </div>
  );

  return (
    <section
      className={`${styles.panel} ${variant === 'compact' ? styles.panelCompact : ''}`}
      aria-label={t('forum.contentReward.title')}
    >
      <div className={styles.summaryRow}>
        <div className={styles.summaryCopy}>
          <strong>{t('forum.contentReward.summary', { value: totalCountDisplay })}</strong>
          {variant === 'summary' && (
            <span>{t('forum.contentReward.description')}</span>
          )}
        </div>
        <div className={styles.summaryActions}>
          {totalCount > 0n && (
            <button
              type="button"
              className={styles.recordsButton}
              onClick={toggleRecords}
              aria-expanded={recordsExpanded}
            >
              {recordsExpanded
                ? t('forum.contentReward.collapseRecords')
                : t('forum.contentReward.viewRecords')}
            </button>
          )}
          {state.voCreateEnabled && (
            <button
              type="button"
              className={`${styles.rewardButton} ${state.voViewerRewarded ? styles.rewardButtonDone : ''}`}
              onClick={handleOpenDialog}
              disabled={state.voViewerRewarded}
            >
              <Icon icon={state.voViewerRewarded ? 'mdi:check-circle-outline' : 'mdi:sprout'} size={16} />
              {state.voViewerRewarded
                ? t('forum.contentReward.rewarded')
                : t('forum.contentReward.action')}
            </button>
          )}
        </div>
      </div>

      {(variant === 'summary' || recordsExpanded) && totalCount > 0n && (
        <div className={styles.recordsRegion} aria-live="polite">
          {recordsLoading ? (
            <span className={styles.mutedText}>{t('forum.contentReward.recordsLoading')}</span>
          ) : recordsError ? (
            <div className={styles.recordsError}>
              <span>{t('forum.contentReward.error.loadRecords')}</span>
              <button
                type="button"
                onClick={() => void loadRecords(recordPageIndex, recordsExpanded)}
              >
                {t('common.retry')}
              </button>
            </div>
          ) : records.length === 0 ? (
            <span className={styles.mutedText}>{t('forum.contentReward.recordsEmpty')}</span>
          ) : (
            <div className={styles.recordList}>
              {records.map((record) => {
                const avatarUrl = resolveMediaUrl(record.voSenderAvatarUrl);
                return (
                  <article key={String(record.voRewardId)} className={styles.recordItem}>
                    <span className={styles.recordAvatar} aria-hidden="true">
                      {avatarUrl ? (
                        <img src={avatarUrl} alt="" loading="lazy" />
                      ) : (
                        record.voSenderDisplayName.trim().charAt(0).toUpperCase() || '?'
                      )}
                    </span>
                    <span className={styles.recordText}>
                      <strong>{record.voSenderDisplayName}</strong>
                      <span>{reasonLabel(record.voReasonCode)}</span>
                    </span>
                    <time dateTime={record.voCreateTime}>
                      {formatDateTimeByTimeZone(record.voCreateTime, displayTimeZone, '-', locale)}
                    </time>
                  </article>
                );
              })}
            </div>
          )}
          {recordsExpanded && (canGoPrevious || canGoNext) && (
            <div className={styles.pagination}>
              <button
                type="button"
                disabled={!canGoPrevious || recordsLoading}
                onClick={() => void loadRecords(recordPageIndex - 1, true)}
              >
                {t('common.previousPage')}
              </button>
              <span>{recordPageIndex}</span>
              <button
                type="button"
                disabled={!canGoNext || recordsLoading}
                onClick={() => void loadRecords(recordPageIndex + 1, true)}
              >
                {t('common.nextPage')}
              </button>
            </div>
          )}
        </div>
      )}

      {isMobile ? (
        <BottomSheet
          isOpen={dialogOpen}
          onClose={handleCloseDialog}
          closeLabel={t('common.close')}
          closeOnEscape={!submitting}
          closeOnOverlayClick={!submitting}
          title={t('forum.contentReward.dialogTitle')}
          height="78%"
          footer={dialogFooter}
          bodyClassName={styles.mobileDialogBody}
        >
          {dialogBody}
        </BottomSheet>
      ) : (
        <Modal
          isOpen={dialogOpen}
          onClose={handleCloseDialog}
          closeLabel={t('common.close')}
          closeOnEscape={!submitting}
          closeOnOverlayClick={!submitting}
          title={t('forum.contentReward.dialogTitle')}
          footer={dialogFooter}
        >
          {dialogBody}
        </Modal>
      )}
    </section>
  );
};
