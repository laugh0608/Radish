import { useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import type { Order } from '@/types/shop';
import type { LongId } from '@/api/user';
import { getOrderStatusColor, normalizeOrderStatus, OrderStatus } from '@/api/shop';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import type { ShopLoadError } from '../hooks/useShopData';
import styles from './OrderDetail.module.css';

interface OrderDetailProps {
  orderId: LongId;
  order: Order | null;
  loading: boolean;
  loadError?: ShopLoadError | null;
  backHref?: string;
  inventoryHref?: string;
  productHref?: string;
  onBack: () => void;
  onInventoryClick?: () => void;
  onProductClick: (productId: LongId) => void;
  onCancelOrder: (orderId: LongId, reason?: string) => void;
  onRetry?: () => void;
  diagnosticActionLabel?: string;
  onCopyDiagnostics?: (error: ShopLoadError) => void;
}

function shouldHandleOrderDetailLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handleOrderDetailLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandleOrderDetailLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

export const OrderDetail = ({
  order,
  loading,
  loadError,
  backHref,
  inventoryHref,
  productHref,
  onBack,
  onInventoryClick,
  onProductClick,
  onCancelOrder,
  onRetry,
  diagnosticActionLabel,
  onCopyDiagnostics
}: OrderDetailProps) => {
  const { t } = useTranslation();
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  if (loading) {
    return (
      <div className={styles.container}>
        <WebStateSlot
          tone="loading"
          title={t('shop.loading')}
          description={t('shop.orderDetail.title')}
        />
      </div>
    );
  }

  if (!order) {
    if (loadError?.scope === 'order-detail') {
      const errorActions: WebStateSlotAction[] = [
        {
          label: t('common.retry'),
          onClick: onRetry,
        },
        {
          label: diagnosticActionLabel || t('common.copyDiagnostics'),
          kind: 'secondary' as const,
          onClick: () => {
            onCopyDiagnostics?.(loadError);
          },
        },
        backHref
          ? {
              label: t('shop.backToOrders'),
              href: backHref,
              kind: 'secondary' as const,
              onClick: (event: MouseEvent<HTMLAnchorElement | HTMLButtonElement>) => handleOrderDetailLinkClick(event as MouseEvent<HTMLAnchorElement>, onBack),
            }
          : {
              label: t('shop.backToOrders'),
              kind: 'secondary' as const,
              onClick: onBack,
            },
      ].filter((action) => Boolean(action.onClick || action.href));

      return (
        <div className={styles.container}>
          <WebStateSlot
            tone="error"
            title={t('shop.orderDetail.loadFailedTitle')}
            description={loadError.message}
            actions={errorActions}
          />
        </div>
      );
    }

    return (
      <div className={styles.container}>
        <WebStateSlot
          tone="notFound"
          title={t('shop.notFound.orderTitle')}
          description={t('shop.notFound.orderDescription')}
          actions={[
            backHref
              ? {
                  label: t('shop.backToOrders'),
                  href: backHref,
                  onClick: (event) => handleOrderDetailLinkClick(event as MouseEvent<HTMLAnchorElement>, onBack),
                }
              : {
                  label: t('shop.backToOrders'),
                  onClick: onBack,
                },
          ]}
        />
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
  const normalizedStatus = normalizeOrderStatus(order.voStatus);

  // 判断是否可以取消订单
  const canCancel = normalizedStatus === OrderStatus.Pending || normalizedStatus === OrderStatus.Paid;

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
  const timelineItems = [
    {
      key: 'create',
      title: t('shop.orderDetail.create'),
      time: formatTime(order.voCreateTime),
      description: t('shop.orderDetail.timelineCreateDescription'),
      tone: 'normal' as const,
    },
    ...(order.voPaidTime ? [{
      key: 'paid',
      title: t('shop.orderDetail.paid'),
      time: formatTime(order.voPaidTime),
      description: t('shop.orderDetail.timelinePaidDescription'),
      tone: 'normal' as const,
    }] : []),
    ...(order.voCompletedTime ? [{
      key: 'completed',
      title: t('shop.orderDetail.completed'),
      time: formatTime(order.voCompletedTime),
      description: t('shop.orderDetail.timelineCompletedDescription'),
      tone: 'success' as const,
    }] : []),
    ...(order.voCancelledTime ? [{
      key: 'cancelled',
      title: t('shop.orderDetail.cancelled'),
      time: formatTime(order.voCancelledTime),
      description: order.voCancelReason
        ? t('shop.orderDetail.cancelReason', { reason: order.voCancelReason })
        : t('shop.orderDetail.timelineCancelledDescription'),
      tone: 'cancelled' as const,
    }] : []),
    ...(normalizedStatus === OrderStatus.Failed && order.voFailReason ? [{
      key: 'failed',
      title: t('shop.orderDetail.failed'),
      time: '-',
      description: t('shop.orderDetail.failReason', { reason: order.voFailReason }),
      tone: 'failed' as const,
    }] : []),
  ];
  const canOpenInventory = Boolean(onInventoryClick || inventoryHref);
  const inventoryStatus = normalizedStatus === OrderStatus.Completed
    ? t('shop.orderDetail.inventoryReady')
    : normalizedStatus === OrderStatus.Paid
      ? t('shop.orderDetail.inventoryPending')
      : t('shop.orderDetail.inventoryUnavailable');

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        {backHref ? (
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handleOrderDetailLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={17} />
            <span>{t('shop.back')}</span>
          </a>
        ) : (
          <button type="button" className={styles.backButton} onClick={onBack}>
            <Icon icon="mdi:arrow-left" size={17} />
            <span>{t('shop.back')}</span>
          </button>
        )}
        <div className={styles.headerCopy}>
          <p className={styles.kicker}>{t('shop.title')}</p>
          <h1 className={styles.title}>{t('shop.orderDetail.title')}</h1>
          <p className={styles.description}>{t('shop.orders.orderNo', { orderNo: order.voOrderNo })}</p>
        </div>
      </div>

      <div className={styles.contentGrid}>
        <div className={styles.mainColumn}>
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
          <div className={styles.sectionHeader}>
            <h2 className={styles.sectionTitle}>{t('shop.orderDetail.productInfo')}</h2>
            {productHref ? (
              <a
                className={styles.productActionButton}
                href={productHref}
                onClick={(event) => handleOrderDetailLinkClick(event, () => onProductClick(order.voProductId))}
              >
                {t('shop.orderDetail.viewProduct')}
              </a>
            ) : (
              <button
                type="button"
                className={styles.productActionButton}
                onClick={() => onProductClick(order.voProductId)}
              >
                {t('shop.orderDetail.viewProduct')}
              </button>
            )}
          </div>
          <div className={styles.productInfo}>
            <div className={styles.productImage}>
              {productIconUrl ? (
                <img src={productIconUrl} alt={order.voProductName} />
              ) : (
                <div className={styles.defaultImage}>
                  <Icon icon="mdi:gift-outline" size={32} />
                </div>
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
            {timelineItems.map((item) => (
              <div key={item.key} className={styles.timelineItem}>
                <div
                  className={`${styles.timelineDot} ${item.tone === 'cancelled' ? styles.cancelled : ''} ${item.tone === 'failed' ? styles.failed : ''}`}
                ></div>
                <div className={styles.timelineContent}>
                  <div className={styles.timelineTitle}>{item.title}</div>
                  <div className={styles.timelineTime}>{item.time}</div>
                  <div className={styles.timelineReason}>{item.description}</div>
                </div>
              </div>
            ))}
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

        <aside className={styles.detailRail}>
          <section className={styles.railCard}>
            <span>{t('shop.orderDetail.railStatus')}</span>
            <strong style={{ color: getOrderStatusColor(order.voStatus) }}>{order.voStatusDisplay ?? ''}</strong>
          </section>
          <section className={styles.railCard}>
            <span>{t('shop.orderDetail.railPayment')}</span>
            <strong>{order.voTotalPrice.toLocaleString()}</strong>
            <p>{t('shop.currency.carrot')}</p>
          </section>
          <section className={styles.railCard}>
            <span>{t('shop.orderDetail.railInventory')}</span>
            <strong>{inventoryStatus}</strong>
            {order.voBenefitExpiresAt && (
              <p>{t('shop.orderDetail.expireAt')} {formatTime(order.voBenefitExpiresAt)}</p>
            )}
          </section>
          <div className={styles.railActions}>
            {canOpenInventory && (
              inventoryHref ? (
                <a
                  className={styles.inventoryButton}
                  href={inventoryHref}
                  onClick={(event) => {
                    if (!onInventoryClick) {
                      return;
                    }
                    handleOrderDetailLinkClick(event, onInventoryClick);
                  }}
                >
                  <Icon icon="mdi:package-variant-closed" size={17} />
                  <span>{t('shop.orderDetail.openInventory')}</span>
                </a>
              ) : (
                <button type="button" className={styles.inventoryButton} onClick={onInventoryClick}>
                  <Icon icon="mdi:package-variant-closed" size={17} />
                  <span>{t('shop.orderDetail.openInventory')}</span>
                </button>
              )
            )}
            {backHref ? (
              <a
                className={styles.railBackButton}
                href={backHref}
                onClick={(event) => handleOrderDetailLinkClick(event, onBack)}
              >
                <Icon icon="mdi:arrow-left" size={17} />
                <span>{t('shop.backToOrders')}</span>
              </a>
            ) : (
              <button type="button" className={styles.railBackButton} onClick={onBack}>
                <Icon icon="mdi:arrow-left" size={17} />
                <span>{t('shop.backToOrders')}</span>
              </button>
            )}
          </div>
          <p className={styles.railHint}>{t('shop.orderDetail.railScopeHint')}</p>
        </aside>
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
