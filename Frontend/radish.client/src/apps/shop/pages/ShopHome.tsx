import type { MouseEvent, SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { ProductCategory, ProductListItem } from '@/types/shop';
import type { LongId } from '@/api/user';
import { resolveMediaUrl } from '@/utils/media';
import styles from './ShopHome.module.css';

interface ShopHomeProps {
  categories: ProductCategory[];
  featuredProducts: ProductListItem[];
  loading: boolean;
  bannerTitleLevel?: 'h1' | 'h2';
  getCategoryHref?: (categoryId: string) => string;
  getProductHref?: (productId: LongId) => string;
  viewAllProductsHref?: string;
  onCategoryClick: (categoryId: string) => void;
  onProductClick: (productId: LongId) => void;
  onViewAllProducts: () => void;
}

function shouldHandleShopHomeLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handleShopHomeLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandleShopHomeLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

export const ShopHome = ({
  categories,
  featuredProducts,
  loading,
  bannerTitleLevel = 'h1',
  getCategoryHref,
  getProductHref,
  viewAllProductsHref,
  onCategoryClick,
  onProductClick,
  onViewAllProducts
}: ShopHomeProps) => {
  const { t } = useTranslation();
  const visibleCategories = categories.filter((category) => (category.voProductCount ?? 0) > 0);
  const BannerTitle = bannerTitleLevel;

  const handleCategoryImageError = (event: SyntheticEvent<HTMLImageElement>) => {
    event.currentTarget.style.display = 'none';
  };

  const renderViewAllAction = (label: string) => (
    viewAllProductsHref ? (
      <a
        className={styles.viewAllButton}
        href={viewAllProductsHref}
        onClick={(event) => handleShopHomeLinkClick(event, onViewAllProducts)}
      >
        {label} →
      </a>
    ) : (
      <button type="button" className={styles.viewAllButton} onClick={onViewAllProducts}>
        {label} →
      </button>
    )
  );

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
          <BannerTitle className={styles.bannerTitle}>🛒 {t('shop.welcomeTitle')}</BannerTitle>
          <p className={styles.bannerSubtitle}>{t('shop.welcomeSubtitle')}</p>
        </div>
      </div>

      {/* 商品分类 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>{t('shop.categoryTitle')}</h2>
          {renderViewAllAction(t('shop.viewAll'))}
        </div>

        <div className={styles.categoriesGrid}>
          {visibleCategories.map((category) => {
            const categoryId = String(category.voId);
            const categoryIconUrl = resolveMediaUrl(category.voIcon);
            const categoryHref = getCategoryHref?.(categoryId);
            const categoryContent = (
              <>
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
              </>
            );

            return categoryHref ? (
              <a
                key={category.voId}
                className={styles.categoryCard}
                href={categoryHref}
                onClick={(event) => handleShopHomeLinkClick(event, () => onCategoryClick(categoryId))}
              >
                {categoryContent}
              </a>
            ) : (
              <div
                key={category.voId}
                className={styles.categoryCard}
                onClick={() => onCategoryClick(categoryId)}
              >
                {categoryContent}
              </div>
            );
          })}
        </div>
      </section>

      {/* 推荐商品 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>{t('shop.featuredProducts')}</h2>
          {renderViewAllAction(t('shop.viewMore'))}
        </div>

        <div className={styles.productsGrid}>
          {featuredProducts.map((product) => {
            const coverImageUrl = resolveMediaUrl(product.voCoverImage);
            const iconImageUrl = resolveMediaUrl(product.voIcon);
            const productHref = getProductHref?.(product.voId);
            const productContent = (
              <>
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
              </>
            );

            return productHref ? (
              <a
                key={product.voId}
                className={styles.productCard}
                href={productHref}
                onClick={(event) => handleShopHomeLinkClick(event, () => onProductClick(product.voId))}
              >
                {productContent}
              </a>
            ) : (
              <div
                key={product.voId}
                className={styles.productCard}
                onClick={() => onProductClick(product.voId)}
              >
                {productContent}
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
