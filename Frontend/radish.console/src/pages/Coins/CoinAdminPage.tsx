import { useState } from 'react';
import {
  AntInput as Input,
  Button,
  Form,
  InputNumber,
  message,
  Tag,
} from '@radish/ui';
import { ReloadOutlined, SearchOutlined, WalletOutlined } from '@radish/ui';
import { adminAdjustBalance, getBalanceByUserId, type UserBalanceVo } from '@/api/coinAdminApi';
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

interface CoinAdjustFormValues {
  userId: string;
  deltaAmount: number;
  reason: string;
}

export const CoinAdminPage = () => {
  useDocumentTitle('胡萝卜管理');

  const [queryUserId, setQueryUserId] = useState('');
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<CoinAdjustFormValues>();
  const canAdjust = usePermission(CONSOLE_PERMISSIONS.coinsAdjust);

  const loadBalance = async () => {
    const userId = normalizePositiveLongIdInput(queryUserId);
    if (!userId) {
      message.error('请输入有效的用户 ID');
      return;
    }

    try {
      setLoading(true);
      const result = await getBalanceByUserId(userId);
      setBalance(result);
      form.setFieldValue('userId', userId);
    } catch (error) {
      log.error('CoinAdminPage', '加载用户余额失败:', error);
      message.error('加载用户余额失败');
    } finally {
      setLoading(false);
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
      await adminAdjustBalance({
        userId: normalizedUserId,
        deltaAmount: values.deltaAmount,
        reason: values.reason,
      });

      message.success('胡萝卜余额调整成功');
      setQueryUserId(normalizedUserId);
      await loadBalance();
      form.setFieldsValue({ deltaAmount: 0, reason: '' });
    } catch (error) {
      log.error('CoinAdminPage', '调整胡萝卜余额失败:', error);
      message.error('调整胡萝卜余额失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="admin-feature-page coin-admin-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <WalletOutlined /> 胡萝卜管理
            </h2>
            <p className="admin-feature-subtle">支持按用户查询余额，并执行正负向调账。</p>
          </div>
          <div className="coin-admin-header-actions">
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadBalance();
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
            <p className="admin-feature-subtle">输入用户 ID 查询当前胡萝卜余额、冻结额和累计收支。</p>
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
                  { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' }
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
