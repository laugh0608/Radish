import type { MouseEvent } from 'react';
import type { OrderListItem } from '@/types/shop';
import type { LongId } from '@/api/user';
import { useTranslation } from 'react-i18next';
import { getOrderStatusColor } from '@/api/shop';
import { resolveMediaUrl } from '@/utils/media';
import styles from './OrderList.module.css';

interface OrderListProps {
  orders: OrderListItem[];
  currentPage: number;
  totalPages: number;
  loading: boolean;
  backHref?: string;
  getOrderHref?: (orderId: LongId) => string;
  onOrderClick: (orderId: LongId) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
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

export const OrderList = ({
  orders,
  currentPage,
  totalPages,
  loading,
  backHref,
  getOrderHref,
  onOrderClick,
  onPageChange,
  onBack
}: OrderListProps) => {
  const { t } = useTranslation();

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        {backHref ? (
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handleOrderListLinkClick(event, onBack)}
          >
            ← {t('shop.back')}
          </a>
        ) : (
          <button type="button" className={styles.backButton} onClick={onBack}>
            ← {t('shop.back')}
          </button>
        )}
        <h1 className={styles.title}>{t('shop.orders.title')}</h1>
      </div>

      {loading ? (
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>{t('shop.loading')}</p>
        </div>
      ) : orders.length > 0 ? (
        <>
          <div className={styles.orderList}>
            {orders.map((order) => {
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
                      {productIconUrl && (
                        <img
                          src={productIconUrl}
                          alt={order.voProductName}
                          className={styles.productIcon}
                        />
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
                  onClick={() => onOrderClick(order.voId)}
                >
                  {orderContent}
                </div>
              );
            })}
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
        <div className={styles.emptyState}>
          <div className={styles.emptyIcon}>📦</div>
          <h3>{t('shop.orders.emptyTitle')}</h3>
          <p>{t('shop.orders.emptyDescription')}</p>
        </div>
      )}
    </div>
  );
};
