import type { Product } from '@/types/shop';
import { useTranslation } from 'react-i18next';
import { getProductTypeDisplay, StockType } from '@/api/shop';
import styles from './ProductDetail.module.css';

interface ProductDetailProps {
  productId: number;
  product: Product | null;
  loading: boolean;
  canBuy: { canBuy: boolean; reason: string } | null;
  checkingCanBuy: boolean;
  isAuthenticated: boolean;
  onBack: () => void;
  onPurchase: (productId: number) => void;
}

export const ProductDetail = ({
  product,
  loading,
  canBuy,
  checkingCanBuy,
  isAuthenticated,
  onBack,
  onPurchase
}: ProductDetailProps) => {
  const { t } = useTranslation();
  const blockedReason = canBuy?.reason?.trim() ?? '';

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

  if (!product) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h2>{t('shop.notFound.productTitle')}</h2>
          <p>{t('shop.notFound.productDescription')}</p>
          <button className={styles.backButton} onClick={onBack}>
            {t('shop.backToProducts')}
          </button>
        </div>
      </div>
    );
  }

  const handlePurchase = () => {
    if (!isAuthenticated) {
      alert(t('shop.loginRequired'));
      return;
    }
    onPurchase(product.voId);
  };

  const getPurchaseButtonText = () => {
    if (!isAuthenticated) return t('shop.loginRequired');
    if (checkingCanBuy) return t('shop.checkingAvailability');
    if (!product.voInStock) return t('shop.outOfStock');
    if (!product.voIsOnSale) return t('shop.unavailable');
    if (canBuy && !canBuy.canBuy) return blockedReason || t('shop.unavailable');
    return t('shop.buyNow');
  };

  const isPurchaseDisabled = () => {
    return !isAuthenticated ||
           checkingCanBuy ||
           !product.voInStock ||
           !product.voIsOnSale ||
           (canBuy !== null && !canBuy.canBuy);
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← {t('shop.back')}
        </button>
      </div>

      <div className={styles.content}>
        {/* 商品主要信息 */}
        <div className={styles.productMain}>
          {/* 商品图片 */}
          <div className={styles.imageSection}>
            <div className={styles.mainImage}>
              {product.voCoverImage ? (
                <img src={product.voCoverImage} alt={product.voName} />
              ) : product.voIcon ? (
                <img src={product.voIcon} alt={product.voName} />
              ) : (
                <div className={styles.defaultImage}>
                  <span>🎁</span>
                </div>
              )}

              {product.voHasDiscount && (
                <div className={styles.discountBadge}>
                  {product.voDiscountPercent}折
                </div>
              )}

              {!product.voInStock && (
                <div className={styles.outOfStockOverlay}>
                  <span>{t('shop.outOfStock')}</span>
                </div>
              )}
            </div>
          </div>

          {/* 商品信息 */}
          <div className={styles.infoSection}>
            <div className={styles.productType}>
              {getProductTypeDisplay(product.voProductType)}
            </div>

            <h1 className={styles.productName}>{product.voName}</h1>

            <div className={styles.priceSection}>
              <div className={styles.currentPrice}>
                {product.voPrice.toLocaleString()} {t('shop.currency.carrot')}
              </div>
              {product.voOriginalPrice && product.voOriginalPrice > product.voPrice && (
                <div className={styles.originalPrice}>
                  {t('shop.originalPrice', { price: product.voOriginalPrice.toLocaleString() })}
                </div>
              )}
            </div>

            <div className={styles.metaInfo}>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.sold')}</span>
                <span className={styles.metaValue}>{t('shop.productCount', { count: product.voSoldCount ?? 0 })}</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.stock')}</span>
                <span className={styles.metaValue}>
                  {product.voStockType === StockType.Unlimited ? t('shop.stock.unlimited') : t('shop.productCount', { count: product.voStock ?? 0 })}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.limit')}</span>
                <span className={styles.metaValue}>
                  {(product.voLimitPerUser ?? 0) > 0 ? t('shop.limit.perUser', { count: product.voLimitPerUser }) : t('shop.limit.unlimited')}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.duration')}</span>
                <span className={styles.metaValue}>{product.voDurationDisplay ?? ''}</span>
              </div>
            </div>

            {/* 购买按钮 */}
            <div className={styles.purchaseSection}>
              <button
                className={`${styles.purchaseButton} ${isPurchaseDisabled() ? styles.disabled : ''}`}
                onClick={handlePurchase}
                disabled={isPurchaseDisabled()}
              >
                {getPurchaseButtonText()}
              </button>

              {canBuy && !canBuy.canBuy && blockedReason && (
                <div className={styles.purchaseHint}>
                  {blockedReason}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* 商品详情 */}
        <div className={styles.detailSection}>
          <h2 className={styles.sectionTitle}>{t('shop.section.detail')}</h2>

          <div className={styles.detailContent}>
            {product.voDescription ? (
              <div className={styles.description}>
                {product.voDescription.split('\n').map((line, index) => (
                  <p key={index}>{line}</p>
                ))}
              </div>
            ) : (
              <p className={styles.noDescription}>{t('shop.noDescription')}</p>
            )}

            {/* 权益/消耗品特殊信息 */}
            {product.voBenefitValue && (
              <div className={styles.benefitInfo}>
                <h3>{t('shop.section.benefit')}</h3>
                <div className={styles.benefitValue}>
                  {product.voBenefitValue}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* 购买须知 */}
        <div className={styles.noticeSection}>
          <h2 className={styles.sectionTitle}>{t('shop.section.notice')}</h2>

          <div className={styles.noticeContent}>
            <ul>
              <li>{t('shop.notice.balance')}</li>
              <li>{t('shop.notice.benefit')}</li>
              <li>{t('shop.notice.item')}</li>
              <li>{t('shop.notice.expire')}</li>
              <li>{t('shop.notice.support')}</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
};
