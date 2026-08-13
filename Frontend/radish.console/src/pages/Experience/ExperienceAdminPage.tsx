import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { ApiResponseError } from '@radish/http';
import {
  Button,
  Form,
  message,
} from '@radish/ui';
import {
  adminAdjustExperience,
  adminRecordGovernanceReview,
  type ExpTransactionVo,
  adminFreezeExperience,
  adminUnfreezeExperience,
  getLevelConfigs,
  getLevelRecalculationAudits,
  type ExperienceLevelRecalculationAuditVo,
  type ExperienceLevelRecalculationPreviewVo,
  type UserExpAnomalyRuleSummaryVo,
  getUserDailyStats,
  getUserGovernanceActions,
  getUserTransactions,
  type UserExpGovernanceRecommendationVo,
  type UserExperienceGovernanceActionVo,
  getUserExperience,
  previewLevelConfigRecalculation,
  recalculateLevelConfigs,
  type LevelConfigVo,
  type UserExpDailyStatsVo,
  type UserExpDailyStatsWindowVo,
  type UserExperienceVo,
} from '@/api/experienceAdminApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsoleStatusChip,
} from '@/components/ConsolePage';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { getLocalizedApiErrorMessage } from '@/utils/apiErrorMessage';
import { formatConsoleDateTime, formatConsoleInteger } from '@/utils/localeFormatters';
import dayjs, { type Dayjs } from 'dayjs';
import {
  formatFullStatDate,
  createExperienceGovernanceIdempotencyKey,
  getGovernanceReviewResultForRecommendationLevel,
  getGovernanceReviewResultForRuleSeverity,
  getTransactionExpTypePresetForRuleCodes,
  isFormValidationError,
  normalizePositiveLongIdInput,
  type AdjustFormValues,
  type FreezeFormValues,
  type GovernanceReviewDraftContext,
  type GovernanceReviewFormValues,
  type GovernanceReviewResult,
  type StatsWindowDays,
} from './experienceAdminHelpers';
import { ExperienceAdminHeader } from './ExperienceAdminHeader';
import { ExperienceGovernanceActionForms } from './ExperienceGovernanceActionForms';
import { ExperienceGovernanceReviewSection } from './ExperienceGovernanceReviewSection';
import { ExperienceLevelConfigSection } from './ExperienceLevelConfigSection';
import { ExperienceObservationSummary } from './ExperienceObservationSummary';
import { ExperienceTransactionSection } from './ExperienceTransactionSection';
import { ExperienceUserQuerySummary } from './ExperienceUserQuerySummary';
import '../adminFeature.css';
import './ExperienceAdminPage.css';

type ExperienceReadState = 'idle' | 'loading' | 'ready' | 'unavailable' | 'stale';

const getExperienceReadStateTone = (state: ExperienceReadState) => {
  switch (state) {
    case 'ready':
      return 'success' as const;
    case 'loading':
      return 'info' as const;
    case 'stale':
      return 'warning' as const;
    case 'unavailable':
      return 'danger' as const;
    default:
      return 'neutral' as const;
  }
};

const isExperienceVersionConflict = (error: unknown) => (
  error instanceof ApiResponseError && error.code === 'Experience.VersionConflict'
);

const isExperienceWriteStateUncertain = (error: unknown) => (
  error instanceof ApiResponseError && [
    'Experience.AdjustmentProcessing',
    'Experience.AdjustmentReplayUnavailable',
    'Experience.ReviewProcessing',
    'Experience.ReviewReplayUnavailable',
  ].includes(error.code ?? '')
);

export const ExperienceAdminPage = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('experience.documentTitle'));
  const [searchParams, setSearchParams] = useSearchParams();
  const initialTargetUserId = normalizePositiveLongIdInput(searchParams.get('target') ?? '');
  const initialStatsWindow = searchParams.get('stats') === '30' ? 30 : 7;
  const initialTransactionPage = Math.max(1, Number(searchParams.get('txnPage')) || 1);
  const initialTransactionPageSize = Math.min(100, Math.max(1, Number(searchParams.get('txnSize')) || 10));
  const initialActionPage = Math.max(1, Number(searchParams.get('actionPage')) || 1);
  const initialActionPageSize = Math.min(100, Math.max(1, Number(searchParams.get('actionSize')) || 10));

  const [queryUserId, setQueryUserId] = useState(initialTargetUserId ?? '');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [dailyStatsWindow, setDailyStatsWindow] = useState<UserExpDailyStatsWindowVo | null>(null);
  const [statsWindowDays, setStatsWindowDays] = useState<StatsWindowDays>(initialStatsWindow);
  const [levels, setLevels] = useState<LevelConfigVo[]>([]);
  const [transactions, setTransactions] = useState<ExpTransactionVo[]>([]);
  const [governanceActions, setGovernanceActions] = useState<UserExperienceGovernanceActionVo[]>([]);
  const [governanceActionTotal, setGovernanceActionTotal] = useState(0);
  const [governanceActionPageIndex, setGovernanceActionPageIndex] = useState(initialActionPage);
  const [governanceActionPageSize, setGovernanceActionPageSize] = useState(initialActionPageSize);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionPageIndex, setTransactionPageIndex] = useState(initialTransactionPage);
  const [transactionPageSize, setTransactionPageSize] = useState(initialTransactionPageSize);
  const [transactionTypeFilter, setTransactionTypeFilter] = useState<string | undefined>(
    searchParams.get('txnType') || undefined
  );
  const [transactionStartDate, setTransactionStartDate] = useState<Dayjs | null>(
    searchParams.get('txnStart') ? dayjs(searchParams.get('txnStart')) : null
  );
  const [transactionEndDate, setTransactionEndDate] = useState<Dayjs | null>(
    searchParams.get('txnEnd') ? dayjs(searchParams.get('txnEnd')) : null
  );
  const [transactionReviewHint, setTransactionReviewHint] = useState<string | null>(null);
  const [reviewContextDraft, setReviewContextDraft] = useState<GovernanceReviewDraftContext | null>(null);
  const [loadingExperience, setLoadingExperience] = useState(false);
  const [loadingDailyStats, setLoadingDailyStats] = useState(false);
  const [loadingLevels, setLoadingLevels] = useState(false);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [loadingGovernanceActions, setLoadingGovernanceActions] = useState(false);
  const [experienceReadState, setExperienceReadState] = useState<ExperienceReadState>('idle');
  const [statsReadState, setStatsReadState] = useState<ExperienceReadState>('idle');
  const [transactionsReadState, setTransactionsReadState] = useState<ExperienceReadState>('idle');
  const [actionsReadState, setActionsReadState] = useState<ExperienceReadState>('idle');
  const [levelsReadState, setLevelsReadState] = useState<ExperienceReadState>('idle');
  const [submitting, setSubmitting] = useState(false);
  const [freezing, setFreezing] = useState(false);
  const [unfreezing, setUnfreezing] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [recalculating, setRecalculating] = useState(false);
  const [previewingRecalculation, setPreviewingRecalculation] = useState(false);
  const [levelRecalculationPreview, setLevelRecalculationPreview] = useState<ExperienceLevelRecalculationPreviewVo | null>(null);
  const [levelRecalculationAudits, setLevelRecalculationAudits] = useState<ExperienceLevelRecalculationAuditVo[]>([]);
  const [form] = Form.useForm<AdjustFormValues>();
  const [freezeForm] = Form.useForm<FreezeFormValues>();
  const [reviewForm] = Form.useForm<GovernanceReviewFormValues>();
  const transactionSectionRef = useRef<HTMLElement | null>(null);
  const reviewSectionRef = useRef<HTMLElement | null>(null);
  const freezeSectionRef = useRef<HTMLElement | null>(null);
  const experienceRequestGeneration = useRef(0);
  const statsRequestGeneration = useRef(0);
  const transactionRequestGeneration = useRef(0);
  const actionRequestGeneration = useRef(0);
  const levelsRequestGeneration = useRef(0);
  const initialTargetLoaded = useRef(false);
  const adjustIdempotencyKey = useRef<string | null>(null);
  const reviewIdempotencyKey = useRef<string | null>(null);

  const canAdjust = usePermission(CONSOLE_PERMISSIONS.experienceAdjust);
  const canFreeze = usePermission(CONSOLE_PERMISSIONS.experienceFreeze);
  const canRecalculate = usePermission(CONSOLE_PERMISSIONS.experienceRecalculate);
  const hasAuthoritativeExperience = Boolean(
    loadedUserId && experience && !loadingExperience && experienceReadState === 'ready'
  );
  const hasAuthoritativeLevels = !loadingLevels && levelsReadState === 'ready';
  const dailyStats = dailyStatsWindow?.voStats ?? [];
  const dailyStatsSummary = dailyStatsWindow?.voSummary ?? null;
  const anomalyRuleSummaries = dailyStatsWindow?.voRuleSummaries ?? [];
  const governanceRecommendation = dailyStatsWindow?.voRecommendation ?? null;
  const dailyLimits = dailyStatsWindow?.voLimits ?? null;
  const primaryAnomalyRule = anomalyRuleSummaries[0] ?? null;
  const primaryReviewDay = dailyStats.find((record) => (
    record.voObservations ?? []
  ).some((observation) => observation.voKind === 'anomaly')) ?? dailyStats[0] ?? null;
  const latestGovernanceAction = governanceActions[0] ?? null;

  useEffect(() => {
    setSearchParams((previous) => {
      const next = new URLSearchParams(previous);
      if (loadedUserId) next.set('target', loadedUserId);
      next.set('stats', String(statsWindowDays));
      next.set('txnPage', String(transactionPageIndex));
      next.set('txnSize', String(transactionPageSize));
      next.set('actionPage', String(governanceActionPageIndex));
      next.set('actionSize', String(governanceActionPageSize));
      if (transactionTypeFilter) next.set('txnType', transactionTypeFilter);
      else next.delete('txnType');
      if (transactionStartDate) next.set('txnStart', transactionStartDate.format('YYYY-MM-DD'));
      else next.delete('txnStart');
      if (transactionEndDate) next.set('txnEnd', transactionEndDate.format('YYYY-MM-DD'));
      else next.delete('txnEnd');
      return next;
    }, { replace: true });
  }, [
    governanceActionPageIndex,
    governanceActionPageSize,
    loadedUserId,
    setSearchParams,
    statsWindowDays,
    transactionEndDate,
    transactionPageIndex,
    transactionPageSize,
    transactionStartDate,
    transactionTypeFilter,
  ]);

  const loadLevels = useCallback(async () => {
    const requestGeneration = ++levelsRequestGeneration.current;
    try {
      setLoadingLevels(true);
      const result = await getLevelConfigs();
      if (requestGeneration !== levelsRequestGeneration.current) return;
      setLevels(result);
      setLevelsReadState('ready');
    } catch (error) {
      log.error('ExperienceAdminPage', '加载等级配置失败:', error);
      if (requestGeneration !== levelsRequestGeneration.current) return;
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadLevelsFailed'));
      setLevelsReadState((current) => current === 'ready' || current === 'stale' ? 'stale' : 'unavailable');
    } finally {
      if (requestGeneration === levelsRequestGeneration.current) setLoadingLevels(false);
    }
  }, [t]);

  useEffect(() => {
    void loadLevels();
  }, [loadLevels]);

  const loadLevelRecalculationAudits = useCallback(async () => {
    if (!canRecalculate) return;
    try {
      const result = await getLevelRecalculationAudits(1, 10);
      setLevelRecalculationAudits(result.data);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载等级重算审计失败:', error);
    }
  }, [canRecalculate]);

  useEffect(() => {
    void loadLevelRecalculationAudits();
  }, [loadLevelRecalculationAudits]);

  const loadDailyStats = async (userId: string, days: StatsWindowDays = statsWindowDays) => {
    const requestGeneration = ++statsRequestGeneration.current;
    const normalizedUserId = normalizePositiveLongIdInput(userId);
    if (!normalizedUserId) {
      setDailyStatsWindow(null);
      return;
    }

    try {
      setLoadingDailyStats(true);
      const result = await getUserDailyStats(normalizedUserId, days);
      if (requestGeneration !== statsRequestGeneration.current) return;
      setDailyStatsWindow(result);
      setStatsReadState('ready');
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验统计失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadStatsFailed'));
      if (requestGeneration !== statsRequestGeneration.current) return;
      setStatsReadState((current) => current === 'ready' || current === 'stale' ? 'stale' : 'unavailable');
    } finally {
      if (requestGeneration === statsRequestGeneration.current) setLoadingDailyStats(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId || !experience) {
      setDailyStatsWindow(null);
      setStatsReadState('idle');
      return;
    }

    void loadDailyStats(loadedUserId, statsWindowDays);
    // Daily stats refresh is scoped to the selected user and window; memoizing the loader would pull unrelated form state into the effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadedUserId, statsWindowDays]);

  const loadGovernanceActions = useCallback(async (
    userId: string,
    pageIndex: number = governanceActionPageIndex,
    pageSize: number = governanceActionPageSize
  ) => {
    const requestGeneration = ++actionRequestGeneration.current;
    const normalizedUserId = normalizePositiveLongIdInput(userId);
    if (!normalizedUserId) {
      setGovernanceActions([]);
      setGovernanceActionTotal(0);
      return;
    }

    try {
      setLoadingGovernanceActions(true);
      const result = await getUserGovernanceActions(normalizedUserId, pageIndex, pageSize);
      if (requestGeneration !== actionRequestGeneration.current) return;
      setGovernanceActions(result.data);
      setGovernanceActionTotal(result.dataCount);
      setGovernanceActionPageIndex(result.page);
      setGovernanceActionPageSize(result.pageSize);
      setActionsReadState('ready');
    } catch (error) {
      log.error('ExperienceAdminPage', '加载经验治理留痕失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadActionsFailed'));
      if (requestGeneration !== actionRequestGeneration.current) return;
      setActionsReadState((current) => current === 'ready' || current === 'stale' ? 'stale' : 'unavailable');
    } finally {
      if (requestGeneration === actionRequestGeneration.current) setLoadingGovernanceActions(false);
    }
  }, [governanceActionPageIndex, governanceActionPageSize, t]);

  useEffect(() => {
    if (!loadedUserId) {
      setGovernanceActions([]);
      setGovernanceActionTotal(0);
      setActionsReadState('idle');
      return;
    }

    void loadGovernanceActions(loadedUserId, governanceActionPageIndex, governanceActionPageSize);
  }, [
    governanceActionPageIndex,
    governanceActionPageSize,
    loadGovernanceActions,
    loadedUserId,
  ]);

  const loadTransactions = async (
    userId: string,
    targetPageIndex = transactionPageIndex,
    targetPageSize = transactionPageSize,
    targetExpType = transactionTypeFilter,
    targetStartDate = transactionStartDate,
    targetEndDate = transactionEndDate
  ) => {
    const requestGeneration = ++transactionRequestGeneration.current;
    const normalizedUserId = normalizePositiveLongIdInput(userId);
    if (!normalizedUserId) {
      setTransactions([]);
      setTransactionTotal(0);
      setTransactionPageIndex(1);
      return;
    }

    try {
      setLoadingTransactions(true);
      const result = await getUserTransactions({
        userId: normalizedUserId,
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        expType: targetExpType,
        startDate: targetStartDate ? targetStartDate.startOf('day').toDate().toISOString() : undefined,
        endDate: targetEndDate ? targetEndDate.endOf('day').toDate().toISOString() : undefined,
      });
      if (requestGeneration !== transactionRequestGeneration.current) return;
      setTransactions(result.data);
      setTransactionTotal(result.dataCount);
      setTransactionPageIndex(result.page);
      setTransactionPageSize(result.pageSize);
      setTransactionsReadState('ready');
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验流水失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadTransactionsFailed'));
      if (requestGeneration !== transactionRequestGeneration.current) return;
      setTransactionsReadState((current) => current === 'ready' || current === 'stale' ? 'stale' : 'unavailable');
    } finally {
      if (requestGeneration === transactionRequestGeneration.current) setLoadingTransactions(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId) {
      setTransactions([]);
      setTransactionTotal(0);
      setTransactionPageIndex(1);
      setTransactionsReadState('idle');
      return;
    }

    void loadTransactions(loadedUserId, transactionPageIndex, transactionPageSize, transactionTypeFilter, transactionStartDate, transactionEndDate);
    // Transaction refresh is driven by explicit ledger filters; the loader also serves pagination callbacks with current defaults.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadedUserId, transactionTypeFilter, transactionStartDate, transactionEndDate]);

  const clearLoadedExperience = () => {
    setLoadedUserId(null);
    setExperience(null);
    setExperienceReadState('idle');
    setDailyStatsWindow(null);
    setStatsReadState('idle');
    setTransactions([]);
    setTransactionTotal(0);
    setTransactionPageIndex(1);
    setTransactionTypeFilter(undefined);
    setTransactionStartDate(null);
    setTransactionEndDate(null);
    setTransactionReviewHint(null);
    setTransactionsReadState('idle');
    setGovernanceActions([]);
    setGovernanceActionTotal(0);
    setGovernanceActionPageIndex(1);
    setActionsReadState('idle');
    setReviewContextDraft(null);
    adjustIdempotencyKey.current = null;
    reviewIdempotencyKey.current = null;
    if (loadedUserId) {
      reviewForm.resetFields();
    }
    freezeForm.setFieldsValue({
      reason: '',
      frozenUntil: undefined,
    });
  };

  const loadExperience = async (
    userIdOverride?: string,
    options?: {
      showInvalidMessage?: boolean;
      showFailureMessage?: boolean;
      preserveDrafts?: boolean;
    }
  ) => {
    const requestGeneration = ++experienceRequestGeneration.current;
    const userId = userIdOverride ?? normalizePositiveLongIdInput(queryUserId);
    if (!userId) {
      if (options?.showInvalidMessage ?? true) {
        message.error(t('experience.form.userIdInvalid'));
      }
      return;
    }

    try {
      setQueryUserId(String(userId));
      setLoadingExperience(true);
      const result = await getUserExperience(userId);
      if (requestGeneration !== experienceRequestGeneration.current) return;
      if (loadedUserId !== String(userId)) {
        clearLoadedExperience();
        if (!loadedUserId && initialTargetUserId === String(userId)) {
          setTransactionPageIndex(initialTransactionPage);
          setGovernanceActionPageIndex(initialActionPage);
        }
      }
      setLoadedUserId(String(userId));
      setExperience(result);
      setExperienceReadState('ready');
      if (!options?.preserveDrafts) {
        freezeForm.setFieldsValue({
          reason: result.voFrozenReason || '',
          frozenUntil: result.voFrozenUntil ? dayjs(result.voFrozenUntil) : undefined,
        });
        if (loadedUserId) {
          reviewForm.resetFields();
        }
      }
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验失败:', error);
      if (requestGeneration !== experienceRequestGeneration.current) return;
      if (options?.showFailureMessage ?? true) {
        message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadUserFailed'));
      }
      setExperienceReadState((current) => current === 'ready' || current === 'stale' ? 'stale' : 'unavailable');
    } finally {
      if (requestGeneration === experienceRequestGeneration.current) setLoadingExperience(false);
    }
  };

  useEffect(() => {
    if (initialTargetLoaded.current || !initialTargetUserId) return;
    initialTargetLoaded.current = true;
    void loadExperience(initialTargetUserId, { showInvalidMessage: false });
    // Initial URL hydration must run once; later target changes are explicit queries.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialTargetUserId]);

  const reconcileExperienceAfterWriteFailure = async (error: unknown) => {
    if (isExperienceVersionConflict(error)) {
      setExperienceReadState('stale');
      if (loadedUserId) {
        await loadExperience(loadedUserId, {
          showInvalidMessage: false,
          showFailureMessage: false,
          preserveDrafts: true,
        });
      }
      return;
    }

    if (isExperienceWriteStateUncertain(error)) {
      setExperienceReadState('stale');
    }
  };

  const focusTransactionSection = () => {
    transactionSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const focusReviewSection = () => {
    reviewSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const focusFreezeSection = () => {
    freezeSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const applyTransactionReviewPreset = (options: {
    expType?: string;
    date?: Dayjs | null;
    hint: string;
  }) => {
    setTransactionPageIndex(1);
    setTransactionTypeFilter(options.expType);
    setTransactionStartDate(options.date ?? null);
    setTransactionEndDate(options.date ?? null);
    setTransactionReviewHint(options.hint);
    focusTransactionSection();
  };

  const applyGovernanceReviewDraft = (options: {
    reviewResult: GovernanceReviewResult;
    remark: string;
    context: GovernanceReviewDraftContext;
  }) => {
    reviewForm.setFieldsValue({
      reviewResult: options.reviewResult,
      remark: options.remark,
    });
    setReviewContextDraft(options.context);
    focusReviewSection();
    message.success(t('experience.feedback.reviewDraftPrefilled'));
  };

  const prefillFreezeReason = (reason: string) => {
    if (!loadedUserId) {
      return;
    }

    freezeForm.setFieldsValue({
      reason,
    });
    focusFreezeSection();
    message.success(t('experience.feedback.freezeReasonPrefilled'));
  };

  const handleRuleReview = (rule: UserExpAnomalyRuleSummaryVo) => {
    applyTransactionReviewPreset({
      expType: getTransactionExpTypePresetForRuleCodes([rule.voRuleCode]),
      date: rule.voLatestHitDate ? dayjs(rule.voLatestHitDate) : null,
      hint: rule.voLatestHitDate
        ? t('experience.hint.ruleTransactionsOnDate', { rule: rule.voRuleLabel, date: formatFullStatDate(rule.voLatestHitDate, i18n.resolvedLanguage) })
        : t('experience.hint.ruleTransactions', { rule: rule.voRuleLabel }),
    });
  };

  const handleRuleFreezeReason = (rule: UserExpAnomalyRuleSummaryVo) => {
    const latestHitDate = rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate) : '最近命中日';
    prefillFreezeReason(
      `经验异常待复核：最近 ${statsWindowDays} 天规则「${rule.voRuleLabel}」命中 ${rule.voHitDays} 天，最近命中 ${latestHitDate}，最强信号为${rule.voStrongestSignal}。`
    );
  };

  const handleRecommendationGovernanceReview = (recommendation: UserExpGovernanceRecommendationVo) => {
    applyGovernanceReviewDraft({
      reviewResult: getGovernanceReviewResultForRecommendationLevel(recommendation.voLevel),
      remark: `经验治理复核：最近 ${statsWindowDays} 天系统建议「${recommendation.voTitle}」，已结合经验流水与相关来源人工复核。`,
      context: {
        windowDays: statsWindowDays,
        ruleCodes: [],
        ruleLabels: [],
        recommendationLevel: recommendation.voLevel,
        recommendationReason: recommendation.voReason,
        hint: t('experience.hint.recommendationPrefilled', { title: recommendation.voTitle }),
      },
    });
  };

  const handleRuleGovernanceReview = (rule: UserExpAnomalyRuleSummaryVo) => {
    const latestHitDate = rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate) : null;
    applyGovernanceReviewDraft({
      reviewResult: getGovernanceReviewResultForRuleSeverity(rule.voSeverity),
      remark: latestHitDate
        ? `经验治理复核：最近 ${statsWindowDays} 天规则「${rule.voRuleLabel}」命中 ${rule.voHitDays} 天，最近命中 ${latestHitDate}。`
        : `经验治理复核：最近 ${statsWindowDays} 天规则「${rule.voRuleLabel}」命中 ${rule.voHitDays} 天。`,
      context: {
        windowDays: statsWindowDays,
        statDate: rule.voLatestHitDate,
        ruleCodes: [rule.voRuleCode],
        ruleLabels: [rule.voRuleLabel],
        recommendationLevel: governanceRecommendation?.voLevel,
        recommendationReason: rule.voSuggestedAction,
        hint: latestHitDate
          ? t('experience.hint.ruleReviewOnDate', { rule: rule.voRuleLabel, date: latestHitDate })
          : t('experience.hint.ruleReview', { rule: rule.voRuleLabel }),
      },
    });
  };

  const handleDayReview = (record: UserExpDailyStatsVo) => {
    const anomalyRuleCodes = (record.voObservations ?? [])
      .filter((observation) => observation.voKind === 'anomaly')
      .map((observation) => observation.voRuleCode);
    applyTransactionReviewPreset({
      expType: getTransactionExpTypePresetForRuleCodes(anomalyRuleCodes),
      date: dayjs(record.voStatDate),
      hint: t('experience.hint.dayTransactions', { date: formatFullStatDate(record.voStatDate, i18n.resolvedLanguage) }),
    });
  };

  const handleDayFreezeReason = (record: UserExpDailyStatsVo) => {
    const anomalyLabels = (record.voObservations ?? [])
      .filter((observation) => observation.voKind === 'anomaly')
      .map((observation) => observation.voLabel);
    prefillFreezeReason(
      `经验异常待复核：${formatFullStatDate(record.voStatDate)} 命中 ${anomalyLabels.join('、')}，请结合经验流水、互动来源和目标内容人工复核。`
    );
  };

  const handleDayGovernanceReview = (record: UserExpDailyStatsVo) => {
    const anomalyObservations = (record.voObservations ?? [])
      .filter((observation) => observation.voKind === 'anomaly');
    applyGovernanceReviewDraft({
      reviewResult: anomalyObservations.length >= 2 ? 'FreezeSuggest' : 'Observe',
      remark: `经验治理复核：${formatFullStatDate(record.voStatDate)} 命中 ${anomalyObservations.map((observation) => observation.voLabel).join('、')}，已结合经验流水与相关来源人工复核。`,
      context: {
        windowDays: statsWindowDays,
        statDate: record.voStatDate,
        ruleCodes: anomalyObservations.map((observation) => observation.voRuleCode),
        ruleLabels: anomalyObservations.map((observation) => observation.voLabel),
        recommendationLevel: governanceRecommendation?.voLevel,
        recommendationReason: governanceRecommendation?.voReason,
        hint: t('experience.hint.dayReview', { date: formatFullStatDate(record.voStatDate, i18n.resolvedLanguage) }),
      },
    });
  };

  const handleRecordGovernanceReview = async () => {
    if (!loadedUserId || !experience || !hasAuthoritativeExperience) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }

    try {
      const values = await reviewForm.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(loadedUserId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setReviewing(true);
      reviewIdempotencyKey.current ??= createExperienceGovernanceIdempotencyKey(
        'review',
        normalizedUserId,
        experience.voVersion
      );
      const result = await adminRecordGovernanceReview({
        userId: normalizedUserId,
        reviewResult: values.reviewResult,
        remark: values.remark.trim(),
        windowDays: reviewContextDraft?.windowDays,
        statDate: reviewContextDraft?.statDate
          ? dayjs(reviewContextDraft.statDate).format('YYYY-MM-DD')
          : undefined,
        ruleCodes: reviewContextDraft?.ruleCodes,
        ruleLabels: reviewContextDraft?.ruleLabels,
        recommendationLevel: reviewContextDraft?.recommendationLevel,
        recommendationReason: reviewContextDraft?.recommendationReason ?? undefined,
        expectedVersion: experience.voVersion,
        idempotencyKey: reviewIdempotencyKey.current,
      });

      setExperience(result.voExperience);
      setExperienceReadState('ready');
      message.success(t('experience.feedback.reviewRecorded'));
      setGovernanceActionPageIndex(1);
      await loadGovernanceActions(normalizedUserId, 1, governanceActionPageSize);
      reviewForm.resetFields();
      setReviewContextDraft(null);
      reviewIdempotencyKey.current = null;
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '记录经验治理复核结论失败:', error);
      await reconcileExperienceAfterWriteFailure(error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.reviewFailed'));
    } finally {
      setReviewing(false);
    }
  };

  const handleAdjust = async () => {
    if (!loadedUserId || !experience || !hasAuthoritativeExperience) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }

    try {
      const values = await form.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(loadedUserId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setSubmitting(true);
      setTransactionPageIndex(1);
      adjustIdempotencyKey.current ??= createExperienceGovernanceIdempotencyKey(
        'adjust',
        normalizedUserId,
        experience.voVersion
      );
      const result = await adminAdjustExperience({
        userId: normalizedUserId,
        deltaExp: values.deltaExp,
        reason: values.reason.trim(),
        expectedVersion: experience.voVersion,
        idempotencyKey: adjustIdempotencyKey.current,
      });

      setExperience(result.voExperience);
      setExperienceReadState('ready');
      message.success(t('experience.feedback.adjusted'));
      await loadTransactions(
        normalizedUserId,
        1,
        transactionPageSize,
        transactionTypeFilter,
        transactionStartDate,
        transactionEndDate
      );
      form.setFieldsValue({ deltaExp: 0, reason: '' });
      adjustIdempotencyKey.current = null;
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '调整经验失败:', error);
      await reconcileExperienceAfterWriteFailure(error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.adjustFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleFreeze = async () => {
    if (!loadedUserId || !experience || !hasAuthoritativeExperience) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }

    try {
      const values = await freezeForm.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(loadedUserId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setFreezing(true);
      setTransactionPageIndex(1);
      const result = await adminFreezeExperience({
        userId: normalizedUserId,
        reason: values.reason.trim(),
        frozenUntil: values.frozenUntil ? values.frozenUntil.toDate().toISOString() : undefined,
        expectedVersion: experience.voVersion,
      });

      setExperience(result.voExperience);
      setExperienceReadState('ready');
      message.success(t('experience.feedback.frozen'));
      setGovernanceActionPageIndex(1);
      await loadGovernanceActions(normalizedUserId, 1, governanceActionPageSize);
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '冻结经验失败:', error);
      await reconcileExperienceAfterWriteFailure(error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.freezeFailed'));
    } finally {
      setFreezing(false);
    }
  };

  const handleUnfreeze = async () => {
    if (!loadedUserId || !experience || !hasAuthoritativeExperience) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }

    try {
      const values = await freezeForm.validateFields(['reason']);
      const normalizedUserId = normalizePositiveLongIdInput(loadedUserId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setUnfreezing(true);
      setTransactionPageIndex(1);
      const result = await adminUnfreezeExperience({
        userId: normalizedUserId,
        reason: values.reason.trim(),
        expectedVersion: experience.voVersion,
      });

      setExperience(result.voExperience);
      setExperienceReadState('ready');
      message.success(t('experience.feedback.unfrozen'));
      setGovernanceActionPageIndex(1);
      await loadGovernanceActions(normalizedUserId, 1, governanceActionPageSize);
      freezeForm.setFieldsValue({
        reason: '',
        frozenUntil: undefined,
      });
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '解冻经验失败:', error);
      await reconcileExperienceAfterWriteFailure(error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.unfreezeFailed'));
    } finally {
      setUnfreezing(false);
    }
  };

  const handlePreviewRecalculation = async () => {
    if (!hasAuthoritativeLevels) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }

    try {
      setPreviewingRecalculation(true);
      setLevelRecalculationPreview(await previewLevelConfigRecalculation());
    } catch (error) {
      log.error('ExperienceAdminPage', '预览等级配置重算失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.recalculateFailed'));
    } finally {
      setPreviewingRecalculation(false);
    }
  };

  const handleRecalculate = async (reason: string) => {
    if (!levelRecalculationPreview || !hasAuthoritativeLevels) {
      message.error(t('experience.feedback.authorityRequired'));
      return;
    }
    try {
      setRecalculating(true);
      const result = await recalculateLevelConfigs({
        expectedFingerprint: levelRecalculationPreview.voFingerprint,
        reason,
      });
      setLevels(result.voLevels);
      setLevelsReadState('ready');
      setLevelRecalculationPreview(null);
      await loadLevelRecalculationAudits();
      message.success(t('experience.feedback.recalculated'));
    } catch (error) {
      log.error('ExperienceAdminPage', '重算等级配置失败:', error);
      if (error instanceof ApiResponseError && error.code === 'Experience.LevelPreviewConflict') {
        setLevelsReadState('stale');
      }
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.recalculateFailed'));
    } finally {
      setRecalculating(false);
    }
  };

  return (
    <div className="admin-feature-page">
      <ExperienceAdminHeader
        canAdjust={canAdjust}
        canFreeze={canFreeze}
        onRefresh={() => {
          void Promise.all([
            loadExperience(undefined, { showInvalidMessage: false }),
            loadLevels(),
          ]);
        }}
      />

      <ConsoleMetricGrid label={t('experience.metrics.ariaLabel')}>
        <ConsoleMetricCard
          label={t('experience.metrics.currentUser')}
          value={loadedUserId ?? t('experience.common.notQueried')}
          description={experience?.voUserName || t('experience.metrics.awaitUserId')}
          tone={loadedUserId ? 'info' : 'neutral'}
        />
        <ConsoleMetricCard
          label={t('experience.metrics.totalExp')}
          value={experience ? formatConsoleInteger(experience.voTotalExp, i18n.resolvedLanguage) : '--'}
          description={experience ? t('experience.metrics.level', { level: experience.voCurrentLevel }) : t('experience.metrics.userNotLoaded')}
          tone="success"
        />
        <ConsoleMetricCard
          label={t('experience.metrics.anomalyHits')}
          value={dailyStatsSummary ? formatConsoleInteger(dailyStatsSummary.voReviewDays, i18n.resolvedLanguage) : '--'}
          description={t('experience.metrics.window', { count: statsWindowDays })}
          tone={dailyStatsSummary && dailyStatsSummary.voReviewDays > 0 ? 'warning' : 'neutral'}
        />
        <ConsoleMetricCard
          label={t('experience.metrics.actions')}
          value={actionsReadState === 'ready' || actionsReadState === 'stale'
            ? formatConsoleInteger(governanceActionTotal, i18n.resolvedLanguage)
            : '--'}
          description={t('experience.metrics.actionsDescription')}
        />
      </ConsoleMetricGrid>

      <section className="experience-read-state-strip" aria-label={t('experience.metrics.ariaLabel')}>
        {([
          ['experience.query.title', loadingExperience ? 'loading' : experienceReadState],
          ['experience.observation.title', loadingDailyStats ? 'loading' : statsReadState],
          ['experience.transactions.title', loadingTransactions ? 'loading' : transactionsReadState],
          ['experience.review.recentActions', loadingGovernanceActions ? 'loading' : actionsReadState],
          ['experience.levels.title', loadingLevels ? 'loading' : levelsReadState],
        ] as const).map(([labelKey, state]) => (
          <div key={labelKey} className="experience-read-state-strip__item">
            <span>{t(labelKey)}</span>
            <ConsoleStatusChip tone={getExperienceReadStateTone(state)}>
              {t(`experience.readState.${state}`)}
            </ConsoleStatusChip>
          </div>
        ))}
      </section>

      <section className="governance-task-flow" aria-label={t('experience.flow.ariaLabel')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('experience.flow.user')}</strong>
          <p>{loadedUserId ? t('experience.flow.userLoaded', { userId: loadedUserId }) : t('experience.flow.userPending')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('experience.flow.trend')}</strong>
          <p>{dailyStatsSummary ? t('experience.flow.trendReady', { window: statsWindowDays, count: dailyStatsSummary.voReviewDays }) : t('experience.flow.trendPending')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('experience.flow.transactions')}</strong>
          <p>{transactionTotal > 0 ? t('experience.flow.transactionsReady', { count: transactionTotal }) : t('experience.flow.transactionsPending')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('experience.flow.review')}</strong>
          <p>{governanceActionTotal > 0 ? t('experience.flow.reviewReady', { count: governanceActionTotal }) : t('experience.flow.reviewPending')}</p>
        </div>
      </section>

      <div className="governance-workbench governance-workbench--experience">
        <div className="governance-workbench__queue">
          <ExperienceUserQuerySummary
            queryUserId={queryUserId}
            loadedUserId={loadedUserId}
            experience={experience}
            loadingExperience={loadingExperience}
            onQueryUserIdChange={setQueryUserId}
            onQuery={() => {
              void loadExperience(undefined, { showInvalidMessage: true });
            }}
          />

          <ExperienceObservationSummary
            loadedUserId={loadedUserId}
            statsWindowDays={statsWindowDays}
            loadingDailyStats={loadingDailyStats}
            canFreeze={canFreeze}
            dailyStats={dailyStats}
            dailyStatsSummary={dailyStatsSummary}
            anomalyRuleSummaries={anomalyRuleSummaries}
            governanceRecommendation={governanceRecommendation}
            dailyLimits={dailyLimits}
            onStatsWindowDaysChange={setStatsWindowDays}
            onRecommendationGovernanceReview={handleRecommendationGovernanceReview}
            onRecommendationFreezeReason={prefillFreezeReason}
            onRuleReview={handleRuleReview}
            onRuleGovernanceReview={handleRuleGovernanceReview}
            onRuleFreezeReason={handleRuleFreezeReason}
            onDayReview={handleDayReview}
            onDayGovernanceReview={handleDayGovernanceReview}
            onDayFreezeReason={handleDayFreezeReason}
          />
        </div>

        <div className="governance-workbench__detail">
          <ExperienceTransactionSection
            transactionSectionRef={transactionSectionRef}
            loadedUserId={loadedUserId}
            transactions={transactions}
            loadingTransactions={loadingTransactions}
            transactionTotal={transactionTotal}
            transactionPageIndex={transactionPageIndex}
            transactionPageSize={transactionPageSize}
            transactionTypeFilter={transactionTypeFilter}
            transactionStartDate={transactionStartDate}
            transactionEndDate={transactionEndDate}
            transactionReviewHint={transactionReviewHint}
            onTransactionTypeFilterChange={(value) => {
              setTransactionPageIndex(1);
              setTransactionReviewHint(null);
              setTransactionTypeFilter(value);
            }}
            onTransactionStartDateChange={(value) => {
              setTransactionPageIndex(1);
              setTransactionReviewHint(null);
              setTransactionStartDate(value);
            }}
            onTransactionEndDateChange={(value) => {
              setTransactionPageIndex(1);
              setTransactionReviewHint(null);
              setTransactionEndDate(value);
            }}
            onClearTransactionFilters={() => {
              setTransactionPageIndex(1);
              setTransactionTypeFilter(undefined);
              setTransactionStartDate(null);
              setTransactionEndDate(null);
              setTransactionReviewHint(null);
            }}
            onPageChange={(page, pageSize) => {
              if (!loadedUserId) {
                return;
              }

              void loadTransactions(loadedUserId, page, pageSize, transactionTypeFilter, transactionStartDate, transactionEndDate);
            }}
          />

          <ExperienceGovernanceReviewSection
            reviewSectionRef={reviewSectionRef}
            loadedUserId={loadedUserId}
            experience={experience}
            reviewForm={reviewForm}
            reviewContextDraft={reviewContextDraft}
            canFreeze={canFreeze && hasAuthoritativeExperience}
            reviewing={reviewing}
            governanceActions={governanceActions}
            governanceActionTotal={governanceActionTotal}
            governanceActionPageIndex={governanceActionPageIndex}
            governanceActionPageSize={governanceActionPageSize}
            loadingGovernanceActions={loadingGovernanceActions}
            onGovernanceActionPageChange={(page, pageSize) => {
              setGovernanceActionPageIndex(page);
              setGovernanceActionPageSize(pageSize);
            }}
            onRecordGovernanceReview={handleRecordGovernanceReview}
            onClearReviewDraft={() => {
              reviewForm.resetFields();
              setReviewContextDraft(null);
            }}
          />
        </div>

        <div className="governance-workbench__actions">
          <section className="admin-feature-rail" aria-label={t('experience.rail.ariaLabel')}>
            <div className="admin-feature-rail__header">
              <div>
                <span className="admin-feature-rail__eyebrow">{t('experience.rail.eyebrow')}</span>
                <h3>{t('experience.rail.title')}</h3>
              </div>
              <ConsoleStatusChip tone={experience?.voExpFrozen ? 'warning' : (loadedUserId ? 'info' : 'neutral')}>
                {experience?.voExpFrozen ? t('experience.common.freezing') : (loadedUserId ? t('experience.common.observing') : t('experience.common.notQueried'))}
              </ConsoleStatusChip>
            </div>

            {loadedUserId && experience ? (
              <>
                <div className="admin-feature-rail__list">
                  <div className="admin-feature-rail__item">
                    <span>{t('experience.metrics.currentUser')}</span>
                    <strong>{experience.voUserName || `#${loadedUserId}`}</strong>
                  </div>
                  <div className="admin-feature-rail__item">
                    <span>{t('experience.rail.levelTotal')}</span>
                    <strong>Lv.{experience.voCurrentLevel} · {formatConsoleInteger(experience.voTotalExp, i18n.resolvedLanguage)}</strong>
                  </div>
                  <div className="admin-feature-rail__item">
                    <span>{t('experience.rail.recommendation')}</span>
                    <strong>{governanceRecommendation?.voTitle ?? t('experience.rail.noRecommendation')}</strong>
                  </div>
                </div>

                {primaryAnomalyRule ? (
                  <div className="admin-feature-rail__callout">
                    <span>{t('experience.rail.primaryRule')}</span>
                    <strong>{primaryAnomalyRule.voRuleLabel}</strong>
                    <p>{primaryAnomalyRule.voStrongestSignal}</p>
                    <div className="admin-feature-rail__actions">
                      <Button size="small" onClick={() => handleRuleReview(primaryAnomalyRule)}>
                        {t('experience.actions.viewTransactions')}
                      </Button>
                      <Button
                        size="small"
                        disabled={!canFreeze}
                        onClick={() => handleRuleGovernanceReview(primaryAnomalyRule)}
                      >
                        {t('experience.actions.prefillReviewShort')}
                      </Button>
                    </div>
                  </div>
                ) : primaryReviewDay ? (
                  <div className="admin-feature-rail__callout">
                    <span>{t('experience.rail.reviewDay')}</span>
                    <strong>{formatFullStatDate(primaryReviewDay.voStatDate, i18n.resolvedLanguage)}</strong>
                    <p>{t('experience.rail.dayExp', { value: formatConsoleInteger(primaryReviewDay.voExpEarned, i18n.resolvedLanguage) })}</p>
                    <div className="admin-feature-rail__actions">
                      <Button size="small" onClick={() => handleDayReview(primaryReviewDay)}>
                        {t('experience.actions.locateTransactions')}
                      </Button>
                      <Button
                        size="small"
                        disabled={!canFreeze}
                        onClick={() => handleDayGovernanceReview(primaryReviewDay)}
                      >
                        {t('experience.actions.prefillReviewShort')}
                      </Button>
                    </div>
                  </div>
                ) : (
                  <p className="admin-feature-rail__empty">
                    {t('experience.rail.noObservation')}
                  </p>
                )}

                <div className="admin-feature-rail__list">
                  <div className="admin-feature-rail__item">
                    <span>{t('experience.rail.latestAction')}</span>
                    <strong>{latestGovernanceAction ? t(`experience.actionType.${latestGovernanceAction.voActionType}`, { defaultValue: latestGovernanceAction.voActionTypeDisplay }) : t('experience.common.none')}</strong>
                  </div>
                  <div className="admin-feature-rail__item">
                    <span>{t('experience.rail.freezeStatus')}</span>
                    <strong>{experience.voExpFrozen ? (experience.voFrozenUntil ? formatConsoleDateTime(experience.voFrozenUntil, i18n.resolvedLanguage) : t('experience.common.permanentFreeze')) : t('experience.common.notFrozen')}</strong>
                  </div>
                </div>
              </>
            ) : (
              <p className="admin-feature-rail__empty">
                {t('experience.rail.queryFirst')}
              </p>
            )}
          </section>

          <ExperienceGovernanceActionForms
            adjustForm={form}
            freezeForm={freezeForm}
            freezeSectionRef={freezeSectionRef}
            experience={experience}
            loadedUserId={loadedUserId}
            canAdjust={canAdjust && hasAuthoritativeExperience}
            canFreeze={canFreeze && hasAuthoritativeExperience}
            submitting={submitting}
            freezing={freezing}
            unfreezing={unfreezing}
            onAdjust={handleAdjust}
            onFreeze={handleFreeze}
            onUnfreeze={handleUnfreeze}
          />
        </div>
      </div>

      <ExperienceLevelConfigSection
        levels={levels}
        loadingLevels={loadingLevels}
        canRecalculate={canRecalculate && hasAuthoritativeLevels}
        recalculating={recalculating}
        previewing={previewingRecalculation}
        preview={levelRecalculationPreview}
        audits={levelRecalculationAudits}
        onPreview={handlePreviewRecalculation}
        onRecalculate={handleRecalculate}
      />
    </div>
  );
};
