import { useEffect, useState } from 'react';
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
  type TableColumnsType,
} from '@radish/ui';
import { ReloadOutlined } from '@radish/ui';
import {
  adminAdjustExperience,
  type ExpTransactionVo,
  adminFreezeExperience,
  adminUnfreezeExperience,
  getLevelConfigs,
  type UserExpAnomalyRuleSummaryVo,
  getUserDailyStats,
  getUserTransactions,
  type UserExpGovernanceRecommendationVo,
  type UserExpDailyLimitSnapshotVo,
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
import '../adminFeature.css';

interface FreezeFormValues {
  userId: string;
  reason: string;
  frozenUntil?: Dayjs;
}

interface AdjustFormValues {
  userId: string;
  deltaExp: number;
  reason?: string;
}

type StatsWindowDays = 7 | 30;

const EXPERIENCE_TRANSACTION_TYPE_OPTIONS = [
  { label: '管理员调整', value: 'ADMIN_ADJUST' },
  { label: '惩罚扣减', value: 'PENALTY' },
  { label: '发布帖子', value: 'POST_CREATE' },
  { label: '发布评论', value: 'COMMENT_CREATE' },
  { label: '点赞互动', value: 'LIKE_OTHERS' },
  { label: '被点赞', value: 'RECEIVE_LIKE' },
  { label: '高亮奖励', value: 'GOD_COMMENT' },
  { label: '登录奖励', value: 'DAILY_LOGIN' },
];

function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function isFormValidationError(error: unknown): error is { errorFields: unknown[] } {
  return typeof error === 'object' && error !== null && 'errorFields' in error;
}

function formatStatDate(value: string): string {
  return dayjs(value).format('MM-DD');
}

function formatFullStatDate(value: string): string {
  return dayjs(value).format('YYYY-MM-DD');
}

function formatDisplayTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  return dayjs(value).isValid() ? dayjs(value).format('YYYY-MM-DD HH:mm:ss') : value;
}

function formatTransactionAmount(value: number): string {
  return `${value >= 0 ? '+' : ''}${value}`;
}

function getRecommendationTagColor(level?: UserExpGovernanceRecommendationVo['voLevel']): 'success' | 'warning' | 'error' | 'default' {
  switch (level) {
    case 'freeze-suggest':
      return 'error';
    case 'review':
      return 'warning';
    case 'normal':
      return 'success';
    default:
      return 'default';
  }
}

function getRuleSeverityTagColor(severity?: UserExpAnomalyRuleSummaryVo['voSeverity']): 'success' | 'warning' | 'error' | 'default' {
  switch (severity) {
    case 'freeze-suggest':
      return 'error';
    case 'review':
      return 'warning';
    case 'observe':
      return 'default';
    default:
      return 'default';
  }
}

function getRuleSeverityLabel(severity?: UserExpAnomalyRuleSummaryVo['voSeverity']): string {
  switch (severity) {
    case 'freeze-suggest':
      return '冻结建议';
    case 'review':
      return '人工复核';
    case 'observe':
      return '继续观察';
    default:
      return '继续观察';
  }
}

function formatLimitValue(
  value: number,
  limit: number,
  limits?: UserExpDailyLimitSnapshotVo | null
): string {
  if (!limits?.voDailyLimitEnabled || limit <= 0) {
    return String(value);
  }

  return `${value}/${limit}`;
}

export const ExperienceAdminPage = () => {
  useDocumentTitle('经验等级');

  const [queryUserId, setQueryUserId] = useState('');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [dailyStatsWindow, setDailyStatsWindow] = useState<UserExpDailyStatsWindowVo | null>(null);
  const [statsWindowDays, setStatsWindowDays] = useState<StatsWindowDays>(7);
  const [levels, setLevels] = useState<LevelConfigVo[]>([]);
  const [transactions, setTransactions] = useState<ExpTransactionVo[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionPageIndex, setTransactionPageIndex] = useState(1);
  const [transactionPageSize, setTransactionPageSize] = useState(10);
  const [transactionTypeFilter, setTransactionTypeFilter] = useState<string | undefined>();
  const [loadingExperience, setLoadingExperience] = useState(false);
  const [loadingDailyStats, setLoadingDailyStats] = useState(false);
  const [loadingLevels, setLoadingLevels] = useState(false);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [freezing, setFreezing] = useState(false);
  const [unfreezing, setUnfreezing] = useState(false);
  const [recalculating, setRecalculating] = useState(false);
  const [form] = Form.useForm<AdjustFormValues>();
  const [freezeForm] = Form.useForm<FreezeFormValues>();

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

  const loadTransactions = async (
    userId: string,
    targetPageIndex = transactionPageIndex,
    targetPageSize = transactionPageSize,
    targetExpType = transactionTypeFilter
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

    void loadTransactions(loadedUserId, 1, transactionPageSize, transactionTypeFilter);
  }, [loadedUserId, transactionTypeFilter]);

  const clearLoadedExperience = (userId: string) => {
    setLoadedUserId(null);
    setExperience(null);
    setDailyStatsWindow(null);
    setTransactions([]);
    setTransactionTotal(0);
    setTransactionPageIndex(1);
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
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验失败:', error);
      message.error(error instanceof Error ? error.message : '加载用户经验失败');
    } finally {
      setLoadingExperience(false);
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

  const levelColumns: TableColumnsType<LevelConfigVo> = [
    {
      title: '等级',
      key: 'level',
      width: 120,
      render: (_, record) => (
        <div>
          <div>L{record.voLevel}</div>
          <div style={{ color: '#8c8c8c' }}>{record.voLevelName}</div>
        </div>
      ),
    },
    {
      title: '累计经验',
      dataIndex: 'voExpCumulative',
      key: 'voExpCumulative',
      width: 140,
    },
    {
      title: '升级所需',
      dataIndex: 'voExpRequired',
      key: 'voExpRequired',
      width: 140,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => <Tag color={record.voIsEnabled ? 'success' : 'default'}>{record.voIsEnabled ? '启用' : '禁用'}</Tag>,
    },
    {
      title: '说明',
      key: 'description',
      render: (_, record) => record.voDescription || '-',
    },
  ];

  const dailyStatsColumns: TableColumnsType<UserExpDailyStatsVo> = [
    {
      title: '日期',
      key: 'date',
      width: 120,
      render: (_, record) => (
        <div>
          <div>{formatStatDate(record.voStatDate)}</div>
          <div style={{ color: '#8c8c8c' }}>{dayjs(record.voStatDate).format('ddd')}</div>
        </div>
      ),
    },
    {
      title: '总经验',
      dataIndex: 'voExpEarned',
      key: 'voExpEarned',
      width: 100,
      render: (value: number) => (
        <div>
          <div>{value}</div>
          {dailyLimits?.voDailyLimitEnabled && (
            <div style={{ color: '#8c8c8c' }}>
              上限 {formatLimitValue(value, dailyLimits.voMaxDailyExp, dailyLimits)}
            </div>
          )}
        </div>
      ),
    },
    {
      title: '来源拆分',
      key: 'sources',
      width: 360,
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <span>发帖 {formatLimitValue(record.voExpFromPost, dailyLimits?.voMaxExpFromPost ?? 0, dailyLimits)}</span>
          <span>评论 {formatLimitValue(record.voExpFromComment, dailyLimits?.voMaxExpFromComment ?? 0, dailyLimits)}</span>
          <span>点赞 {formatLimitValue(record.voExpFromLike, dailyLimits?.voMaxExpFromLike ?? 0, dailyLimits)}</span>
          <span>高亮 {formatLimitValue(record.voExpFromHighlight, dailyLimits?.voMaxExpFromHighlight ?? 0, dailyLimits)}</span>
          <span>登录 {formatLimitValue(record.voExpFromLogin, dailyLimits?.voMaxExpFromLogin ?? 0, dailyLimits)}</span>
        </div>
      ),
    },
    {
      title: '行为计数',
      key: 'counts',
      width: 320,
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <span>发帖 {record.voPostCount}</span>
          <span>评论 {record.voCommentCount}</span>
          <span>点赞 {record.voLikeGivenCount}</span>
          <span>被赞 {record.voLikeReceivedCount}</span>
        </div>
      ),
    },
    {
      title: '观察',
      key: 'observations',
      width: 360,
      render: (_, record) => {
        const observations = record.voObservations ?? [];
        const contextObservations = observations.filter((observation) => observation.voKind !== 'anomaly');
        const anomalyObservations = observations.filter((observation) => observation.voKind === 'anomaly');
        return (
          <div style={{ display: 'grid', gap: 10 }}>
            <div>
              <div style={{ color: '#8c8c8c', marginBottom: 6 }}>来源 / 状态</div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {contextObservations.length > 0 ? contextObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span style={{ color: '#8c8c8c' }}>-</span>}
              </div>
            </div>
            <div>
              <div style={{ color: '#8c8c8c', marginBottom: 6 }}>异常判定</div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {anomalyObservations.length > 0 ? anomalyObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span style={{ color: '#8c8c8c' }}>未命中</span>}
              </div>
            </div>
          </div>
        );
      },
    },
  ];

  const transactionColumns: TableColumnsType<ExpTransactionVo> = [
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatDisplayTime(value),
    },
    {
      title: '类型',
      key: 'type',
      width: 140,
      render: (_, record) => (
        <div>
          <div>{record.voExpTypeDisplay}</div>
          <div style={{ color: '#8c8c8c' }}>{record.voExpType}</div>
        </div>
      ),
    },
    {
      title: '变动量',
      dataIndex: 'voExpAmount',
      key: 'voExpAmount',
      width: 110,
      render: (value: number) => (
        <span style={{ color: value >= 0 ? '#389e0d' : '#cf1322', fontWeight: 600 }}>
          {formatTransactionAmount(value)}
        </span>
      ),
    },
    {
      title: '经验变化',
      key: 'expRange',
      width: 180,
      render: (_, record) => `${record.voExpBefore} -> ${record.voExpAfter}`,
    },
    {
      title: '等级变化',
      key: 'levelRange',
      width: 170,
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span>{`L${record.voLevelBefore} -> L${record.voLevelAfter}`}</span>
          {record.voLevelAfter > record.voLevelBefore ? <Tag color="success">升级</Tag> : null}
        </div>
      ),
    },
    {
      title: '操作者',
      key: 'operator',
      width: 180,
      render: (_, record) => (
        <div>
          <div>{record.voOperatorName || 'System'}</div>
          <div style={{ color: '#8c8c8c' }}>ID: {record.voOperatorId}</div>
        </div>
      ),
    },
    {
      title: '备注',
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (value?: string | null) => value || '-',
    },
  ];

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
              scroll={{ x: 1160 }}
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

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>经验流水</h3>
            <p className="admin-feature-subtle">回看该用户最近的经验变动、管理员操作痕迹与升级轨迹。</p>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Select
              value={transactionTypeFilter}
              style={{ width: 220 }}
              options={EXPERIENCE_TRANSACTION_TYPE_OPTIONS}
              allowClear
              placeholder="筛选经验类型"
              onChange={(value) => setTransactionTypeFilter(typeof value === 'string' && value.length > 0 ? value : undefined)}
              disabled={!loadedUserId}
            />
          </div>
        </div>

        {loadedUserId ? (
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
                void loadTransactions(loadedUserId, page, pageSize, transactionTypeFilter);
              },
            }}
            locale={{
              emptyText: loadingTransactions ? '经验流水加载中...' : '该用户暂无经验流水记录',
            }}
          />
        ) : (
          <div style={{ marginTop: 20, color: '#8c8c8c' }}>
            请先查询用户经验，再查看经验流水。
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

      <section className="admin-feature-card">
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
