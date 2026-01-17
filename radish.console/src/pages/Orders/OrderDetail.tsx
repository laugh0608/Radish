import { Modal, Descriptions, Tag, Button, Space } from '@radish/ui';
import { SyncOutlined } from '@radish/ui';
import {
  getOrderStatusDisplay,
  getOrderStatusColor,
  getProductTypeDisplay,
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
      open={visible}
      onCancel={onClose}
      width={800}
      footer={
        <Space>
          {order.status === 5 && onRetry && (
            <Button type="primary" icon={<SyncOutlined />} onClick={onRetry}>
              重试发放权益
            </Button>
          )}
          <Button onClick={onClose}>关闭</Button>
        </Space>
      }
    >
      <Descriptions bordered column={2}>
        <Descriptions.Item label="订单号" span={2}>
          {order.orderNo}
        </Descriptions.Item>

        <Descriptions.Item label="订单状态">
          <Tag color={getOrderStatusColor(order.status)}>
            {order.statusDisplay}
          </Tag>
        </Descriptions.Item>

        <Descriptions.Item label="创建时间">
          {new Date(order.createTime).toLocaleString('zh-CN')}
        </Descriptions.Item>

        {order.paidTime && (
          <Descriptions.Item label="支付时间" span={2}>
            {new Date(order.paidTime).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.completedTime && (
          <Descriptions.Item label="完成时间" span={2}>
            {new Date(order.completedTime).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.cancelledTime && (
          <>
            <Descriptions.Item label="取消时间">
              {new Date(order.cancelledTime).toLocaleString('zh-CN')}
            </Descriptions.Item>
            <Descriptions.Item label="取消原因">
              {order.cancelReason || '-'}
            </Descriptions.Item>
          </>
        )}

        {order.failReason && (
          <Descriptions.Item label="失败原因" span={2}>
            <span style={{ color: '#ff4d4f' }}>{order.failReason}</span>
          </Descriptions.Item>
        )}

        <Descriptions.Item label="用户 ID">
          {order.userId}
        </Descriptions.Item>

        <Descriptions.Item label="用户名">
          {order.userName || '未知'}
        </Descriptions.Item>

        <Descriptions.Item label="商品 ID">
          {order.productId}
        </Descriptions.Item>

        <Descriptions.Item label="商品名称">
          {order.productName}
        </Descriptions.Item>

        <Descriptions.Item label="商品类型">
          <Tag color="blue">{order.productTypeDisplay}</Tag>
        </Descriptions.Item>

        <Descriptions.Item label="数量">
          {order.quantity}
        </Descriptions.Item>

        <Descriptions.Item label="单价">
          <span style={{ color: '#ff4d4f', fontWeight: 'bold' }}>
            {order.unitPrice} 胡萝卜
          </span>
        </Descriptions.Item>

        <Descriptions.Item label="总价">
          <span style={{ color: '#ff4d4f', fontWeight: 'bold', fontSize: '16px' }}>
            {order.totalPrice} 胡萝卜
          </span>
        </Descriptions.Item>

        {order.benefitExpiresAt && (
          <Descriptions.Item label="权益到期时间" span={2}>
            {new Date(order.benefitExpiresAt).toLocaleString('zh-CN')}
          </Descriptions.Item>
        )}

        {order.durationDisplay && (
          <Descriptions.Item label="有效期" span={2}>
            {order.durationDisplay}
          </Descriptions.Item>
        )}
      </Descriptions>
    </Modal>
  );
};
