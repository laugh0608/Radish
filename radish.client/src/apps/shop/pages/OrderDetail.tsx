import { useState } from 'react';
import { log } from '@/utils/logger';
import type { OrderData } from '@/utils/viewModelMapper';
import { getOrderStatusColor, OrderStatus } from '@/api/shop';
import styles from './OrderDetail.module.css';

interface OrderDetailProps {
  orderId: number;
  order: OrderData | null;
  loading: boolean;
  onBack: () => void;
  onCancelOrder: (orderId: number, reason?: string) => void;
}

export const OrderDetail = ({
  order,
  loading,
  onBack,
  onCancelOrder
}: OrderDetailProps) => {
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>加载中...</p>
        </div>
      </div>
    );
  }

  if (!order) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h2>订单不存在</h2>
          <p>您访问的订单可能已被删除或不存在</p>
          <button className={styles.backButton} onClick={onBack}>
            返回订单列表
          </button>
        </div>
      </div>
    );
  }

  const handleCancelClick = () => {
    setShowCancelDialog(true);
  };

  const handleConfirmCancel = async () => {
    setCancelling(true);
    try {
      await onCancelOrder(order.id, cancelReason || undefined);
      setShowCancelDialog(false);
      setCancelReason('');
    } catch (error) {
      log.error('取消订单失败:', error);
    } finally {
      setCancelling(false);
    }
  };

  const handleCloseCancelDialog = () => {
    setShowCancelDialog(false);
    setCancelReason('');
  };

  // 判断是否可以取消订单
  const canCancel = order.status === OrderStatus.Pending || order.status === OrderStatus.Paid;

  // 格式化时间
  const formatTime = (timeStr?: string) => {
    if (!timeStr) return '-';
    return new Date(timeStr).toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← 返回
        </button>
        <h1 className={styles.title}>订单详情</h1>
      </div>

      <div className={styles.content}>
        {/* 订单状态 */}
        <div className={styles.statusSection}>
          <div
            className={styles.statusBadge}
            style={{ backgroundColor: getOrderStatusColor(order.status) }}
          >
            {order.statusDisplay}
          </div>
          <div className={styles.orderNo}>订单号：{order.orderNo}</div>
        </div>

        {/* 商品信息 */}
        <div className={styles.section}>
          <h2 className={styles.sectionTitle}>商品信息</h2>
          <div className={styles.productInfo}>
            <div className={styles.productImage}>
              {order.productIcon ? (
                <img src={order.productIcon} alt={order.productName} />
              ) : (
                <div className={styles.defaultImage}>🎁</div>
              )}
            </div>
            <div className={styles.productDetails}>
              <div className={styles.productType}>
                {order.productTypeDisplay}
              </div>
              <h3 className={styles.productName}>{order.productName}</h3>
              <div className={styles.productMeta}>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>单价：</span>
                  <span className={styles.metaValue}>
                    {order.unitPrice.toLocaleString()} 胡萝卜
                  </span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>数量：</span>
                  <span className={styles.metaValue}>{order.quantity} 件</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>总价：</span>
                  <span className={styles.metaValue}>
                    {order.totalPrice.toLocaleString()} 胡萝卜
                  </span>
                </div>
                {order.durationDisplay && (
                  <div className={styles.metaItem}>
                    <span className={styles.metaLabel}>有效期：</span>
                    <span className={styles.metaValue}>{order.durationDisplay}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* 订单时间轴 */}
        <div className={styles.section}>
          <h2 className={styles.sectionTitle}>订单跟踪</h2>
          <div className={styles.timeline}>
            <div className={styles.timelineItem}>
              <div className={styles.timelineDot}></div>
              <div className={styles.timelineContent}>
                <div className={styles.timelineTitle}>创建订单</div>
                <div className={styles.timelineTime}>{formatTime(order.createTime)}</div>
              </div>
            </div>

            {order.paidTime && (
              <div className={styles.timelineItem}>
                <div className={styles.timelineDot}></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>支付完成</div>
                  <div className={styles.timelineTime}>{formatTime(order.paidTime)}</div>
                </div>
              </div>
            )}

            {order.completedTime && (
              <div className={styles.timelineItem}>
                <div className={styles.timelineDot}></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>订单完成</div>
                  <div className={styles.timelineTime}>{formatTime(order.completedTime)}</div>
                </div>
              </div>
            )}

            {order.cancelledTime && (
              <div className={styles.timelineItem}>
                <div className={`${styles.timelineDot} ${styles.cancelled}`}></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>订单取消</div>
                  <div className={styles.timelineTime}>{formatTime(order.cancelledTime)}</div>
                  {order.cancelReason && (
                    <div className={styles.timelineReason}>
                      取消原因：{order.cancelReason}
                    </div>
                  )}
                </div>
              </div>
            )}

            {order.status === OrderStatus.Failed && order.failReason && (
              <div className={styles.timelineItem}>
                <div className={`${styles.timelineDot} ${styles.failed}`}></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>发放失败</div>
                  <div className={styles.timelineReason}>
                    失败原因：{order.failReason}
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* 权益到期时间 */}
        {order.benefitExpiresAt && (
          <div className={styles.section}>
            <h2 className={styles.sectionTitle}>权益信息</h2>
            <div className={styles.benefitInfo}>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>权益到期时间：</span>
                <span className={styles.infoValue}>{formatTime(order.benefitExpiresAt)}</span>
              </div>
            </div>
          </div>
        )}

        {/* 操作按钮 */}
        {canCancel && (
          <div className={styles.actions}>
            <button
              className={styles.cancelOrderButton}
              onClick={handleCancelClick}
            >
              取消订单
            </button>
          </div>
        )}
      </div>

      {/* 取消订单对话框 */}
      {showCancelDialog && (
        <div className={styles.dialogOverlay} onClick={handleCloseCancelDialog}>
          <div className={styles.dialog} onClick={(e) => e.stopPropagation()}>
            <div className={styles.dialogHeader}>
              <h3>取消订单</h3>
              <button className={styles.dialogClose} onClick={handleCloseCancelDialog}>
                ✕
              </button>
            </div>
            <div className={styles.dialogContent}>
              <p>确定要取消这个订单吗？</p>
              <div className={styles.reasonInput}>
                <label>取消原因（可选）：</label>
                <textarea
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="请输入取消原因..."
                  rows={3}
                  maxLength={200}
                />
              </div>
            </div>
            <div className={styles.dialogFooter}>
              <button
                className={styles.dialogCancelButton}
                onClick={handleCloseCancelDialog}
                disabled={cancelling}
              >
                返回
              </button>
              <button
                className={styles.dialogConfirmButton}
                onClick={handleConfirmCancel}
                disabled={cancelling}
              >
                {cancelling ? '取消中...' : '确认取消'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
