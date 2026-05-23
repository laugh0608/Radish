import { useEffect, useRef, useState } from 'react';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  DatePicker,
  Form,
  InputNumber,
  Table,
  Tag,
  message,
} from '@radish/ui';
import { ReloadOutlined } from '@radish/ui';
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
  EXPERIENCE_TRANSACTION_TYPE_OPTIONS,
  formatFullStatDate,
  formatLimitValue,
  getGovernanceReviewResultForRecommendationLevel,
  getGovernanceReviewResultForRuleSeverity,
  getRecommendationTagColor,
  getRuleSeverityLabel,
  getRuleSeverityTagColor,
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
import {
  createDailyStatsColumns,
  createGovernanceActionColumns,
  createLevelColumns,
  createTransactionColumns,
} from './experienceAdminColumns';
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

  const levelColumns = createLevelColumns();
  const dailyStatsColumns = createDailyStatsColumns({
    canFreeze,
    dailyLimits,
    onDayReview: handleDayReview,
    onDayGovernanceReview: handleDayGovernanceReview,
    onDayFreezeReason: handleDayFreezeReason,
  });
  const transactionColumns = createTransactionColumns();
  const governanceActionColumns = createGovernanceActionColumns();

  return (
    <div className="admin-feature-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>经验等级</h2>
            <p className="admin-feature-subtle">支持按用户查看经验等级、调经验，并回看当前等级配置。</p>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void Promise.all([
              loadExperience(undefined, { showInvalidMessage: false }),
              loadLevels(),
            ]);
          }}>
            刷新
          </Button>
        </div>
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>用户经验查询</h3>
            <p className="admin-feature-subtle">输入用户 ID 查看当前等级、总经验、下一级与冻结状态。</p>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Input
              placeholder="用户 ID"
              value={queryUserId}
              onChange={(event) => setQueryUserId(event.target.value)}
              onPressEnter={() => {
                void loadExperience(undefined, { showInvalidMessage: true });
              }}
              style={{ width: 220 }}
            />
            <Button variant="primary" onClick={() => {
              void loadExperience(undefined, { showInvalidMessage: true });
            }} disabled={loadingExperience}>
              {loadingExperience ? '查询中...' : '查询'}
            </Button>
          </div>
        </div>

        <div className="admin-feature-metrics" style={{ marginTop: 20 }}>
          <div style={{ marginBottom: 12, color: '#8c8c8c' }}>
            {loadedUserId && experience
              ? `当前展示：${experience.voUserName || '未命名用户'}（ID: ${loadedUserId}）`
              : '当前未加载用户经验数据'}
          </div>
          <div className="admin-feature-metric">
            <span>当前等级</span>
            <strong>{experience ? `${experience.voCurrentLevelName} (L${experience.voCurrentLevel})` : '--'}</strong>
          </div>
          <div className="admin-feature-metric">
            <span>总经验</span>
            <strong>{experience ? experience.voTotalExp : '--'}</strong>
          </div>
          <div className="admin-feature-metric">
            <span>距下一级</span>
            <strong>{experience ? experience.voExpToNextLevel : '--'}</strong>
          </div>
          <div className="admin-feature-metric">
            <span>状态</span>
            <strong>{experience ? (experience.voExpFrozen ? '经验冻结' : '正常') : '--'}</strong>
          </div>
        </div>
        {experience?.voExpFrozen && (
          <div style={{ marginTop: 16, display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
            <Tag color="warning">冻结中</Tag>
            <span>到期时间：{experience.voFrozenUntil || '永久冻结'}</span>
            <span>原因：{experience.voFrozenReason || '未填写'}</span>
          </div>
        )}
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>经验统计观察</h3>
            <p className="admin-feature-subtle">查看最近 7 / 30 天经验来源与异常判定，仅服务人工复核和冻结建议，不自动处罚。</p>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Button
              variant={statsWindowDays === 7 ? 'primary' : 'secondary'}
              disabled={!loadedUserId || loadingDailyStats}
              onClick={() => setStatsWindowDays(7)}
            >
              最近 7 天
            </Button>
            <Button
              variant={statsWindowDays === 30 ? 'primary' : 'secondary'}
              disabled={!loadedUserId || loadingDailyStats}
              onClick={() => setStatsWindowDays(30)}
            >
              最近 30 天
            </Button>
          </div>
        </div>

        {loadedUserId ? (
          <>
            <div className="admin-feature-metrics" style={{ marginTop: 20 }}>
              <div className="admin-feature-metric">
                <span>窗口总经验</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.voTotalExp : '--'}</strong>
              </div>
              <div className="admin-feature-metric">
                <span>日均经验</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.voAverageExp.toFixed(1) : '--'}</strong>
              </div>
              <div className="admin-feature-metric">
                <span>峰值单日</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.voPeakDayExp : '--'}</strong>
                <div style={{ marginTop: 6, color: '#8c8c8c' }}>
                  {dailyStatsSummary?.voPeakStatDate
                    ? formatFullStatDate(dailyStatsSummary.voPeakStatDate)
                    : '--'}
                </div>
              </div>
              <div className="admin-feature-metric">
                <span>零增长天数</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.voZeroGainDays : '--'}</strong>
              </div>
              <div className="admin-feature-metric">
                <span>异常命中天数</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.voReviewDays : '--'}</strong>
              </div>
            </div>

            {dailyLimits && (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                  <strong>当前经验阈值</strong>
                  <Tag color={dailyLimits.voDailyLimitEnabled ? 'success' : 'default'}>
                    {dailyLimits.voDailyLimitEnabled ? '每日上限启用中' : '每日上限已停用'}
                  </Tag>
                </div>
                <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 12 }}>
                  <span>总经验 {dailyLimits.voMaxDailyExp}</span>
                  <span>发帖 {dailyLimits.voMaxExpFromPost}</span>
                  <span>评论 {dailyLimits.voMaxExpFromComment}</span>
                  <span>点赞 {dailyLimits.voMaxExpFromLike}</span>
                  <span>高亮 {dailyLimits.voMaxExpFromHighlight}</span>
                  <span>登录 {dailyLimits.voMaxExpFromLogin}</span>
                </div>
              </div>
            )}

            {governanceRecommendation && (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                  <strong>当前治理建议</strong>
                  <Tag color={getRecommendationTagColor(governanceRecommendation.voLevel)}>
                    {governanceRecommendation.voTitle}
                  </Tag>
                </div>
                <div style={{ marginTop: 12 }}>{governanceRecommendation.voReason}</div>
                <div style={{ marginTop: 8, color: '#8c8c8c' }}>
                  建议动作：{governanceRecommendation.voSuggestedAction}
                </div>
                <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <Button
                    variant="secondary"
                    disabled={!canFreeze}
                    onClick={() => handleRecommendationGovernanceReview(governanceRecommendation)}
                  >
                    带入复核结论
                  </Button>
                  {governanceRecommendation.voLevel !== 'normal' && (
                    <Button
                      variant="secondary"
                      disabled={!canFreeze}
                      onClick={() => prefillFreezeReason(`经验异常待复核：${governanceRecommendation.voReason}`)}
                    >
                      带入冻结原因
                    </Button>
                  )}
                </div>
              </div>
            )}

            {anomalyRuleSummaries.length > 0 && (
              <div style={{ marginTop: 16 }}>
                <div style={{ marginBottom: 12, fontWeight: 600 }}>异常规则摘要</div>
                <div style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
                  gap: 12,
                }}>
                  {anomalyRuleSummaries.map((rule) => (
                    <div
                      key={rule.voRuleCode}
                      style={{
                        border: '1px solid rgba(5, 5, 5, 0.08)',
                        borderRadius: 12,
                        padding: 16,
                        background: '#fff',
                      }}
                    >
                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                        <strong>{rule.voRuleLabel}</strong>
                        <Tag color={getRuleSeverityTagColor(rule.voSeverity)}>
                          {getRuleSeverityLabel(rule.voSeverity)}
                        </Tag>
                      </div>
                      <div style={{ marginTop: 10, color: '#595959' }}>{rule.voThresholdDescription}</div>
                      <div style={{ marginTop: 10, display: 'grid', gap: 6 }}>
                        <span>窗口命中：{rule.voHitDays} 天</span>
                        <span>最强信号：{rule.voStrongestSignal}</span>
                        <span>最近命中：{rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate) : '-'}</span>
                      </div>
                      <div style={{ marginTop: 10, color: '#8c8c8c' }}>
                        建议动作：{rule.voSuggestedAction}
                      </div>
                      <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                        <Button onClick={() => handleRuleReview(rule)}>
                          查看流水
                        </Button>
                        <Button
                          variant="secondary"
                          disabled={!canFreeze}
                          onClick={() => handleRuleGovernanceReview(rule)}
                        >
                          带入复核结论
                        </Button>
                        <Button
                          variant="secondary"
                          disabled={!canFreeze}
                          onClick={() => handleRuleFreezeReason(rule)}
                        >
                          带入冻结原因
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {dailyStatsSummary && (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                {dailyStatsSummary.voNotices.map((notice) => (
                  <div key={notice}>{notice}</div>
                ))}
              </div>
            )}

            <Table<UserExpDailyStatsVo>
              rowKey={(record) => `${record.voUserId}-${record.voStatDate}`}
              columns={dailyStatsColumns}
              dataSource={dailyStats}
              loading={loadingDailyStats}
              pagination={false}
              scroll={{ x: 1520 }}
              style={{ marginTop: 20 }}
              locale={{
                emptyText: loadingDailyStats
                  ? '统计加载中...'
                  : `最近 ${statsWindowDays} 天暂无经验统计记录`,
              }}
            />
          </>
        ) : (
          <div style={{ marginTop: 20, color: '#8c8c8c' }}>
            请先查询用户经验，再查看最近 {statsWindowDays} 天的统计观察。
          </div>
        )}
      </section>

      <section className="admin-feature-card" ref={transactionSectionRef}>
        <div className="admin-feature-header">
          <div>
            <h3>经验流水</h3>
            <p className="admin-feature-subtle">回看该用户最近的经验变动、管理员操作痕迹与升级轨迹，并支持按异常日期 / 类型快速复核。</p>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Select
              value={transactionTypeFilter}
              style={{ width: 220 }}
              options={EXPERIENCE_TRANSACTION_TYPE_OPTIONS}
              allowClear
              placeholder="筛选经验类型"
              onChange={(value) => {
                setTransactionReviewHint(null);
                setTransactionTypeFilter(typeof value === 'string' && value.length > 0 ? value : undefined);
              }}
              disabled={!loadedUserId}
            />
            <DatePicker
              value={transactionStartDate}
              allowClear
              placeholder="开始日期"
              style={{ width: 160 }}
              disabled={!loadedUserId}
              onChange={(value) => {
                setTransactionReviewHint(null);
                setTransactionStartDate(value);
              }}
            />
            <DatePicker
              value={transactionEndDate}
              allowClear
              placeholder="结束日期"
              style={{ width: 160 }}
              disabled={!loadedUserId}
              onChange={(value) => {
                setTransactionReviewHint(null);
                setTransactionEndDate(value);
              }}
            />
            <Button
              disabled={!loadedUserId}
              onClick={() => {
                setTransactionPageIndex(1);
                setTransactionTypeFilter(undefined);
                setTransactionStartDate(null);
                setTransactionEndDate(null);
                setTransactionReviewHint(null);
              }}
            >
              清空筛选
            </Button>
          </div>
        </div>

        {loadedUserId ? (
          <>
            {transactionReviewHint && (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                {transactionReviewHint}
              </div>
            )}
            <Table<ExpTransactionVo>
              rowKey="voId"
              columns={transactionColumns}
              dataSource={transactions}
              loading={loadingTransactions}
              scroll={{ x: 1120 }}
              style={{ marginTop: 20 }}
              pagination={{
                current: transactionPageIndex,
                pageSize: transactionPageSize,
                total: transactionTotal,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => `共 ${total} 条`,
                onChange: (page, pageSize) => {
                  void loadTransactions(loadedUserId, page, pageSize, transactionTypeFilter, transactionStartDate, transactionEndDate);
                },
              }}
              locale={{
                emptyText: loadingTransactions ? '经验流水加载中...' : '该用户暂无经验流水记录',
              }}
            />
          </>
        ) : (
          <div style={{ marginTop: 20, color: '#8c8c8c' }}>
            请先查询用户经验，再查看经验流水。
          </div>
        )}
      </section>

      <section className="admin-feature-card" ref={reviewSectionRef}>
        <div className="admin-feature-header">
          <div>
            <h3>复核结论与留痕</h3>
            <p className="admin-feature-subtle">记录人工复核结论；冻结 / 解冻动作会自动写入治理留痕，便于后续回看。</p>
          </div>
        </div>

        {loadedUserId ? (
          <>
            <div style={{ marginTop: 16, color: '#8c8c8c' }}>
              当前目标：{experience?.voUserName || '未命名用户'}（ID: {loadedUserId}）
            </div>

            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
              {reviewContextDraft ? (
                <>
                  <div>{reviewContextDraft.hint}</div>
                  {reviewContextDraft.recommendationReason && (
                    <div style={{ marginTop: 8, color: '#8c8c8c' }}>
                      建议原因快照：{reviewContextDraft.recommendationReason}
                    </div>
                  )}
                </>
              ) : '可从上方治理建议、规则摘要或每日异常一键带入复核草稿，也可直接手动填写结论。'}
            </div>

            <Form form={reviewForm} layout="vertical" className="admin-feature-form" style={{ marginTop: 20 }}>
              <Form.Item
                name="reviewResult"
                label="复核结论"
                rules={[{ required: true, message: '请选择复核结论' }]}
              >
                <Select
                  placeholder="选择复核结论"
                  options={[
                    { label: '已复核，未见异常', value: 'NoIssue' },
                    { label: '已复核，继续观察', value: 'Observe' },
                    { label: '已复核，可考虑冻结', value: 'FreezeSuggest' },
                  ]}
                  disabled={!canFreeze || reviewing}
                />
              </Form.Item>

              <Form.Item
                name="remark"
                label="复核备注"
                rules={[
                  { required: true, message: '请输入复核备注' },
                  { max: 500, message: '复核备注不能超过500个字符' },
                ]}
              >
                <Input.TextArea
                  rows={4}
                  maxLength={500}
                  showCount
                  placeholder="例如：已回看对应日期经验流水与目标内容，暂未发现异常；继续观察。"
                  disabled={!canFreeze || reviewing}
                />
              </Form.Item>

              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                <Button
                  variant="primary"
                  disabled={!canFreeze || reviewing}
                  onClick={() => {
                    void handleRecordGovernanceReview();
                  }}
                >
                  {reviewing ? '记录中...' : '记录复核结论'}
                </Button>
                <Button
                  disabled={reviewing}
                  onClick={() => {
                    reviewForm.resetFields();
                    setReviewContextDraft(null);
                  }}
                >
                  清空复核草稿
                </Button>
              </div>
            </Form>

            <div style={{ marginTop: 24, fontWeight: 600 }}>最近治理留痕</div>
            <Table<UserExperienceGovernanceActionVo>
              rowKey="voActionId"
              columns={governanceActionColumns}
              dataSource={governanceActions}
              loading={loadingGovernanceActions}
              pagination={false}
              scroll={{ x: 1280 }}
              style={{ marginTop: 16 }}
              locale={{
                emptyText: loadingGovernanceActions ? '治理留痕加载中...' : '该用户暂无治理留痕',
              }}
            />
          </>
        ) : (
          <div style={{ marginTop: 20, color: '#8c8c8c' }}>
            请先查询用户经验，再记录复核结论或查看治理留痕。
          </div>
        )}
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>管理员调经验</h3>
            <p className="admin-feature-subtle">正数表示补发经验，负数表示扣减经验。</p>
          </div>
        </div>

        <Form form={form} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label="用户 ID"
            rules={[
              { required: true, message: '请输入用户 ID' },
              { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' }
            ]}
          >
            <Input style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item
            name="deltaExp"
            label="经验变动量"
            rules={[{ required: true, message: '请输入经验变动量' }]}
          >
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item name="reason" label="调整原因">
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="例如：补偿、回收、活动奖励" />
          </Form.Item>

          <div>
            <Button variant="primary" disabled={!canAdjust || submitting} onClick={() => {
              void handleAdjust();
            }}>
              {submitting ? '提交中...' : '提交调整'}
            </Button>
          </div>
        </Form>
      </section>

      <section className="admin-feature-card" ref={freezeSectionRef}>
        <div className="admin-feature-header">
          <div>
            <h3>冻结 / 解冻经验</h3>
            <p className="admin-feature-subtle">可设置临时冻结或永久冻结；冻结中的用户不会继续累计经验，也不会参与经验排行榜。</p>
          </div>
        </div>

        <Form form={freezeForm} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label="用户 ID"
            rules={[
              { required: true, message: '请输入用户 ID' },
              { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' }
            ]}
          >
            <Input style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item name="frozenUntil" label="冻结到期时间">
            <DatePicker
              showTime
              allowClear
              style={{ width: '100%' }}
              placeholder="留空表示永久冻结"
            />
          </Form.Item>

          <Form.Item
            name="reason"
            label="冻结原因"
            rules={[{ required: true, message: '请输入冻结原因' }]}
          >
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="例如：异常刷经验、待人工复核" />
          </Form.Item>

          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Button variant="primary" disabled={!canFreeze || freezing} onClick={() => {
              void handleFreeze();
            }}>
              {freezing ? '冻结中...' : '提交冻结'}
            </Button>
            <Button disabled={!canFreeze || unfreezing || !experience?.voExpFrozen} onClick={() => {
              void handleUnfreeze();
            }}>
              {unfreezing ? '解冻中...' : '解除冻结'}
            </Button>
          </div>
        </Form>
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>等级配置</h3>
            <p className="admin-feature-subtle">查看当前等级曲线，必要时重新按后端配置重算。</p>
          </div>
          <Button variant="primary" disabled={!canRecalculate || recalculating} onClick={() => {
            void handleRecalculate();
          }}>
            {recalculating ? '重算中...' : '重算等级配置'}
          </Button>
        </div>

        <Table<LevelConfigVo>
          rowKey="voLevel"
          columns={levelColumns}
          dataSource={levels}
          loading={loadingLevels}
          pagination={false}
          scroll={{ x: 960 }}
        />
      </section>
    </div>
  );
};
