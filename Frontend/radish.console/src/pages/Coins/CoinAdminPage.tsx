import { useState } from 'react';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  message,
  Tag,
  Table,
  type TableColumnsType,
} from '@radish/ui';
import { ReloadOutlined, SearchOutlined, WalletOutlined } from '@radish/ui';
import {
  adminAdjustBalance,
  getBalanceByUserId,
  getTransactionsByUserId,
  type CoinTransactionVo,
  type UserBalanceVo,
} from '@/api/coinAdminApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './CoinAdminPage.css';

function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function formatDisplayTime(time?: string | null): string {
  if (!time) return '-';

  const date = new Date(time);
  if (Number.isNaN(date.getTime())) {
    return time;
  }

  return date.toLocaleString('zh-CN');
}

interface CoinAdjustFormValues {
  userId: string;
  deltaAmount: number;
  reason: string;
}

const TRANSACTION_TYPE_OPTIONS = [
  { value: 'SYSTEM_GRANT', label: '系统赠送' },
  { value: 'LIKE_REWARD', label: '点赞奖励' },
  { value: 'COMMENT_REWARD', label: '评论奖励' },
  { value: 'TRANSFER', label: '用户转账' },
  { value: 'TIP', label: '打赏' },
  { value: 'CONSUME', label: '消费' },
  { value: 'REFUND', label: '退款' },
  { value: 'PENALTY', label: '惩罚扣除' },
  { value: 'ADMIN_ADJUST', label: '管理员调整' },
];

const TRANSACTION_STATUS_OPTIONS = [
  { value: 'PENDING', label: '待处理' },
  { value: 'SUCCESS', label: '成功' },
  { value: 'FAILED', label: '失败' },
];

export const CoinAdminPage = () => {
  useDocumentTitle('胡萝卜管理');

  const [queryUserId, setQueryUserId] = useState('');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [transactions, setTransactions] = useState<CoinTransactionVo[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionPageIndex, setTransactionPageIndex] = useState(1);
  const [transactionPageSize, setTransactionPageSize] = useState(10);
  const [transactionTypeFilter, setTransactionTypeFilter] = useState<string | undefined>();
  const [transactionStatusFilter, setTransactionStatusFilter] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);
  const [transactionLoading, setTransactionLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<CoinAdjustFormValues>();
  const canAdjust = usePermission(CONSOLE_PERMISSIONS.coinsAdjust);

  const getSignedCoinAmount = (transaction: CoinTransactionVo, userId: string) => {
    if (String(transaction.voFromUserId ?? '') === userId) {
      return -transaction.voAmount;
    }

    return transaction.voAmount;
  };

  const getSignedAmountClassName = (amount: number) => (
    amount >= 0
      ? 'coin-admin-signed-amount coin-admin-signed-amount--positive'
      : 'coin-admin-signed-amount coin-admin-signed-amount--negative'
  );

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'SUCCESS':
        return 'success';
      case 'FAILED':
        return 'error';
      case 'PENDING':
        return 'warning';
      default:
        return 'default';
    }
  };

  const loadTransactions = async (
    userId: string,
    pageIndex = transactionPageIndex,
    pageSize = transactionPageSize,
    transactionType = transactionTypeFilter,
    status = transactionStatusFilter
  ) => {
    try {
      setTransactionLoading(true);
      const result = await getTransactionsByUserId({
        userId,
        pageIndex,
        pageSize,
        transactionType,
        status,
      });
      setTransactions(result.data);
      setTransactionTotal(result.dataCount);
      setTransactionPageIndex(result.page);
      setTransactionPageSize(result.pageSize);
    } catch (error) {
      log.error('CoinAdminPage', '加载胡萝卜流水失败:', error);
      message.error('加载胡萝卜流水失败');
      setTransactions([]);
      setTransactionTotal(0);
    } finally {
      setTransactionLoading(false);
    }
  };

  const loadBalance = async (targetUserId?: string) => {
    const userId = targetUserId ?? normalizePositiveLongIdInput(queryUserId);
    if (!userId) {
      message.error('请输入有效的用户 ID');
      return;
    }

    try {
      setLoading(true);
      const result = await getBalanceByUserId(userId);
      setBalance(result);
      setLoadedUserId(userId);
      setQueryUserId(userId);
      form.setFieldValue('userId', userId);
    } catch (error) {
      log.error('CoinAdminPage', '加载用户余额失败:', error);
      message.error('加载用户余额失败');
      setBalance(null);
      setLoadedUserId(null);
      setTransactions([]);
      setTransactionTotal(0);
      return;
    } finally {
      setLoading(false);
    }

    await loadTransactions(userId, 1, transactionPageSize);
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
      await adminAdjustBalance({
        userId: normalizedUserId,
        deltaAmount: values.deltaAmount,
        reason: values.reason,
      });

      message.success('胡萝卜余额调整成功');
      setQueryUserId(normalizedUserId);
      await loadBalance(normalizedUserId);
      form.setFieldsValue({ deltaAmount: 0, reason: '' });
    } catch (error) {
      log.error('CoinAdminPage', '调整胡萝卜余额失败:', error);
      message.error('调整胡萝卜余额失败');
    } finally {
      setSubmitting(false);
    }
  };

  const handleTransactionTypeChange = (value: string | undefined) => {
    const nextValue = typeof value === 'string' && value.length > 0 ? value : undefined;
    setTransactionTypeFilter(nextValue);
    if (loadedUserId) {
      void loadTransactions(loadedUserId, 1, transactionPageSize, nextValue, transactionStatusFilter);
    }
  };

  const handleTransactionStatusChange = (value: string | undefined) => {
    const nextValue = typeof value === 'string' && value.length > 0 ? value : undefined;
    setTransactionStatusFilter(nextValue);
    if (loadedUserId) {
      void loadTransactions(loadedUserId, 1, transactionPageSize, transactionTypeFilter, nextValue);
    }
  };

  const clearTransactionFilters = () => {
    setTransactionTypeFilter(undefined);
    setTransactionStatusFilter(undefined);
    if (loadedUserId) {
      void loadTransactions(loadedUserId, 1, transactionPageSize, undefined, undefined);
    }
  };

  const transactionColumns: TableColumnsType<CoinTransactionVo> = [
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
    {
      title: '金额',
      dataIndex: 'voAmount',
      key: 'voAmount',
      width: 120,
      render: (_amount: number, record) => {
        const signedAmount = getSignedCoinAmount(record, loadedUserId ?? '');

        return (
          <span className={getSignedAmountClassName(signedAmount)}>
            {signedAmount >= 0 ? '+' : ''}{signedAmount}
          </span>
        );
      },
    },
    {
      title: '类型',
      dataIndex: 'voTransactionTypeDisplay',
      key: 'voTransactionTypeDisplay',
      width: 130,
    },
    {
      title: '状态',
      dataIndex: 'voStatus',
      key: 'voStatus',
      width: 100,
      render: (status: string, record) => (
        <Tag color={getStatusColor(status)}>
          {record.voStatusDisplay || status}
        </Tag>
      ),
    },
    {
      title: '操作人',
      dataIndex: 'voCreateBy',
      key: 'voCreateBy',
      width: 150,
      render: (operatorName: string, record) => (
        <span>{operatorName || record.voCreateId || '-'}</span>
      ),
    },
    {
      title: '业务',
      dataIndex: 'voBusinessType',
      key: 'voBusinessType',
      width: 140,
      render: (businessType: string | null | undefined, record) => {
        if (!businessType && !record.voBusinessId) return '-';

        return `${businessType || '-'}${record.voBusinessId ? ` #${record.voBusinessId}` : ''}`;
      },
    },
    {
      title: '流水号',
      dataIndex: 'voTransactionNo',
      key: 'voTransactionNo',
      width: 220,
    },
    {
      title: '备注',
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (remark?: string | null) => remark || '-',
    },
  ];

  return (
    <div className="admin-feature-page coin-admin-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <WalletOutlined /> 胡萝卜管理
            </h2>
            <p className="admin-feature-subtle">支持按用户查询余额、回看交易流水，并执行正负向调账。</p>
          </div>
          <div className="coin-admin-header-actions">
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadBalance(loadedUserId ?? undefined);
            }}>
              刷新当前用户
            </Button>
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="胡萝卜余额指标">
        <div className="admin-feature-metric">
          <span>可用余额</span>
          <strong>{balance ? `${balance.voBalance} 胡萝卜` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>冻结余额</span>
          <strong>{balance ? `${balance.voFrozenBalance} 胡萝卜` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>累计获得</span>
          <strong>{balance ? balance.voTotalEarned : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>累计消费</span>
          <strong>{balance ? balance.voTotalSpent : '--'}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label="用户余额查询">
            <div className="admin-table-toolbar__title">
              <span>用户余额查询</span>
              <Tag>{balance ? `用户 ${balance.voUserId}` : '未查询'}</Tag>
            </div>
            <p className="admin-feature-subtle">输入用户 ID 查询当前胡萝卜余额、冻结额、累计收支和最近交易流水。</p>
            <div className="admin-table-toolbar__filters">
              <Input
                className="coin-admin-query-input"
                placeholder="用户 ID"
                value={queryUserId}
                onChange={(event) => setQueryUserId(event.target.value)}
                onPressEnter={() => {
                  void loadBalance();
                }}
              />
              <Button
                variant="primary"
                icon={<SearchOutlined />}
                onClick={() => {
                  void loadBalance();
                }}
                disabled={loading}
              >
                {loading ? '查询中...' : '查询'}
              </Button>
            </div>
          </section>

          <section className="admin-table-panel">
            <div className="coin-admin-section-title">
              <div>
                <h3>管理员调账</h3>
                <p className="admin-feature-subtle">正数表示发放胡萝卜，负数表示扣减胡萝卜。</p>
              </div>
              <Tag>{canAdjust ? '可调账' : '无调账权限'}</Tag>
            </div>

            <Form form={form} layout="vertical" className="admin-feature-form">
              <Form.Item
                name="userId"
                label="用户 ID"
                rules={[
                  { required: true, message: '请输入用户 ID' },
                  { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' },
                ]}
              >
                <Input />
              </Form.Item>

              <Form.Item
                name="deltaAmount"
                label="变动金额（胡萝卜）"
                rules={[{ required: true, message: '请输入变动金额' }]}
              >
                <InputNumber className="coin-admin-full-width" />
              </Form.Item>

              <Form.Item
                name="reason"
                label="调整原因"
                rules={[{ required: true, message: '请输入调整原因' }]}
              >
                <Input.TextArea rows={4} maxLength={200} showCount placeholder="例如：运营补发、违规扣减" />
              </Form.Item>

              <div>
                <Button variant="primary" disabled={!canAdjust || submitting} onClick={() => {
                  void handleAdjust();
                }}>
                  {submitting ? '提交中...' : '提交调账'}
                </Button>
              </div>
            </Form>
          </section>

          <section className="admin-table-panel">
            <div className="coin-admin-section-title">
              <div>
                <h3>交易流水</h3>
                <p className="admin-feature-subtle">回看用户胡萝卜收支、管理员调账和关联业务，调账成功后会自动刷新。</p>
              </div>
              <Tag>{loadedUserId ? `用户 ${loadedUserId}` : '未查询'}</Tag>
            </div>

            <div className="coin-admin-transaction-filters">
              <Select
                value={transactionTypeFilter}
                allowClear
                placeholder="筛选交易类型"
                options={TRANSACTION_TYPE_OPTIONS}
                disabled={!loadedUserId}
                onChange={handleTransactionTypeChange}
              />
              <Select
                value={transactionStatusFilter}
                allowClear
                placeholder="筛选状态"
                options={TRANSACTION_STATUS_OPTIONS}
                disabled={!loadedUserId}
                onChange={handleTransactionStatusChange}
              />
              <Button disabled={!loadedUserId} onClick={clearTransactionFilters}>
                清空筛选
              </Button>
            </div>

            <Table<CoinTransactionVo>
              rowKey="voId"
              columns={transactionColumns}
              dataSource={transactions}
              loading={transactionLoading}
              scroll={{ x: 1220 }}
              style={{ marginTop: 20 }}
              pagination={{
                current: transactionPageIndex,
                pageSize: transactionPageSize,
                total: transactionTotal,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => `共 ${total} 条`,
                onChange: (page, size) => {
                  if (loadedUserId) {
                    void loadTransactions(loadedUserId, page, size);
                  }
                },
              }}
              locale={{
                emptyText: loadedUserId
                  ? (transactionLoading ? '胡萝卜流水加载中...' : '该用户暂无胡萝卜流水')
                  : '请先查询用户余额，再查看胡萝卜流水',
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>调账摘要</h3>
          <p className="admin-feature-subtle">用于核对当前查询对象和管理员调账权限。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">当前用户</span>
              <span className="admin-table-summary__value">{balance ? balance.voUserId : '未查询'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">累计转入</span>
              <span className="admin-table-summary__value">{balance ? balance.voTotalTransferredIn : '--'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">累计转出</span>
              <span className="admin-table-summary__value">{balance ? balance.voTotalTransferredOut : '--'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">调账权限</span>
              <span className="admin-table-summary__value">
                {canAdjust ? '可提交管理员调账' : '仅可查看余额'}
              </span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};
