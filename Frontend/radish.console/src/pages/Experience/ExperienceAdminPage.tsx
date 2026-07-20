import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  type UserExpAnomalyRuleSummaryVo,
  getUserDailyStats,
  getUserGovernanceActions,
  getUserTransactions,
  type UserExpGovernanceRecommendationVo,
  type UserExperienceGovernanceActionVo,
  getUserExperience,
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

export const ExperienceAdminPage = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('experience.documentTitle'));

  const [queryUserId, setQueryUserId] = useState('');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [dailyStatsWindow, setDailyStatsWindow] = useState<UserExpDailyStatsWindowVo | null>(null);
  const [statsWindowDays, setStatsWindowDays] = useState<StatsWindowDays>(7);
  const [levels, setLevels] = useState<LevelConfigVo[]>([]);
  const [transactions, setTransactions] = useState<ExpTransactionVo[]>([]);
  const [governanceActions, setGovernanceActions] = useState<UserExperienceGovernanceActionVo[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionPageIndex, setTransactionPageIndex] = useState(1);
  const [transactionPageSize, setTransactionPageSize] = useState(10);
  const [transactionTypeFilter, setTransactionTypeFilter] = useState<string | undefined>();
  const [transactionStartDate, setTransactionStartDate] = useState<Dayjs | null>(null);
  const [transactionEndDate, setTransactionEndDate] = useState<Dayjs | null>(null);
  const [transactionReviewHint, setTransactionReviewHint] = useState<string | null>(null);
  const [reviewContextDraft, setReviewContextDraft] = useState<GovernanceReviewDraftContext | null>(null);
  const [loadingExperience, setLoadingExperience] = useState(false);
  const [loadingDailyStats, setLoadingDailyStats] = useState(false);
  const [loadingLevels, setLoadingLevels] = useState(false);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [loadingGovernanceActions, setLoadingGovernanceActions] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [freezing, setFreezing] = useState(false);
  const [unfreezing, setUnfreezing] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [recalculating, setRecalculating] = useState(false);
  const [form] = Form.useForm<AdjustFormValues>();
  const [freezeForm] = Form.useForm<FreezeFormValues>();
  const [reviewForm] = Form.useForm<GovernanceReviewFormValues>();
  const transactionSectionRef = useRef<HTMLElement | null>(null);
  const reviewSectionRef = useRef<HTMLElement | null>(null);
  const freezeSectionRef = useRef<HTMLElement | null>(null);

  const canAdjust = usePermission(CONSOLE_PERMISSIONS.experienceAdjust);
  const canFreeze = usePermission(CONSOLE_PERMISSIONS.experienceFreeze);
  const canRecalculate = usePermission(CONSOLE_PERMISSIONS.experienceRecalculate);
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

  const loadLevels = useCallback(async () => {
    try {
      setLoadingLevels(true);
      const result = await getLevelConfigs();
      setLevels(result);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载等级配置失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadLevelsFailed'));
    } finally {
      setLoadingLevels(false);
    }
  }, [t]);

  useEffect(() => {
    void loadLevels();
  }, [loadLevels]);

  const loadDailyStats = async (userId: string, days: StatsWindowDays = statsWindowDays) => {
    const normalizedUserId = normalizePositiveLongIdInput(userId);
    if (!normalizedUserId) {
      setDailyStatsWindow(null);
      return;
    }

    try {
      setLoadingDailyStats(true);
      const result = await getUserDailyStats(normalizedUserId, days);
      setDailyStatsWindow(result);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验统计失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadStatsFailed'));
      setDailyStatsWindow(null);
    } finally {
      setLoadingDailyStats(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId) {
      setDailyStatsWindow(null);
      return;
    }

    void loadDailyStats(loadedUserId, statsWindowDays);
    // Daily stats refresh is scoped to the selected user and window; memoizing the loader would pull unrelated form state into the effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadedUserId, statsWindowDays]);

  const loadGovernanceActions = useCallback(async (userId: string, take: number = 20) => {
    const normalizedUserId = normalizePositiveLongIdInput(userId);
    if (!normalizedUserId) {
      setGovernanceActions([]);
      return;
    }

    try {
      setLoadingGovernanceActions(true);
      const result = await getUserGovernanceActions(normalizedUserId, take);
      setGovernanceActions(result);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载经验治理留痕失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadActionsFailed'));
      setGovernanceActions([]);
    } finally {
      setLoadingGovernanceActions(false);
    }
  }, [t]);

  useEffect(() => {
    if (!loadedUserId) {
      setGovernanceActions([]);
      return;
    }

    void loadGovernanceActions(loadedUserId);
  }, [loadGovernanceActions, loadedUserId]);

  const loadTransactions = async (
    userId: string,
    targetPageIndex = transactionPageIndex,
    targetPageSize = transactionPageSize,
    targetExpType = transactionTypeFilter,
    targetStartDate = transactionStartDate,
    targetEndDate = transactionEndDate
  ) => {
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
      setTransactions(result.data);
      setTransactionTotal(result.dataCount);
      setTransactionPageIndex(result.page);
      setTransactionPageSize(result.pageSize);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验流水失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadTransactionsFailed'));
      setTransactions([]);
      setTransactionTotal(0);
    } finally {
      setLoadingTransactions(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId) {
      setTransactions([]);
      setTransactionTotal(0);
      setTransactionPageIndex(1);
      return;
    }

    void loadTransactions(loadedUserId, 1, transactionPageSize, transactionTypeFilter, transactionStartDate, transactionEndDate);
    // Transaction refresh is driven by explicit ledger filters; the loader also serves pagination callbacks with current defaults.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadedUserId, transactionTypeFilter, transactionStartDate, transactionEndDate]);

  const clearLoadedExperience = (userId: string) => {
    setLoadedUserId(null);
    setExperience(null);
    setDailyStatsWindow(null);
    setTransactions([]);
    setTransactionTotal(0);
    setTransactionPageIndex(1);
    setTransactionTypeFilter(undefined);
    setTransactionStartDate(null);
    setTransactionEndDate(null);
    setTransactionReviewHint(null);
    setGovernanceActions([]);
    setReviewContextDraft(null);
    reviewForm.resetFields();
    form.setFieldValue('userId', userId);
    freezeForm.setFieldsValue({
      userId,
      reason: '',
      frozenUntil: undefined,
    });
  };

  const loadExperience = async (
    userIdOverride?: string,
    options?: { showInvalidMessage?: boolean }
  ) => {
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
      clearLoadedExperience(userId);
      const result = await getUserExperience(userId);
      setLoadedUserId(String(userId));
      setExperience(result);
      form.setFieldValue('userId', userId);
      freezeForm.setFieldsValue({
        userId,
        reason: result.voFrozenReason || '',
        frozenUntil: result.voFrozenUntil ? dayjs(result.voFrozenUntil) : undefined,
      });
      reviewForm.resetFields();
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.loadUserFailed'));
    } finally {
      setLoadingExperience(false);
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
      userId: loadedUserId,
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
    if (!loadedUserId) {
      message.error(t('experience.feedback.queryFirst'));
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
      await adminRecordGovernanceReview({
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
      });

      message.success(t('experience.feedback.reviewRecorded'));
      await loadGovernanceActions(normalizedUserId);
      reviewForm.resetFields();
      setReviewContextDraft(null);
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '记录经验治理复核结论失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.reviewFailed'));
    } finally {
      setReviewing(false);
    }
  };

  const handleAdjust = async () => {
    try {
      const values = await form.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setSubmitting(true);
      setTransactionPageIndex(1);
      await adminAdjustExperience({
        userId: normalizedUserId,
        deltaExp: values.deltaExp,
        reason: values.reason,
      });

      message.success(t('experience.feedback.adjusted'));
      await loadExperience(normalizedUserId, { showInvalidMessage: false });
      form.setFieldsValue({ deltaExp: 0, reason: '' });
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '调整经验失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.adjustFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleFreeze = async () => {
    try {
      const values = await freezeForm.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setFreezing(true);
      setTransactionPageIndex(1);
      await adminFreezeExperience({
        userId: normalizedUserId,
        reason: values.reason.trim(),
        frozenUntil: values.frozenUntil ? values.frozenUntil.toDate().toISOString() : undefined,
      });

      message.success(t('experience.feedback.frozen'));
      await loadExperience(normalizedUserId, { showInvalidMessage: false });
      await loadGovernanceActions(normalizedUserId);
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '冻结经验失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.freezeFailed'));
    } finally {
      setFreezing(false);
    }
  };

  const handleUnfreeze = async () => {
    try {
      const values = await freezeForm.validateFields(['userId']);
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error(t('experience.form.userIdInvalid'));
        return;
      }

      setUnfreezing(true);
      setTransactionPageIndex(1);
      await adminUnfreezeExperience(normalizedUserId);

      message.success(t('experience.feedback.unfrozen'));
      await loadExperience(normalizedUserId, { showInvalidMessage: false });
      await loadGovernanceActions(normalizedUserId);
      freezeForm.setFieldsValue({
        userId: normalizedUserId,
        reason: '',
        frozenUntil: undefined,
      });
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '解冻经验失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'experience.feedback.unfreezeFailed'));
    } finally {
      setUnfreezing(false);
    }
  };

  const handleRecalculate = async () => {
    try {
      setRecalculating(true);
      const result = await recalculateLevelConfigs();
      setLevels(result);
      message.success(t('experience.feedback.recalculated'));
    } catch (error) {
      log.error('ExperienceAdminPage', '重算等级配置失败:', error);
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
          value={formatConsoleInteger(governanceActions.length, i18n.resolvedLanguage)}
          description={t('experience.metrics.actionsDescription')}
        />
      </ConsoleMetricGrid>

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
          <p>{governanceActions.length > 0 ? t('experience.flow.reviewReady', { count: governanceActions.length }) : t('experience.flow.reviewPending')}</p>
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
              setTransactionReviewHint(null);
              setTransactionTypeFilter(value);
            }}
            onTransactionStartDateChange={(value) => {
              setTransactionReviewHint(null);
              setTransactionStartDate(value);
            }}
            onTransactionEndDateChange={(value) => {
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
            canFreeze={canFreeze}
            reviewing={reviewing}
            governanceActions={governanceActions}
            loadingGovernanceActions={loadingGovernanceActions}
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
            canAdjust={canAdjust}
            canFreeze={canFreeze}
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
        canRecalculate={canRecalculate}
        recalculating={recalculating}
        onRecalculate={handleRecalculate}
      />
    </div>
  );
};
