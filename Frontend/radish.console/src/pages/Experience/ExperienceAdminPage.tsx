import { useEffect, useState } from 'react';
import {
  AntInput as Input,
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
  adminFreezeExperience,
  adminUnfreezeExperience,
  getLevelConfigs,
  getUserDailyStats,
  getUserExperience,
  recalculateLevelConfigs,
  type LevelConfigVo,
  type UserExpDailyStatsVo,
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

type ObservationTone = 'success' | 'processing' | 'warning' | 'default';

interface DailyStatObservation {
  label: string;
  color: ObservationTone;
}

interface DailyStatsSummary {
  totalExp: number;
  averageExp: number;
  peakDayExp: number;
  peakDayLabel: string;
  zeroGainDays: number;
  notices: string[];
}

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

function getDominantSource(stat: UserExpDailyStatsVo): DailyStatObservation | null {
  const sources: Array<{ label: string; value: number; color: ObservationTone }> = [
    { label: '发帖驱动', value: stat.voExpFromPost, color: 'success' },
    { label: '评论驱动', value: stat.voExpFromComment, color: 'processing' },
    { label: '点赞驱动', value: stat.voExpFromLike, color: 'warning' },
    { label: '高亮驱动', value: stat.voExpFromHighlight, color: 'warning' },
    { label: '登录驱动', value: stat.voExpFromLogin, color: 'default' },
  ];

  const dominant = sources.reduce((current, candidate) =>
    candidate.value > current.value ? candidate : current
  );

  return dominant.value > 0
    ? { label: dominant.label, color: dominant.color }
    : null;
}

function getDailyStatObservations(stat: UserExpDailyStatsVo): DailyStatObservation[] {
  if (stat.voExpEarned <= 0) {
    return [{ label: '零增长', color: 'default' }];
  }

  const observations: DailyStatObservation[] = [];
  const dominantSource = getDominantSource(stat);
  if (dominantSource) {
    observations.push(dominantSource);
  }

  if (stat.voExpEarned >= 20 && stat.voExpFromLike / stat.voExpEarned >= 0.6) {
    observations.push({ label: '点赞占比偏高', color: 'warning' });
  }

  if (stat.voExpEarned >= 30 && stat.voExpFromHighlight / stat.voExpEarned >= 0.5) {
    observations.push({ label: '高亮奖励集中', color: 'warning' });
  }

  return observations;
}

function buildDailyStatsSummary(stats: UserExpDailyStatsVo[]): DailyStatsSummary | null {
  if (stats.length === 0) {
    return null;
  }

  const totalExp = stats.reduce((sum, stat) => sum + stat.voExpEarned, 0);
  const averageExp = totalExp / stats.length;
  const zeroGainDays = stats.filter((stat) => stat.voExpEarned <= 0).length;
  const peakDay = stats.reduce((current, candidate) =>
    candidate.voExpEarned > current.voExpEarned ? candidate : current
  );

  const notices: string[] = [];
  if (zeroGainDays >= 3) {
    notices.push(`最近 ${stats.length} 天中有 ${zeroGainDays} 天没有经验增长。`);
  }

  const likeHeavyDays = stats.filter(
    (stat) => stat.voExpEarned >= 20 && stat.voExpFromLike / stat.voExpEarned >= 0.6
  ).length;
  if (likeHeavyDays > 0) {
    notices.push(`其中 ${likeHeavyDays} 天经验主要来自点赞，建议结合互动来源复核。`);
  }

  const highlightHeavyDays = stats.filter(
    (stat) => stat.voExpEarned >= 30 && stat.voExpFromHighlight / stat.voExpEarned >= 0.5
  ).length;
  if (highlightHeavyDays > 0) {
    notices.push(`其中 ${highlightHeavyDays} 天经验主要来自高亮评论，建议确认是否集中触发奖励。`);
  }

  if (notices.length === 0) {
    notices.push('最近窗口内经验分布整体平稳，暂未看到明显需要人工复核的集中模式。');
  }

  return {
    totalExp,
    averageExp,
    peakDayExp: peakDay.voExpEarned,
    peakDayLabel: formatFullStatDate(peakDay.voStatDate),
    zeroGainDays,
    notices,
  };
}

export const ExperienceAdminPage = () => {
  useDocumentTitle('经验等级');

  const [queryUserId, setQueryUserId] = useState('');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [dailyStats, setDailyStats] = useState<UserExpDailyStatsVo[]>([]);
  const [statsWindowDays, setStatsWindowDays] = useState<StatsWindowDays>(7);
  const [levels, setLevels] = useState<LevelConfigVo[]>([]);
  const [loadingExperience, setLoadingExperience] = useState(false);
  const [loadingDailyStats, setLoadingDailyStats] = useState(false);
  const [loadingLevels, setLoadingLevels] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [freezing, setFreezing] = useState(false);
  const [unfreezing, setUnfreezing] = useState(false);
  const [recalculating, setRecalculating] = useState(false);
  const [form] = Form.useForm<AdjustFormValues>();
  const [freezeForm] = Form.useForm<FreezeFormValues>();

  const canAdjust = usePermission(CONSOLE_PERMISSIONS.experienceAdjust);
  const canFreeze = usePermission(CONSOLE_PERMISSIONS.experienceFreeze);
  const canRecalculate = usePermission(CONSOLE_PERMISSIONS.experienceRecalculate);
  const dailyStatsSummary = buildDailyStatsSummary(dailyStats);

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
      setDailyStats([]);
      return;
    }

    try {
      setLoadingDailyStats(true);
      const result = await getUserDailyStats(normalizedUserId, days);
      setDailyStats(result);
    } catch (error) {
      log.error('ExperienceAdminPage', '加载用户经验统计失败:', error);
      message.error(error instanceof Error ? error.message : '加载用户经验统计失败');
      setDailyStats([]);
    } finally {
      setLoadingDailyStats(false);
    }
  };

  useEffect(() => {
    if (!loadedUserId) {
      setDailyStats([]);
      return;
    }

    void loadDailyStats(loadedUserId, statsWindowDays);
  }, [loadedUserId, statsWindowDays]);

  const clearLoadedExperience = (userId: string) => {
    setLoadedUserId(null);
    setExperience(null);
    setDailyStats([]);
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
    },
    {
      title: '来源拆分',
      key: 'sources',
      width: 360,
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <span>发帖 {record.voExpFromPost}</span>
          <span>评论 {record.voExpFromComment}</span>
          <span>点赞 {record.voExpFromLike}</span>
          <span>高亮 {record.voExpFromHighlight}</span>
          <span>登录 {record.voExpFromLogin}</span>
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
      width: 260,
      render: (_, record) => {
        const observations = getDailyStatObservations(record);
        return (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {observations.map((observation) => (
              <Tag key={observation.label} color={observation.color}>
                {observation.label}
              </Tag>
            ))}
          </div>
        );
      },
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
            <p className="admin-feature-subtle">查看最近 7 / 30 天经验来源与行为拆分，用于人工复核异常增长模式。</p>
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
                <strong>{dailyStatsSummary ? dailyStatsSummary.totalExp : '--'}</strong>
              </div>
              <div className="admin-feature-metric">
                <span>日均经验</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.averageExp.toFixed(1) : '--'}</strong>
              </div>
              <div className="admin-feature-metric">
                <span>峰值单日</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.peakDayExp : '--'}</strong>
                <div style={{ marginTop: 6, color: '#8c8c8c' }}>
                  {dailyStatsSummary ? dailyStatsSummary.peakDayLabel : '--'}
                </div>
              </div>
              <div className="admin-feature-metric">
                <span>零增长天数</span>
                <strong>{dailyStatsSummary ? dailyStatsSummary.zeroGainDays : '--'}</strong>
              </div>
            </div>

            {dailyStatsSummary && (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                {dailyStatsSummary.notices.map((notice) => (
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
