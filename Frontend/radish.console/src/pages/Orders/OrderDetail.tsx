import { useEffect, useMemo, useState } from 'react';
import { Modal, Descriptions, Tag, Button, Space, AntInput as Input, message } from '@radish/ui';
import { SyncOutlined } from '@radish/ui';
import { adminGetOrder, getOrderStatusColor } from '../../api/shopApi';
import type { Order } from '../../api/types';

interface OrderDetailProps {
  visible: boolean;
  orderId?: string | number;
  fallbackOrder?: Order;
  reloadToken?: number;
  onClose: () => void;
  onRetry?: () => void;
  onViewUser?: (order: Order) => void;
  onViewProduct?: (order: Order) => void;
  onViewCoinTransaction?: (order: Order) => void;
  canRemark?: boolean;
  savingRemark?: boolean;
  onSaveRemark?: (remark: string) => void;
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('zh-CN');
}

export const OrderDetail = ({
  visible,
  orderId,
  fallbackOrder,
  reloadToken = 0,
  onClose,
  onRetry,
  onViewUser,
  onViewProduct,
  onViewCoinTransaction,
  canRemark = false,
  savingRemark = false,
  onSaveRemark,
}: OrderDetailProps) => {
  const [detailOrder, setDetailOrder] = useState<Order | undefined>(fallbackOrder);
  const [adminRemark, setAdminRemark] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setDetailOrder(fallbackOrder);
      setLoadError(null);
      return;
    }

    setDetailOrder(fallbackOrder);
  }, [fallbackOrder, visible]);

  useEffect(() => {
    if (!visible || !orderId) {
      return;
    }

    let cancelled = false;

    const loadOrder = async () => {
      try {
        setLoading(true);
        setLoadError(null);
        const result = await adminGetOrder(orderId);
        if (cancelled) {
          return;
        }

        setDetailOrder(result);
      } catch (error) {
        if (cancelled) {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : '加载订单详情失败';
        setLoadError(errorMessage);
        setDetailOrder((current) => current ?? fallbackOrder);
        message.error(errorMessage);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadOrder();

    return () => {
      cancelled = true;
    };
  }, [fallbackOrder, orderId, reloadToken, visible]);

  const currentOrder = detailOrder ?? fallbackOrder;

  useEffect(() => {
    if (!visible || !currentOrder) {
      return;
    }

    setAdminRemark(currentOrder.voAdminRemark ?? '');
  }, [currentOrder, visible]);

  const normalizedSavedRemark = useMemo(
    () => ((currentOrder?.voAdminRemark ?? '').trim()),
    [currentOrder?.voAdminRemark],
  );
  const normalizedEditingRemark = adminRemark.trim();
  const canSaveRemark = canRemark
    && !!onSaveRemark
    && normalizedEditingRemark !== normalizedSavedRemark
    && !savingRemark;

  return (
    <Modal
      title="订单详情"
      isOpen={visible}
      onClose={onClose}
      size="large"
      footer={
        <Space wrap>
          {currentOrder && onViewUser ? (
            <Button onClick={() => onViewUser(currentOrder)}>
              查看用户
            </Button>
          ) : null}
          {currentOrder && onViewProduct ? (
            <Button onClick={() => onViewProduct(currentOrder)}>
              查看商品
            </Button>
          ) : null}
          {currentOrder?.voCoinTransactionId && onViewCoinTransaction ? (
            <Button onClick={() => onViewCoinTransaction(currentOrder)}>
              查看扣款流水
            </Button>
          ) : null}
          {currentOrder?.voStatus === 'Failed' && onRetry ? (
            <Button variant="primary" onClick={onRetry}>
              <SyncOutlined />
              重试发放权益
            </Button>
          ) : null}
          {canRemark && onSaveRemark ? (
            <Button
              variant="primary"
              onClick={() => onSaveRemark(normalizedEditingRemark)}
              disabled={!canSaveRemark}
            >
              {savingRemark ? '保存中...' : '保存备注'}
            </Button>
          ) : null}
          <Button onClick={onClose}>关闭</Button>
        </Space>
      }
    >
      {!currentOrder ? (
        <div className="order-detail-empty">
          {loading ? '正在加载订单详情...' : loadError ?? '未找到订单详情'}
        </div>
      ) : (
        <>
          <Descriptions bordered column={2}>
            <Descriptions.Item label="订单号" span={2}>
              {currentOrder.voOrderNo}
            </Descriptions.Item>

            <Descriptions.Item label="订单状态">
              <Tag color={getOrderStatusColor(currentOrder.voStatus)}>
                {currentOrder.voStatusDisplay}
              </Tag>
            </Descriptions.Item>

            <Descriptions.Item label="创建时间">
              {formatDateTime(currentOrder.voCreateTime)}
            </Descriptions.Item>

            <Descriptions.Item label="支付时间">
              {formatDateTime(currentOrder.voPaidTime)}
            </Descriptions.Item>

            <Descriptions.Item label="完成时间">
              {formatDateTime(currentOrder.voCompletedTime)}
            </Descriptions.Item>

            <Descriptions.Item label="权益到期时间">
              {formatDateTime(currentOrder.voBenefitExpiresAt)}
            </Descriptions.Item>

            <Descriptions.Item label="取消时间">
              {formatDateTime(currentOrder.voCancelledTime)}
            </Descriptions.Item>

            <Descriptions.Item label="取消原因">
              {currentOrder.voCancelReason || '-'}
            </Descriptions.Item>

            <Descriptions.Item label="失败原因" span={2}>
              {currentOrder.voFailReason ? (
                <span style={{ color: '#ff4d4f' }}>{currentOrder.voFailReason}</span>
              ) : '-'}
            </Descriptions.Item>

            <Descriptions.Item label="用户 ID">
              {currentOrder.voUserId}
            </Descriptions.Item>

            <Descriptions.Item label="用户名">
              {currentOrder.voUserName || '未知'}
            </Descriptions.Item>

            <Descriptions.Item label="商品 ID">
              {currentOrder.voProductId}
            </Descriptions.Item>

            <Descriptions.Item label="商品名称">
              {currentOrder.voProductName}
            </Descriptions.Item>

            <Descriptions.Item label="商品类型">
              <Tag color="blue">{currentOrder.voProductTypeDisplay}</Tag>
            </Descriptions.Item>

            <Descriptions.Item label="数量">
              {currentOrder.voQuantity}
            </Descriptions.Item>

            <Descriptions.Item label="单价">
              <span style={{ color: '#ff4d4f', fontWeight: 'bold' }}>
                {currentOrder.voUnitPrice} 胡萝卜
              </span>
            </Descriptions.Item>

            <Descriptions.Item label="总价">
              <span style={{ color: '#ff4d4f', fontWeight: 'bold', fontSize: '16px' }}>
                {currentOrder.voTotalPrice} 胡萝卜
              </span>
            </Descriptions.Item>

            <Descriptions.Item label="扣款流水 ID" span={2}>
              {currentOrder.voCoinTransactionId || '-'}
            </Descriptions.Item>

            <Descriptions.Item label="有效期" span={2}>
              {currentOrder.voDurationDisplay || '-'}
            </Descriptions.Item>

            <Descriptions.Item label="用户备注" span={2}>
              {currentOrder.voUserRemark || '-'}
            </Descriptions.Item>
          </Descriptions>

          {loadError ? (
            <div className="order-detail-warning">
              {loadError}
            </div>
          ) : null}

          {canRemark ? (
            <div style={{ marginTop: 16 }}>
              <div style={{ marginBottom: 8, fontWeight: 600 }}>管理员备注</div>
              <Input.TextArea
                rows={4}
                maxLength={500}
                showCount
                value={adminRemark}
                onChange={(event) => setAdminRemark(event.target.value)}
                placeholder="补充处理结论、追查线索或人工处置说明"
                disabled={savingRemark}
              />
            </div>
          ) : null}
        </>
      )}
    </Modal>
  );
};
