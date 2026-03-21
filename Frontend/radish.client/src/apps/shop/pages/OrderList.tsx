import type { OrderListItem } from '@/types/shop';
import { useTranslation } from 'react-i18next';
import { getOrderStatusColor } from '@/api/shop';
import styles from './OrderList.module.css';

interface OrderListProps {
  orders: OrderListItem[];
  currentPage: number;
  totalPages: number;
  loading: boolean;
  onOrderClick: (orderId: number) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
}

export const OrderList = ({
  orders,
  currentPage,
  totalPages,
  loading,
  onOrderClick,
  onPageChange,
  onBack
}: OrderListProps) => {
  const { t } = useTranslation();

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← {t('shop.back')}
        </button>
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
            {orders.map((order) => (
              <div
                key={order.voId}
                className={styles.orderCard}
                onClick={() => onOrderClick(order.voId)}
              >
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
                    {order.voProductIcon && (
                      <img
                        src={order.voProductIcon}
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
              </div>
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
        <div className={styles.emptyState}>
          <div className={styles.emptyIcon}>📦</div>
          <h3>{t('shop.orders.emptyTitle')}</h3>
          <p>{t('shop.orders.emptyDescription')}</p>
        </div>
      )}
    </div>
  );
};
