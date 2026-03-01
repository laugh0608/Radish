import type { Product } from '@/types/shop';
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
  // const { t } = useTranslation(); // 暂时不使用
  const blockedReason = canBuy?.reason?.trim() ?? '';

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

  if (!product) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <h2>商品不存在</h2>
          <p>您访问的商品可能已下架或不存在</p>
          <button className={styles.backButton} onClick={onBack}>
            返回商品列表
          </button>
        </div>
      </div>
    );
  }

  const handlePurchase = () => {
    if (!isAuthenticated) {
      alert('请先登录');
      return;
    }
    onPurchase(product.voId);
  };

  const getPurchaseButtonText = () => {
    if (!isAuthenticated) return '请先登录';
    if (checkingCanBuy) return '检查中...';
    if (!product.voInStock) return '缺货';
    if (!product.voIsOnSale) return '已下架';
    if (canBuy && !canBuy.canBuy) return blockedReason || '暂不可购买';
    return '立即购买';
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
          ← 返回
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
                  <span>缺货</span>
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
                {product.voPrice.toLocaleString()} 胡萝卜
              </div>
              {product.voOriginalPrice && product.voOriginalPrice > product.voPrice && (
                <div className={styles.originalPrice}>
                  原价 {product.voOriginalPrice.toLocaleString()} 胡萝卜
                </div>
              )}
            </div>

            <div className={styles.metaInfo}>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>已售:</span>
                <span className={styles.metaValue}>{product.voSoldCount ?? 0} 件</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>库存:</span>
                <span className={styles.metaValue}>
                  {product.voStockType === StockType.Unlimited ? '无限' : `${product.voStock ?? 0} 件`}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>限购:</span>
                <span className={styles.metaValue}>
                  {(product.voLimitPerUser ?? 0) > 0 ? `每人限购 ${product.voLimitPerUser} 件` : '无限制'}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>有效期:</span>
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
          <h2 className={styles.sectionTitle}>商品详情</h2>

          <div className={styles.detailContent}>
            {product.voDescription ? (
              <div className={styles.description}>
                {product.voDescription.split('\n').map((line, index) => (
                  <p key={index}>{line}</p>
                ))}
              </div>
            ) : (
              <p className={styles.noDescription}>暂无详细描述</p>
            )}

            {/* 权益/消耗品特殊信息 */}
            {product.voBenefitValue && (
              <div className={styles.benefitInfo}>
                <h3>权益详情</h3>
                <div className={styles.benefitValue}>
                  {product.voBenefitValue}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* 购买须知 */}
        <div className={styles.noticeSection}>
          <h2 className={styles.sectionTitle}>购买须知</h2>

          <div className={styles.noticeContent}>
            <ul>
              <li>购买前请确认您的胡萝卜余额充足</li>
              <li>权益类商品购买后将自动发放到您的背包</li>
              <li>消耗品道具可在背包中查看和使用</li>
              <li>部分商品有使用期限，请及时使用</li>
              <li>如有问题请联系客服处理</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
};
