import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import type { Order } from '@/types/shop';
import { getOrderStatusColor, OrderStatus } from '@/api/shop';
import styles from './OrderDetail.module.css';

interface OrderDetailProps {
  orderId: number;
  order: Order | null;
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
  const { t } = useTranslation();
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>{t('shop.loading')}</p>
        </div>
      </div>
    );
  }

  if (!order) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h2>{t('shop.notFound.orderTitle')}</h2>
          <p>{t('shop.notFound.orderDescription')}</p>
          <button className={styles.backButton} onClick={onBack}>
            {t('shop.backToOrders')}
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
      await onCancelOrder(order.voId, cancelReason || undefined);
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

  const productIconUrl = resolveMediaUrl(order.voProductIcon);

  // 判断是否可以取消订单
  const canCancel = order.voStatus === OrderStatus.Pending || order.voStatus === OrderStatus.Paid;

  // 格式化时间
  const formatTime = (timeStr?: string | null) => {
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
          ← {t('shop.back')}
        </button>
        <h1 className={styles.title}>{t('shop.orderDetail.title')}</h1>
      </div>

      <div className={styles.content}>
        {/* 订单状态 */}
        <div className={styles.statusSection}>
          <div
            className={styles.statusBadge}
            style={{ backgroundColor: getOrderStatusColor(order.voStatus) }}
          >
            {order.voStatusDisplay ?? ''}
          </div>
          <div className={styles.orderNo}>{t('shop.orders.orderNo', { orderNo: order.voOrderNo })}</div>
        </div>

        {/* 商品信息 */}
        <div className={styles.section}>
          <h2 className={styles.sectionTitle}>{t('shop.orderDetail.productInfo')}</h2>
          <div className={styles.productInfo}>
            <div className={styles.productImage}>
              {productIconUrl ? (
                <img src={productIconUrl} alt={order.voProductName} />
              ) : (
                <div className={styles.defaultImage}>🎁</div>
              )}
            </div>
            <div className={styles.productDetails}>
              <div className={styles.productType}>
                {order.voProductTypeDisplay ?? ''}
              </div>
              <h3 className={styles.productName}>{order.voProductName}</h3>
              <div className={styles.productMeta}>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>{t('shop.orderDetail.unitPrice')}</span>
                  <span className={styles.metaValue}>
                    {order.voUnitPrice.toLocaleString()} {t('shop.currency.carrot')}
                  </span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>{t('shop.orderDetail.quantity')}</span>
                  <span className={styles.metaValue}>{t('shop.productCount', { count: order.voQuantity })}</span>
                </div>
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>{t('shop.orderDetail.totalPrice')}</span>
                  <span className={styles.metaValue}>
                    {order.voTotalPrice.toLocaleString()} {t('shop.currency.carrot')}
                  </span>
                </div>
                {order.voDurationDisplay && (
                  <div className={styles.metaItem}>
                    <span className={styles.metaLabel}>{t('shop.orderDetail.duration')}</span>
                    <span className={styles.metaValue}>{order.voDurationDisplay}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* 订单时间轴 */}
        <div className={styles.section}>
          <h2 className={styles.sectionTitle}>{t('shop.orderDetail.statusTrack')}</h2>
          <div className={styles.timeline}>
            <div className={styles.timelineItem}>
              <div className={styles.timelineDot}></div>
              <div className={styles.timelineContent}>
                <div className={styles.timelineTitle}>{t('shop.orderDetail.create')}</div>
                <div className={styles.timelineTime}>{formatTime(order.voCreateTime)}</div>
              </div>
            </div>

            {order.voPaidTime && (
              <div className={styles.timelineItem}>
              <div className={styles.timelineDot}></div>
              <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>{t('shop.orderDetail.paid')}</div>
                  <div className={styles.timelineTime}>{formatTime(order.voPaidTime)}</div>
                </div>
              </div>
            )}

            {order.voCompletedTime && (
              <div className={styles.timelineItem}>
              <div className={styles.timelineDot}></div>
              <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>{t('shop.orderDetail.completed')}</div>
                  <div className={styles.timelineTime}>{formatTime(order.voCompletedTime)}</div>
                </div>
              </div>
            )}

            {order.voCancelledTime && (
              <div className={styles.timelineItem}>
              <div className={`${styles.timelineDot} ${styles.cancelled}`}></div>
              <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>{t('shop.orderDetail.cancelled')}</div>
                  <div className={styles.timelineTime}>{formatTime(order.voCancelledTime)}</div>
                  {order.voCancelReason && (
                    <div className={styles.timelineReason}>
                      {t('shop.orderDetail.cancelReason', { reason: order.voCancelReason })}
                    </div>
                  )}
                </div>
              </div>
            )}

            {order.voStatus === OrderStatus.Failed && order.voFailReason && (
              <div className={styles.timelineItem}>
              <div className={`${styles.timelineDot} ${styles.failed}`}></div>
              <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>{t('shop.orderDetail.failed')}</div>
                  <div className={styles.timelineReason}>
                    {t('shop.orderDetail.failReason', { reason: order.voFailReason })}
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* 权益到期时间 */}
        {order.voBenefitExpiresAt && (
          <div className={styles.section}>
            <h2 className={styles.sectionTitle}>{t('shop.orderDetail.benefitInfo')}</h2>
            <div className={styles.benefitInfo}>
              <div className={styles.infoItem}>
                <span className={styles.infoLabel}>{t('shop.orderDetail.expireAt')}</span>
                <span className={styles.infoValue}>{formatTime(order.voBenefitExpiresAt)}</span>
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
              {t('shop.orderDetail.cancelOrder')}
            </button>
          </div>
        )}
      </div>

      {/* 取消订单对话框 */}
      {showCancelDialog && (
        <div className={styles.dialogOverlay} onClick={handleCloseCancelDialog}>
          <div className={styles.dialog} onClick={(e) => e.stopPropagation()}>
            <div className={styles.dialogHeader}>
              <h3>{t('shop.orderDetail.cancelDialogTitle')}</h3>
              <button className={styles.dialogClose} onClick={handleCloseCancelDialog}>
                ✕
              </button>
            </div>
            <div className={styles.dialogContent}>
              <p>{t('shop.orderDetail.cancelDialogConfirm')}</p>
              <div className={styles.reasonInput}>
                <label>{t('shop.orderDetail.cancelDialogReason')}</label>
                <textarea
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder={t('shop.orderDetail.cancelDialogPlaceholder')}
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
                {t('shop.back')}
              </button>
              <button
                className={styles.dialogConfirmButton}
                onClick={handleConfirmCancel}
                disabled={cancelling}
              >
                {cancelling ? t('shop.orderDetail.cancelling') : t('shop.orderDetail.confirmCancel')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
