import { useEffect, useRef, useState } from 'react';
import {
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
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
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

export const ExperienceAdminPage = () => {
  useDocumentTitle('经验等级');

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

  const loadLevels = async () => {
    try {
      setLoadingLevels(true);
      const result = await getLevelConfigs();
      setLevels(result);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载等级配置失败:', error);
      message.error('加载等级配置失败');
    } finally {
      setLoadingLevels(false);
    }
  };

  useEffect(() => {
    void loadLevels();
  }, []);

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
      message.error(error instanceof Error ? error.message : '加载用户经验统计失败');
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
  }, [loadedUserId, statsWindowDays]);

  const loadGovernanceActions = async (userId: string, take: number = 20) => {
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
      message.error(error instanceof Error ? error.message : '加载经验治理留痕失败');
      setGovernanceActions([]);
    } finally {
      setLoadingGovernanceActions(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId) {
      setGovernanceActions([]);
      return;
    }

    void loadGovernanceActions(loadedUserId);
  }, [loadedUserId]);

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
        startDate: targetStartDate ? targetStartDate.startOf('day').format('YYYY-MM-DD HH:mm:ss') : undefined,
        endDate: targetEndDate ? targetEndDate.endOf('day').format('YYYY-MM-DD HH:mm:ss') : undefined,
      });
      setTransactions(result.data);
      setTransactionTotal(result.dataCount);
      setTransactionPageIndex(result.page);
      setTransactionPageSize(result.pageSize);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验流水失败:', error);
      message.error(error instanceof Error ? error.message : '加载用户经验流水失败');
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
        message.error('请输入有效的用户 ID');
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
      message.error(error instanceof Error ? error.message : '加载用户经验失败');
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
    message.success('已带入复核结论草稿');
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
    message.success('已带入冻结原因');
  };

  const handleRuleReview = (rule: UserExpAnomalyRuleSummaryVo) => {
    applyTransactionReviewPreset({
      expType: getTransactionExpTypePresetForRuleCodes([rule.voRuleCode]),
      date: rule.voLatestHitDate ? dayjs(rule.voLatestHitDate) : null,
      hint: rule.voLatestHitDate
        ? `已按规则「${rule.voRuleLabel}」定位到 ${formatFullStatDate(rule.voLatestHitDate)} 的经验流水。`
        : `已按规则「${rule.voRuleLabel}」定位相关经验流水。`,
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
        hint: `已带入当前治理建议「${recommendation.voTitle}」作为复核上下文。`,
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
          ? `已带入规则「${rule.voRuleLabel}」和最近命中日 ${latestHitDate} 的复核上下文。`
          : `已带入规则「${rule.voRuleLabel}」的复核上下文。`,
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
      hint: `已定位到 ${formatFullStatDate(record.voStatDate)} 的经验流水，优先复核该日异常记录。`,
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
        hint: `已带入 ${formatFullStatDate(record.voStatDate)} 的异常命中记录作为复核上下文。`,
      },
    });
  };

  const handleRecordGovernanceReview = async () => {
    if (!loadedUserId) {
      message.error('请先查询用户经验');
      return;
    }

    try {
      const values = await reviewForm.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(loadedUserId);
      if (!normalizedUserId) {
        message.error('请输入有效的用户 ID');
        return;
      }

      setReviewing(true);
      await adminRecordGovernanceReview({
        userId: normalizedUserId,
        reviewResult: values.reviewResult,
        remark: values.remark.trim(),
        windowDays: reviewContextDraft?.windowDays,
        statDate: reviewContextDraft?.statDate
          ? dayjs(reviewContextDraft.statDate).startOf('day').format('YYYY-MM-DD HH:mm:ss')
          : undefined,
        ruleCodes: reviewContextDraft?.ruleCodes,
        ruleLabels: reviewContextDraft?.ruleLabels,
        recommendationLevel: reviewContextDraft?.recommendationLevel,
        recommendationReason: reviewContextDraft?.recommendationReason ?? undefined,
      });

      message.success('复核结论已记录');
      await loadGovernanceActions(normalizedUserId);
      reviewForm.resetFields();
      setReviewContextDraft(null);
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '记录经验治理复核结论失败:', error);
      message.error(error instanceof Error ? error.message : '记录复核结论失败');
    } finally {
      setReviewing(false);
    }
  };

  const handleAdjust = async () => {
    try {
      const values = await form.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error('请输入有效的用户 ID');
        return;
      }

      setSubmitting(true);
      setTransactionPageIndex(1);
      await adminAdjustExperience({
        userId: normalizedUserId,
        deltaExp: values.deltaExp,
        reason: values.reason,
      });

      message.success('经验调整成功');
      await loadExperience(normalizedUserId, { showInvalidMessage: false });
      form.setFieldsValue({ deltaExp: 0, reason: '' });
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '调整经验失败:', error);
      message.error(error instanceof Error ? error.message : '调整经验失败');
    } finally {
      setSubmitting(false);
    }
  };

  const handleFreeze = async () => {
    try {
      const values = await freezeForm.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error('请输入有效的用户 ID');
        return;
      }

      setFreezing(true);
      setTransactionPageIndex(1);
      await adminFreezeExperience({
        userId: normalizedUserId,
        reason: values.reason.trim(),
        frozenUntil: values.frozenUntil ? values.frozenUntil.format('YYYY-MM-DD HH:mm:ss') : undefined,
      });

      message.success('经验已冻结');
      await loadExperience(normalizedUserId, { showInvalidMessage: false });
      await loadGovernanceActions(normalizedUserId);
    } catch (error) {
      if (isFormValidationError(error)) {
        return;
      }

      log.error('ExperienceAdminPage', '冻结经验失败:', error);
      message.error(error instanceof Error ? error.message : '冻结经验失败');
    } finally {
      setFreezing(false);
    }
  };

  const handleUnfreeze = async () => {
    try {
      const values = await freezeForm.validateFields(['userId']);
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error('请输入有效的用户 ID');
        return;
      }

      setUnfreezing(true);
      setTransactionPageIndex(1);
      await adminUnfreezeExperience(normalizedUserId);

      message.success('经验已解冻');
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
      message.error(error instanceof Error ? error.message : '解冻经验失败');
    } finally {
      setUnfreezing(false);
    }
  };

  const handleRecalculate = async () => {
    try {
      setRecalculating(true);
      const result = await recalculateLevelConfigs();
      setLevels(result);
      message.success('等级配置已重新计算');
    } catch (error) {
      log.error('ExperienceAdminPage', '重算等级配置失败:', error);
      message.error('重算等级配置失败');
    } finally {
      setRecalculating(false);
    }
  };

  return (
    <div className="admin-feature-page">
      <ExperienceAdminHeader
        onRefresh={() => {
          void Promise.all([
            loadExperience(undefined, { showInvalidMessage: false }),
            loadLevels(),
          ]);
        }}
      />

      <div className="governance-workbench">
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
