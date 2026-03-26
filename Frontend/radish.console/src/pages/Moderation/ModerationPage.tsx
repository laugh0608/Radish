import { useEffect, useState } from 'react';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  Space,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { ReloadOutlined } from '@radish/ui';
import { getActionLogs, getReviewQueue, reviewReport, type ContentReportQueueItemVo, type UserModerationActionVo } from '@/api/moderationApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import './index.css';
import '../adminFeature.css';

const REVIEW_STATUS_OPTIONS = [
  { label: '全部状态', value: -1 },
  { label: '待审核', value: 0 },
  { label: '已通过', value: 1 },
  { label: '已驳回', value: 2 },
];

const ACTION_OPTIONS = [
  { label: '不处罚', value: 0 },
  { label: '禁言', value: 1 },
  { label: '封禁', value: 2 },
];

const renderReportStatus = (value: string) => {
  switch (value) {
    case 'Approved':
      return <Tag color="success">已通过</Tag>;
    case 'Rejected':
      return <Tag color="error">已驳回</Tag>;
    default:
      return <Tag color="processing">待审核</Tag>;
  }
};

const renderActionType = (value: string) => {
  switch (value) {
    case 'Mute':
      return <Tag color="orange">禁言</Tag>;
    case 'Ban':
      return <Tag color="red">封禁</Tag>;
    case 'Unmute':
      return <Tag color="green">解除禁言</Tag>;
    case 'Unban':
      return <Tag color="cyan">解除封禁</Tag>;
    default:
      return <Tag>无动作</Tag>;
  }
};

export const ModerationPage = () => {
  useDocumentTitle('内容治理');

  const [form] = Form.useForm();
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [queueItems, setQueueItems] = useState<ContentReportQueueItemVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [queuePageIndex, setQueuePageIndex] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState(-1);
  const [logItems, setLogItems] = useState<UserModerationActionVo[]>([]);
  const [logTotal, setLogTotal] = useState(0);
  const [logPageIndex, setLogPageIndex] = useState(1);
  const [logPageSize, setLogPageSize] = useState(10);
  const [logTargetUserId, setLogTargetUserId] = useState('');
  const [reviewingItem, setReviewingItem] = useState<ContentReportQueueItemVo | null>(null);
  const [submittingReview, setSubmittingReview] = useState(false);

  const canReview = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const loadQueue = async (targetPageIndex = queuePageIndex, targetPageSize = queuePageSize) => {
    try {
      setLoadingQueue(true);
      const page = await getReviewQueue({
        status: statusFilter,
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
      });

      setQueueItems(page.voItems);
      setQueueTotal(page.voTotal);
      setQueuePageIndex(page.voPageIndex);
      setQueuePageSize(page.voPageSize);
    } catch (error) {
      log.error('ModerationPage', '加载审核队列失败:', error);
      message.error('加载审核队列失败');
    } finally {
      setLoadingQueue(false);
    }
  };

  const loadLogs = async (targetPageIndex = logPageIndex, targetPageSize = logPageSize) => {
    try {
      setLoadingLogs(true);
      const targetUserId = Number(logTargetUserId);
      const page = await getActionLogs({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        targetUserId: Number.isFinite(targetUserId) && targetUserId > 0 ? targetUserId : undefined,
      });

      setLogItems(page.voItems);
      setLogTotal(page.voTotal);
      setLogPageIndex(page.voPageIndex);
      setLogPageSize(page.voPageSize);
    } catch (error) {
      log.error('ModerationPage', '加载治理动作日志失败:', error);
      message.error('加载治理动作日志失败');
    } finally {
      setLoadingLogs(false);
    }
  };

  useEffect(() => {
    void loadQueue(1, queuePageSize);
  }, [statusFilter, queuePageSize]);

  useEffect(() => {
    void loadLogs(1, logPageSize);
  }, [logPageSize]);

  const openReviewModal = (item: ContentReportQueueItemVo) => {
    setReviewingItem(item);
    form.setFieldsValue({
      isApproved: true,
      actionType: 0,
      durationHours: undefined,
      reviewRemark: '',
    });
  };

  const handleSubmitReview = async () => {
    if (!reviewingItem) {
      return;
    }

    try {
      const values = await form.validateFields();
      setSubmittingReview(true);
      await reviewReport({
        reportId: reviewingItem.voReportId,
        isApproved: values.isApproved,
        actionType: values.actionType,
        durationHours: values.actionType === 0 ? null : values.durationHours,
        reviewRemark: values.reviewRemark,
      });

      message.success('审核完成');
      setReviewingItem(null);
      await Promise.all([loadQueue(), loadLogs()]);
    } catch (error) {
      log.error('ModerationPage', '提交审核失败:', error);
      message.error('提交审核失败');
    } finally {
      setSubmittingReview(false);
    }
  };

  const queueColumns: TableColumnsType<ContentReportQueueItemVo> = [
    {
      title: '举报单',
      key: 'report',
      width: 110,
      render: (_, record) => `#${record.voReportId}`,
    },
    {
      title: '目标',
      key: 'target',
      width: 220,
      render: (_, record) => (
        <div>
          <div>{record.voTargetType} #{record.voTargetContentId}</div>
          <div style={{ color: '#8c8c8c' }}>{record.voTargetUserName || `用户 ${record.voTargetUserId}`}</div>
        </div>
      ),
    },
    {
      title: '举报人',
      key: 'reporter',
      width: 180,
      render: (_, record) => `${record.voReporterUserName} (#${record.voReporterUserId})`,
    },
    {
      title: '原因',
      key: 'reason',
      render: (_, record) => (
        <div>
          <div>{record.voReasonType}</div>
          {record.voReasonDetail ? <div style={{ color: '#8c8c8c' }}>{record.voReasonDetail}</div> : null}
        </div>
      ),
    },
    {
      title: '状态',
      key: 'status',
      width: 160,
      render: (_, record) => (
        <Space direction="vertical" size={4}>
          {renderReportStatus(record.voStatus)}
          {renderActionType(record.voReviewActionType)}
        </Space>
      ),
    },
    {
      title: '提交时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
    },
    {
      title: '操作',
      key: 'actions',
      width: 140,
      render: (_, record) => (
        record.voStatus === 'Pending' && canReview ? (
          <Button size="small" variant="primary" onClick={() => openReviewModal(record)}>
            审核
          </Button>
        ) : (
          <span style={{ color: '#8c8c8c' }}>{record.voReviewedByName || '已处理'}</span>
        )
      ),
    },
  ];

  const logColumns: TableColumnsType<UserModerationActionVo> = [
    {
      title: '动作单',
      key: 'voActionId',
      width: 110,
      render: (_, record) => `#${record.voActionId}`,
    },
    {
      title: '目标用户',
      key: 'targetUser',
      width: 180,
      render: (_, record) => record.voTargetUserName || `用户 ${record.voTargetUserId}`,
    },
    {
      title: '动作',
      key: 'actionType',
      width: 140,
      render: (_, record) => renderActionType(record.voActionType),
    },
    {
      title: '原因',
      dataIndex: 'voReason',
      key: 'voReason',
    },
    {
      title: '操作者',
      dataIndex: 'voOperatorUserName',
      key: 'voOperatorUserName',
      width: 140,
    },
    {
      title: '状态',
      key: 'voIsActive',
      width: 100,
      render: (_, record) => <Tag color={record.voIsActive ? 'processing' : 'default'}>{record.voIsActive ? '生效中' : '已结束'}</Tag>,
    },
    {
      title: '开始时间',
      dataIndex: 'voStartTime',
      key: 'voStartTime',
      width: 180,
    },
    {
      title: '结束时间',
      key: 'voEndTime',
      width: 180,
      render: (_, record) => record.voEndTime || '-',
    },
  ];

  return (
    <div className="admin-feature-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>内容治理</h2>
            <p className="admin-feature-subtle">举报队列、审核动作与治理日志统一在 Console 收口。</p>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void Promise.all([loadQueue(), loadLogs()]);
          }}>
            刷新
          </Button>
        </div>
      </section>

      <div className="admin-feature-banner">
        当前治理链路已统一接入帖子、评论、聊天室消息和商品举报，审核通过后可继续联动禁言或封禁动作。
      </div>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>举报审核队列</h3>
            <p className="admin-feature-subtle">按状态查看举报单，并在审核时直接联动治理动作。</p>
          </div>
          <Space wrap>
            <Select
              value={statusFilter}
              style={{ width: 160 }}
              options={REVIEW_STATUS_OPTIONS}
              onChange={(value) => setStatusFilter(value)}
            />
          </Space>
        </div>

        <Table<ContentReportQueueItemVo>
          rowKey="voReportId"
          columns={queueColumns}
          dataSource={queueItems}
          loading={loadingQueue}
          pagination={{
            current: queuePageIndex,
            pageSize: queuePageSize,
            total: queueTotal,
            showSizeChanger: true,
            showQuickJumper: true,
            onChange: (page, size) => {
              void loadQueue(page, size);
            },
          }}
          scroll={{ x: 1280 }}
        />
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>治理动作日志</h3>
            <p className="admin-feature-subtle">回看审核联动后实际落下的禁言/封禁动作。</p>
          </div>
          <Space wrap>
            <Input
              placeholder="按目标用户 ID 过滤"
              value={logTargetUserId}
              onChange={(event) => setLogTargetUserId(event.target.value)}
              style={{ width: 200 }}
            />
            <Button
              variant="primary"
              onClick={() => {
                void loadLogs(1, logPageSize);
              }}
            >
              查询
            </Button>
          </Space>
        </div>

        <Table<UserModerationActionVo>
          rowKey="voActionId"
          columns={logColumns}
          dataSource={logItems}
          loading={loadingLogs}
          pagination={{
            current: logPageIndex,
            pageSize: logPageSize,
            total: logTotal,
            showSizeChanger: true,
            onChange: (page, size) => {
              void loadLogs(page, size);
            },
          }}
          scroll={{ x: 1200 }}
        />
      </section>

      <Modal
        title={reviewingItem ? `审核举报单 #${reviewingItem.voReportId}` : '审核举报单'}
        open={!!reviewingItem}
        onOk={handleSubmitReview}
        onCancel={() => setReviewingItem(null)}
        confirmLoading={submittingReview}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item name="isApproved" label="审核结果" rules={[{ required: true, message: '请选择审核结果' }]}>
            <Select
              options={[
                { label: '通过举报', value: true },
                { label: '驳回举报', value: false },
              ]}
            />
          </Form.Item>

          <Form.Item name="actionType" label="治理动作" rules={[{ required: true, message: '请选择治理动作' }]}>
            <Select options={ACTION_OPTIONS} />
          </Form.Item>

          <Form.Item
            noStyle
            shouldUpdate={(prev, next) => prev.actionType !== next.actionType}
          >
            {({ getFieldValue }) => (
              <Form.Item
                name="durationHours"
                label="持续时长（小时）"
                rules={getFieldValue('actionType') > 0
                  ? [{ required: true, message: '请输入动作时长' }]
                  : []}
              >
                <InputNumber min={1} max={720} style={{ width: '100%' }} disabled={getFieldValue('actionType') === 0} />
              </Form.Item>
            )}
          </Form.Item>

          <Form.Item name="reviewRemark" label="审核备注">
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="补充审核说明或处理依据" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};
