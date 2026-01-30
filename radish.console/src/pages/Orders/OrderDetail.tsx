import { Modal, Descriptions, Tag, Button, Space } from '@radish/ui';
import { SyncOutlined } from '@radish/ui';
import {
  getOrderStatusColor,
} from '../../api/shopApi';
import type { Order } from '../../api/types';

interface OrderDetailProps {
  visible: boolean;
  order?: Order;
  onClose: () => void;
  onRetry?: () => void;
}

export const OrderDetail = ({ visible, order, onClose, onRetry }: OrderDetailProps) => {
  if (!order) {
    return null;
  }

  return (
    <Modal
      title="订单详情"
      isOpen={visible}
      onClose={onClose}
      size="large"
      footer={
        <Space>
          {order.voStatus === 'Failed' && onRetry && (
            <Button variant="primary" onClick={onRetry}>
              <SyncOutlined />
              重试发放权益
            </Button>
          )}
          <Button onClick={onClose}>关闭</Button>
        </Space>
      }
    >
      <Descriptions bordered column={2}>
        <Descriptions.Item label="订单号" span={2}>
          {order.voOrderNo}
        </Descriptions.Item>

        <Descriptions.Item label="订单状态">
          <Tag color={getOrderStatusColor(order.voStatus)}>
            {order.voStatusDisplay}
          </Tag>
        </Descriptions.Item>

        <Descriptions.Item label="创建时间">
          {new Date(order.voCreateTime).toLocaleString('zh-CN')}
        </Descriptions.Item>

        {order.voPaidTime && (
          <Descriptions.Item label="支付时间" span={2}>
            {new Date(order.voPaidTime).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.voCompletedTime && (
          <Descriptions.Item label="完成时间" span={2}>
            {new Date(order.voCompletedTime).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.voCancelledTime && (
          <>
            <Descriptions.Item label="取消时间">
              {new Date(order.voCancelledTime).toLocaleString('zh-CN')}
            </Descriptions.Item>
            <Descriptions.Item label="取消原因">
              {order.voCancelReason || '-'}
            </Descriptions.Item>
          </>
        )}

        {order.voFailReason && (
          <Descriptions.Item label="失败原因" span={2}>
            <span style={{ color: '#ff4d4f' }}>{order.voFailReason}</span>
          </Descriptions.Item>
        )}

        <Descriptions.Item label="用户 ID">
          {order.voUserId}
        </Descriptions.Item>

        <Descriptions.Item label="用户名">
          {order.voUserName || '未知'}
        </Descriptions.Item>

        <Descriptions.Item label="商品 ID">
          {order.voProductId}
        </Descriptions.Item>

        <Descriptions.Item label="商品名称">
          {order.voProductName}
        </Descriptions.Item>

        <Descriptions.Item label="商品类型">
          <Tag color="blue">{order.voProductTypeDisplay}</Tag>
        </Descriptions.Item>

        <Descriptions.Item label="数量">
          {order.voQuantity}
        </Descriptions.Item>

        <Descriptions.Item label="单价">
          <span style={{ color: '#ff4d4f', fontWeight: 'bold' }}>
            {order.voUnitPrice} 胡萝卜
          </span>
        </Descriptions.Item>

        <Descriptions.Item label="总价">
          <span style={{ color: '#ff4d4f', fontWeight: 'bold', fontSize: '16px' }}>
            {order.voTotalPrice} 胡萝卜
          </span>
        </Descriptions.Item>

        {order.voBenefitExpiresAt && (
          <Descriptions.Item label="权益到期时间" span={2}>
            {new Date(order.voBenefitExpiresAt).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.voDurationDisplay && (
          <Descriptions.Item label="有效期" span={2}>
            {order.voDurationDisplay}
          </Descriptions.Item>
        )}
      </Descriptions>
    </Modal>
  );
};
