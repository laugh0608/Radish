import type { SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { ProductCategory, ProductListItem } from '@/types/shop';
import { resolveMediaUrl } from '@/utils/media';
import styles from './ShopHome.module.css';

interface ShopHomeProps {
  categories: ProductCategory[];
  featuredProducts: ProductListItem[];
  loading: boolean;
  onCategoryClick: (categoryId: string) => void;
  onProductClick: (productId: number) => void;
  onViewAllProducts: () => void;
}

export const ShopHome = ({
  categories,
  featuredProducts,
  loading,
  onCategoryClick,
  onProductClick,
  onViewAllProducts
}: ShopHomeProps) => {
  const { t } = useTranslation();

  const handleCategoryImageError = (event: SyntheticEvent<HTMLImageElement>) => {
    event.currentTarget.style.display = 'none';
  };

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

  return (
    <div className={styles.container}>
      {/* 欢迎横幅 */}
      <div className={styles.banner}>
        <div className={styles.bannerContent}>
          <h1 className={styles.bannerTitle}>🛒 {t('shop.welcomeTitle')}</h1>
          <p className={styles.bannerSubtitle}>{t('shop.welcomeSubtitle')}</p>
        </div>
      </div>

      {/* 商品分类 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>{t('shop.categoryTitle')}</h2>
          <button className={styles.viewAllButton} onClick={onViewAllProducts}>
            {t('shop.viewAll')} →
          </button>
        </div>

        <div className={styles.categoriesGrid}>
          {categories.map((category) => {
            const categoryIconUrl = resolveMediaUrl(category.voIcon);

            return (
              <div
                key={category.voId}
                className={styles.categoryCard}
                onClick={() => onCategoryClick(String(category.voId))}
              >
                <div className={styles.categoryIcon}>
                  {categoryIconUrl ? (
                    <img src={categoryIconUrl} alt={category.voName} onError={handleCategoryImageError} />
                  ) : (
                    <span className={styles.defaultIcon}>📦</span>
                  )}
                </div>
                <div className={styles.categoryInfo}>
                  <h3 className={styles.categoryName}>{category.voName}</h3>
                  <p className={styles.categoryDescription}>
                    {category.voDescription || t('shop.categoryFallbackDescription')}
                  </p>
                  <span className={styles.categoryCount}>
                    {t('shop.productCount', { count: category.voProductCount ?? 0 })}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* 推荐商品 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>{t('shop.featuredProducts')}</h2>
          <button className={styles.viewAllButton} onClick={onViewAllProducts}>
            {t('shop.viewMore')} →
          </button>
        </div>

        <div className={styles.productsGrid}>
          {featuredProducts.map((product) => {
            const coverImageUrl = resolveMediaUrl(product.voCoverImage);
            const iconImageUrl = resolveMediaUrl(product.voIcon);

            return (
              <div
                key={product.voId}
                className={styles.productCard}
                onClick={() => onProductClick(product.voId)}
              >
                <div className={styles.productImage}>
                  {coverImageUrl ? (
                    <img src={coverImageUrl} alt={product.voName} />
                  ) : iconImageUrl ? (
                    <img src={iconImageUrl} alt={product.voName} />
                  ) : (
                    <div className={styles.defaultProductImage}>
                      <span>🎁</span>
                    </div>
                  )}

                  {product.voHasDiscount && (
                    <div className={styles.discountBadge}>
                      {t('shop.discount')}
                    </div>
                  )}

                  {!product.voInStock && (
                    <div className={styles.outOfStockOverlay}>
                      <span>{t('shop.outOfStock')}</span>
                    </div>
                  )}
                </div>

                <div className={styles.productInfo}>
                  <h3 className={styles.productName}>{product.voName}</h3>

                  <div className={styles.productPrice}>
                    <span className={styles.currentPrice}>
                      {product.voPrice.toLocaleString()} {t('shop.currency.carrot')}
                    </span>
                    {product.voOriginalPrice && product.voOriginalPrice > product.voPrice && (
                      <span className={styles.originalPrice}>
                        {product.voOriginalPrice.toLocaleString()}
                      </span>
                    )}
                  </div>

                  <div className={styles.productMeta}>
                    <span className={styles.soldCount}>
                      {t('shop.soldCount', { count: product.voSoldCount ?? 0 })}
                    </span>
                    <span className={styles.duration}>
                      {product.voDurationDisplay ?? ''}
                    </span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {featuredProducts.length === 0 && (
          <div className={styles.emptyState}>
            <div className={styles.emptyIcon}>🛍️</div>
            <p>{t('shop.featuredEmpty')}</p>
          </div>
        )}
      </section>

      {/* 购物指南 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>{t('shop.guideTitle')}</h2>
        </div>

        <div className={styles.guideGrid}>
          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>💰</div>
            <h3>{t('shop.guide.earn.title')}</h3>
            <p>{t('shop.guide.earn.description')}</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>🎁</div>
            <h3>{t('shop.guide.benefit.title')}</h3>
            <p>{t('shop.guide.benefit.description')}</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>🎮</div>
            <h3>{t('shop.guide.item.title')}</h3>
            <p>{t('shop.guide.item.description')}</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>📦</div>
            <h3>{t('shop.guide.inventory.title')}</h3>
            <p>{t('shop.guide.inventory.description')}</p>
          </div>
        </div>
      </section>
    </div>
  );
};
