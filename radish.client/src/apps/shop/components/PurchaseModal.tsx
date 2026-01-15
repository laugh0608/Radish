import { useState } from 'react';
import type { Product } from '@/api/shop';
import { getProductTypeDisplay } from '@/api/shop';
import styles from './PurchaseModal.module.css';

interface PurchaseModalProps {
  isOpen: boolean;
  product: Product | null;
  loading: boolean;
  onClose: () => void;
  onConfirm: (productId: number, quantity: number) => void;
}

export const PurchaseModal = ({
  isOpen,
  product,
  loading,
  onClose,
  onConfirm
}: PurchaseModalProps) => {
  // const { t } = useTranslation(); // 暂时不使用
  const [quantity, setQuantity] = useState(1);

  if (!isOpen || !product) {
    return null;
  }

  const handleQuantityChange = (newQuantity: number) => {
    const maxQuantity = product.limitPerUser > 0 ? product.limitPerUser : 99;
    const validQuantity = Math.max(1, Math.min(newQuantity, maxQuantity));
    setQuantity(validQuantity);
  };

  const handleConfirm = () => {
    onConfirm(product.id, quantity);
  };

  const totalPrice = product.price * quantity;

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.header}>
          <h2 className={styles.title}>确认购买</h2>
          <button className={styles.closeButton} onClick={onClose}>
            ✕
          </button>
        </div>

        <div className={styles.content}>
          {/* 商品信息 */}
          <div className={styles.productInfo}>
            <div className={styles.productImage}>
              {product.coverImage ? (
                <img src={product.coverImage} alt={product.name} />
              ) : product.icon ? (
                <img src={product.icon} alt={product.name} />
              ) : (
                <div className={styles.defaultImage}>🎁</div>
              )}
            </div>

            <div className={styles.productDetails}>
              <div className={styles.productType}>
                {getProductTypeDisplay(product.productType)}
              </div>
              <h3 className={styles.productName}>{product.name}</h3>
              <div className={styles.productPrice}>
                <span className={styles.currentPrice}>
                  {product.price.toLocaleString()} 胡萝卜
                </span>
                {product.originalPrice && product.originalPrice > product.price && (
                  <span className={styles.originalPrice}>
                    原价 {product.originalPrice.toLocaleString()}
                  </span>
                )}
              </div>
              <div className={styles.productMeta}>
                有效期：{product.durationDisplay}
              </div>
            </div>
          </div>

          {/* 数量选择 */}
          <div className={styles.quantitySection}>
            <label className={styles.quantityLabel}>购买数量：</label>
            <div className={styles.quantityControls}>
              <button
                className={styles.quantityButton}
                onClick={() => handleQuantityChange(quantity - 1)}
                disabled={quantity <= 1}
              >
                -
              </button>
              <input
                type="number"
                className={styles.quantityInput}
                value={quantity}
                onChange={(e) => handleQuantityChange(parseInt(e.target.value) || 1)}
                min="1"
                max={product.limitPerUser > 0 ? product.limitPerUser : 99}
              />
              <button
                className={styles.quantityButton}
                onClick={() => handleQuantityChange(quantity + 1)}
                disabled={product.limitPerUser > 0 && quantity >= product.limitPerUser}
              >
                +
              </button>
            </div>
            {product.limitPerUser > 0 && (
              <div className={styles.limitHint}>
                每人限购 {product.limitPerUser} 件
              </div>
            )}
          </div>

          {/* 价格汇总 */}
          <div className={styles.priceSection}>
            <div className={styles.priceRow}>
              <span>单价：</span>
              <span>{product.price.toLocaleString()} 胡萝卜</span>
            </div>
            <div className={styles.priceRow}>
              <span>数量：</span>
              <span>{quantity} 件</span>
            </div>
            <div className={`${styles.priceRow} ${styles.totalRow}`}>
              <span>总计：</span>
              <span className={styles.totalPrice}>
                {totalPrice.toLocaleString()} 胡萝卜
              </span>
            </div>
          </div>

          {/* 购买须知 */}
          <div className={styles.noticeSection}>
            <h4 className={styles.noticeTitle}>购买须知：</h4>
            <ul className={styles.noticeList}>
              <li>购买后将立即从您的胡萝卜余额中扣除相应金额</li>
              <li>权益类商品将自动发放到您的背包</li>
              <li>消耗品道具可在背包中查看和使用</li>
              <li>请确认商品信息无误后再进行购买</li>
            </ul>
          </div>
        </div>

        <div className={styles.footer}>
          <button
            className={styles.cancelButton}
            onClick={onClose}
            disabled={loading}
          >
            取消
          </button>
          <button
            className={styles.confirmButton}
            onClick={handleConfirm}
            disabled={loading}
          >
            {loading ? (
              <>
                <span className={styles.loadingSpinner}></span>
                购买中...
              </>
            ) : (
              `确认购买 (${totalPrice.toLocaleString()} 胡萝卜)`
            )}
          </button>
        </div>
      </div>
    </div>
  );
};