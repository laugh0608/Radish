import { useState } from 'react';
import {
  AntInput as Input,
  Button,
  Form,
  InputNumber,
  message,
} from '@radish/ui';
import { ReloadOutlined } from '@radish/ui';
import { adminAdjustBalance, getBalanceByUserId, type UserBalanceVo } from '@/api/coinAdminApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import '../adminFeature.css';

export const CoinAdminPage = () => {
  useDocumentTitle('胡萝卜管理');

  const [queryUserId, setQueryUserId] = useState('');
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm();
  const canAdjust = usePermission(CONSOLE_PERMISSIONS.coinsAdjust);

  const loadBalance = async () => {
    const userId = Number(queryUserId);
    if (!Number.isFinite(userId) || userId <= 0) {
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
      setSubmitting(true);
      await adminAdjustBalance({
        userId: values.userId,
        deltaAmount: values.deltaAmount,
        reason: values.reason,
      });

      message.success('胡萝卜余额调整成功');
      setQueryUserId(String(values.userId));
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
    <div className="admin-feature-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>胡萝卜管理</h2>
            <p className="admin-feature-subtle">支持按用户查询余额，并执行正负向调账。</p>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void loadBalance();
          }}>
            刷新当前用户
          </Button>
        </div>
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>用户余额查询</h3>
            <p className="admin-feature-subtle">输入用户 ID 查询当前胡萝卜余额、冻结额和累计收支。</p>
          </div>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Input
              placeholder="用户 ID"
              value={queryUserId}
              onChange={(event) => setQueryUserId(event.target.value)}
              style={{ width: 220 }}
            />
            <Button variant="primary" onClick={() => {
              void loadBalance();
            }} disabled={loading}>
              {loading ? '查询中...' : '查询'}
            </Button>
          </div>
        </div>

        <div className="admin-feature-metrics" style={{ marginTop: 20 }}>
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
        </div>
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>管理员调账</h3>
            <p className="admin-feature-subtle">正数表示发放胡萝卜，负数表示扣减胡萝卜。</p>
          </div>
        </div>

        <Form form={form} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label="用户 ID"
            rules={[{ required: true, message: '请输入用户 ID' }]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item
            name="deltaAmount"
            label="变动金额（胡萝卜）"
            rules={[{ required: true, message: '请输入变动金额' }]}
          >
            <InputNumber style={{ width: '100%' }} />
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
    </div>
  );
};
