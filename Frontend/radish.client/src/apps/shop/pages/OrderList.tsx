import { useMemo, type MouseEvent } from 'react';
import type { OrderListItem } from '@/types/shop';
import type { LongId } from '@/api/user';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getOrderStatusColor, normalizeOrderStatus, OrderStatus } from '@/api/shop';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { resolveMediaUrl } from '@/utils/media';
import type { ShopLoadError } from '../hooks/useShopData';
import styles from './OrderList.module.css';

interface OrderListProps {
  orders: OrderListItem[];
  currentPage: number;
  totalPages: number;
  loading: boolean;
  loadError?: ShopLoadError | null;
  backHref?: string;
  getOrderHref?: (orderId: LongId) => string;
  onOrderClick: (orderId: LongId) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
  onRetry?: () => void;
  diagnosticActionLabel?: string;
  onCopyDiagnostics?: (error: ShopLoadError) => void;
}

function shouldHandleOrderListLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handleOrderListLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandleOrderListLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

function getOrderGroupKey(order: OrderListItem): 'pending' | 'completed' | 'attention' {
  const status = normalizeOrderStatus(order.voStatus);
  if (status === OrderStatus.Completed) {
    return 'completed';
  }

  if (status === OrderStatus.Pending || status === OrderStatus.Paid) {
    return 'pending';
  }

  return 'attention';
}

export const OrderList = ({
  orders,
  currentPage,
  totalPages,
  loading,
  loadError,
  backHref,
  getOrderHref,
  onOrderClick,
  onPageChange,
  onBack,
  onRetry,
  diagnosticActionLabel,
  onCopyDiagnostics
}: OrderListProps) => {
  const { t } = useTranslation();
  const loadErrorActions: WebStateSlotAction[] = loadError?.scope === 'orders'
    ? [
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
      ].filter((action) => Boolean(action.onClick))
    : [];
  const groupedOrders = useMemo(() => {
    const groups: Record<'pending' | 'completed' | 'attention', OrderListItem[]> = {
      pending: [],
      completed: [],
      attention: [],
    };

    for (const order of orders) {
      groups[getOrderGroupKey(order)].push(order);
    }

    return groups;
  }, [orders]);
  const groupDefinitions = [
    {
      key: 'pending' as const,
      icon: 'mdi:clock-outline',
      title: t('shop.orders.group.pending'),
      description: t('shop.orders.group.pendingDescription'),
      orders: groupedOrders.pending,
    },
    {
      key: 'completed' as const,
      icon: 'mdi:check-circle-outline',
      title: t('shop.orders.group.completed'),
      description: t('shop.orders.group.completedDescription'),
      orders: groupedOrders.completed,
    },
    {
      key: 'attention' as const,
      icon: 'mdi:alert-circle-outline',
      title: t('shop.orders.group.attention'),
      description: t('shop.orders.group.attentionDescription'),
      orders: groupedOrders.attention,
    },
  ];

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        {backHref ? (
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handleOrderListLinkClick(event, onBack)}
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
          <h1 className={styles.title}>{t('shop.orders.title')}</h1>
          <p className={styles.description}>{t('shop.orders.emptyDescription')}</p>
        </div>
      </div>

      {loading ? (
        <WebStateSlot
          tone="loading"
          title={t('shop.loading')}
          description={t('shop.orders.title')}
        />
      ) : loadError?.scope === 'orders' ? (
        <WebStateSlot
          tone="error"
          icon="mdi:receipt-text-remove-outline"
          title={t('shop.orders.loadFailedTitle')}
          description={loadError.message}
          actions={loadErrorActions}
        />
      ) : orders.length > 0 ? (
        <>
          <div className={styles.summaryBar}>
            <div>
              <span>{t('shop.orders.title')}</span>
              <strong>{orders.length}</strong>
              <span>{t('shop.orders.pageInfo', { current: currentPage, total: totalPages })}</span>
            </div>
            <div className={styles.statusChips}>
              {groupDefinitions.map((group) => (
                <span key={group.key}>
                  <Icon icon={group.icon} size={14} />
                  {group.title}
                  <strong>{group.orders.length}</strong>
                </span>
              ))}
            </div>
          </div>
          <div className={styles.orderGroups}>
            {groupDefinitions.map((group) => (
              <section key={group.key} className={styles.orderGroup}>
                <div className={styles.groupHeader}>
                  <div>
                    <h2>{group.title}</h2>
                    <p>{group.description}</p>
                  </div>
                  <span>{group.orders.length}</span>
                </div>
                <div className={styles.orderList}>
                  {group.orders.length > 0 ? (
                    group.orders.map((order) => {
                      const productIconUrl = resolveMediaUrl(order.voProductIcon);
                      const orderHref = getOrderHref?.(order.voId);
                      const orderContent = (
                        <>
                          <div className={styles.orderHeader}>
                            <span className={styles.orderNo}>{t('shop.orders.orderNo', { orderNo: order.voOrderNo })}</span>
                            <span
                              className={styles.orderStatus}
                              style={{ color: getOrderStatusColor(order.voStatus) }}
                            >
                              {order.voStatusDisplay ?? ''}
                            </span>
                          </div>

                          <div className={styles.orderContent}>
                            <div className={styles.productInfo}>
                              {productIconUrl ? (
                                <img
                                  src={productIconUrl}
                                  alt={order.voProductName}
                                  className={styles.productIcon}
                                />
                              ) : (
                                <div className={styles.productIconFallback}>
                                  <Icon icon="mdi:gift-outline" size={22} />
                                </div>
                              )}
                              <div className={styles.productDetails}>
                                <h3 className={styles.productName}>{order.voProductName}</h3>
                                <div className={styles.orderMeta}>
                                  <span>{t('shop.orders.quantity', { count: order.voQuantity })}</span>
                                  <span>{t('shop.orders.totalPrice', { price: order.voTotalPrice.toLocaleString() })}</span>
                                </div>
                              </div>
                            </div>

                            <div className={styles.orderTime}>
                              {order.voCreateTime ? new Date(order.voCreateTime).toLocaleString() : ''}
                            </div>
                          </div>
                        </>
                      );

                      return orderHref ? (
                        <a
                          key={order.voId}
                          className={styles.orderCard}
                          href={orderHref}
                          onClick={(event) => handleOrderListLinkClick(event, () => onOrderClick(order.voId))}
                        >
                          {orderContent}
                        </a>
                      ) : (
                        <div
                          key={order.voId}
                          className={styles.orderCard}
                          role="button"
                          tabIndex={0}
                          onClick={() => onOrderClick(order.voId)}
                          onKeyDown={(event) => {
                            if (event.key === 'Enter' || event.key === ' ') {
                              event.preventDefault();
                              onOrderClick(order.voId);
                            }
                          }}
                        >
                          {orderContent}
                        </div>
                      );
                    })
                  ) : (
                    <div className={styles.emptyGroup}>{t('shop.orders.group.empty')}</div>
                  )}
                </div>
              </section>
            ))}
          </div>

          {totalPages > 1 && (
            <div className={styles.pagination}>
              <button
                className={styles.pageButton}
                disabled={currentPage <= 1}
                onClick={() => onPageChange(currentPage - 1)}
              >
                {t('shop.orders.prevPage')}
              </button>
              <span className={styles.pageInfo}>
                {t('shop.orders.pageInfo', { current: currentPage, total: totalPages })}
              </span>
              <button
                className={styles.pageButton}
                disabled={currentPage >= totalPages}
                onClick={() => onPageChange(currentPage + 1)}
              >
                {t('shop.orders.nextPage')}
              </button>
            </div>
          )}
        </>
      ) : (
        <WebStateSlot
          tone="empty"
          icon="mdi:package-variant-closed"
          title={t('shop.orders.emptyTitle')}
          description={t('shop.orders.emptyDescription')}
          actions={[
            backHref
              ? {
                  label: t('shop.nav.home'),
                  href: backHref,
                  kind: 'secondary',
                  onClick: (event) => handleOrderListLinkClick(event as MouseEvent<HTMLAnchorElement>, onBack),
                }
              : {
                  label: t('shop.nav.home'),
                  kind: 'secondary',
                  onClick: onBack,
                },
          ]}
        />
      )}
    </div>
  );
};
