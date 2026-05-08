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
  getUserExperience,
  recalculateLevelConfigs,
  type LevelConfigVo,
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

function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function isFormValidationError(error: unknown): error is { errorFields: unknown[] } {
  return typeof error === 'object' && error !== null && 'errorFields' in error;
}

export const ExperienceAdminPage = () => {
  useDocumentTitle('经验等级');

  const [queryUserId, setQueryUserId] = useState('');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [levels, setLevels] = useState<LevelConfigVo[]>([]);
  const [loadingExperience, setLoadingExperience] = useState(false);
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

  const clearLoadedExperience = (userId: string) => {
    setLoadedUserId(null);
    setExperience(null);
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
