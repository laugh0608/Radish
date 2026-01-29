import type { OrderListItemData } from '@/utils/viewModelMapper';
import { getOrderStatusColor } from '@/api/shop';
import styles from './OrderList.module.css';

interface OrderListProps {
  orders: OrderListItemData[];
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
  // const { t } = useTranslation(); // 暂时不使用

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← 返回
        </button>
        <h1 className={styles.title}>我的订单</h1>
      </div>

      {loading ? (
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>加载中...</p>
        </div>
      ) : orders.length > 0 ? (
        <>
          <div className={styles.orderList}>
            {orders.map((order) => (
              <div
                key={order.id}
                className={styles.orderCard}
                onClick={() => onOrderClick(order.id)}
              >
                <div className={styles.orderHeader}>
                  <span className={styles.orderNo}>订单号：{order.orderNo}</span>
                  <span
                    className={styles.orderStatus}
                    style={{ color: getOrderStatusColor(order.status) }}
                  >
                    {order.statusDisplay}
                  </span>
                </div>

                <div className={styles.orderContent}>
                  <div className={styles.productInfo}>
                    {order.productIcon && (
                      <img
                        src={order.productIcon}
                        alt={order.productName}
                        className={styles.productIcon}
                      />
                    )}
                    <div className={styles.productDetails}>
                      <h3 className={styles.productName}>{order.productName}</h3>
                      <div className={styles.orderMeta}>
                        <span>数量：{order.quantity}</span>
                        <span>总价：{order.totalPrice.toLocaleString()} 胡萝卜</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.orderTime}>
                    {new Date(order.createTime).toLocaleString()}
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
                上一页
              </button>
              <span className={styles.pageInfo}>
                第 {currentPage} 页，共 {totalPages} 页
              </span>
              <button
                className={styles.pageButton}
                disabled={currentPage >= totalPages}
                onClick={() => onPageChange(currentPage + 1)}
              >
                下一页
              </button>
            </div>
          )}
        </>
      ) : (
        <div className={styles.emptyState}>
          <div className={styles.emptyIcon}>📦</div>
          <h3>暂无订单</h3>
          <p>您还没有购买过任何商品</p>
        </div>
      )}
    </div>
  );
};