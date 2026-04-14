import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Product } from '@/types/shop';
import { getProductTypeDisplay } from '@/api/shop';
import { resolveMediaUrl } from '@/utils/media';
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
  const { t } = useTranslation();
  const [quantity, setQuantity] = useState(1);

  if (!isOpen || !product) {
    return null;
  }

  const handleQuantityChange = (newQuantity: number) => {
    const maxQuantity = (product.voLimitPerUser ?? 0) > 0 ? product.voLimitPerUser! : 99;
    const validQuantity = Math.max(1, Math.min(newQuantity, maxQuantity));
    setQuantity(validQuantity);
  };

  const handleConfirm = () => {
    onConfirm(product.voId, quantity);
  };

  const totalPrice = product.voPrice * quantity;
  const coverImageUrl = resolveMediaUrl(product.voCoverImage);
  const iconImageUrl = resolveMediaUrl(product.voIcon);

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.header}>
          <h2 className={styles.title}>{t('shop.purchase.title')}</h2>
          <button className={styles.closeButton} onClick={onClose}>
            ✕
          </button>
        </div>

        <div className={styles.content}>
          {/* 商品信息 */}
          <div className={styles.productInfo}>
            <div className={styles.productImage}>
              {coverImageUrl ? (
                <img src={coverImageUrl} alt={product.voName} />
              ) : iconImageUrl ? (
                <img src={iconImageUrl} alt={product.voName} />
              ) : (
                <div className={styles.defaultImage}>🎁</div>
              )}
            </div>

            <div className={styles.productDetails}>
              <div className={styles.productType}>
                {getProductTypeDisplay(product.voProductType)}
              </div>
              <h3 className={styles.productName}>{product.voName}</h3>
              <div className={styles.productPrice}>
                <span className={styles.currentPrice}>
                  {product.voPrice.toLocaleString()} {t('shop.currency.carrot')}
                </span>
                {product.voOriginalPrice && product.voOriginalPrice > product.voPrice && (
                  <span className={styles.originalPrice}>
                    {t('shop.originalPrice', { price: product.voOriginalPrice.toLocaleString() })}
                  </span>
                )}
              </div>
              <div className={styles.productMeta}>
                {t('shop.purchase.duration', { value: product.voDurationDisplay ?? '' })}
              </div>
            </div>
          </div>

          {/* 数量选择 */}
          <div className={styles.quantitySection}>
            <label className={styles.quantityLabel}>{t('shop.purchase.quantity')}</label>
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
                max={(product.voLimitPerUser ?? 0) > 0 ? product.voLimitPerUser : 99}
              />
              <button
                className={styles.quantityButton}
                onClick={() => handleQuantityChange(quantity + 1)}
                disabled={(product.voLimitPerUser ?? 0) > 0 && quantity >= (product.voLimitPerUser ?? 0)}
              >
                +
              </button>
            </div>
            {(product.voLimitPerUser ?? 0) > 0 && (
              <div className={styles.limitHint}>
                {t('shop.purchase.limitHint', { count: product.voLimitPerUser })}
              </div>
            )}
          </div>

          {/* 价格汇总 */}
          <div className={styles.priceSection}>
            <div className={styles.priceRow}>
              <span>{t('shop.purchase.unitPrice')}</span>
              <span>{product.voPrice.toLocaleString()} {t('shop.currency.carrot')}</span>
            </div>
            <div className={styles.priceRow}>
              <span>{t('shop.purchase.quantityLabel')}</span>
              <span>{t('shop.productCount', { count: quantity })}</span>
            </div>
            <div className={`${styles.priceRow} ${styles.totalRow}`}>
              <span>{t('shop.purchase.total')}</span>
              <span className={styles.totalPrice}>
                {totalPrice.toLocaleString()} {t('shop.currency.carrot')}
              </span>
            </div>
          </div>

          {/* 购买须知 */}
          <div className={styles.noticeSection}>
            <h4 className={styles.noticeTitle}>{t('shop.purchase.noticeTitle')}</h4>
            <ul className={styles.noticeList}>
              <li>{t('shop.purchase.notice.balance')}</li>
              <li>{t('shop.purchase.notice.benefit')}</li>
              <li>{t('shop.purchase.notice.item')}</li>
              <li>{t('shop.purchase.notice.confirm')}</li>
            </ul>
          </div>
        </div>

        <div className={styles.footer}>
          <button
            className={styles.cancelButton}
            onClick={onClose}
            disabled={loading}
          >
            {t('common.cancel')}
          </button>
          <button
            className={styles.confirmButton}
            onClick={handleConfirm}
            disabled={loading}
          >
            {loading ? (
              <>
                <span className={styles.loadingSpinner}></span>
                {t('shop.purchase.processing')}
              </>
            ) : (
              t('shop.purchase.confirmButton', { price: totalPrice.toLocaleString() })
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
