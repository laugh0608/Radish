import type { ProductCategory, ProductListItem } from '@/types/shop';
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
  // const { t } = useTranslation(); // 暂时不使用

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

  return (
    <div className={styles.container}>
      {/* 欢迎横幅 */}
      <div className={styles.banner}>
        <div className={styles.bannerContent}>
          <h1 className={styles.bannerTitle}>🛒 欢迎来到萝卜商城</h1>
          <p className={styles.bannerSubtitle}>
            使用胡萝卜购买各种权益道具，让你的萝卜园体验更加精彩！
          </p>
        </div>
      </div>

      {/* 商品分类 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>商品分类</h2>
          <button className={styles.viewAllButton} onClick={onViewAllProducts}>
            查看全部 →
          </button>
        </div>

        <div className={styles.categoriesGrid}>
          {categories.map((category) => (
            <div
              key={category.voId}
              className={styles.categoryCard}
              onClick={() => onCategoryClick(String(category.voId))}
            >
              <div className={styles.categoryIcon}>
                {category.voIcon ? (
                  <img src={category.voIcon} alt={category.voName} />
                ) : (
                  <span className={styles.defaultIcon}>📦</span>
                )}
              </div>
              <div className={styles.categoryInfo}>
                <h3 className={styles.categoryName}>{category.voName}</h3>
                <p className={styles.categoryDescription}>
                  {category.voDescription || '精选商品'}
                </p>
                <span className={styles.categoryCount}>
                  {category.voProductCount ?? 0} 件商品
                </span>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* 推荐商品 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>推荐商品</h2>
          <button className={styles.viewAllButton} onClick={onViewAllProducts}>
            查看更多 →
          </button>
        </div>

        <div className={styles.productsGrid}>
          {featuredProducts.map((product) => (
            <div
              key={product.voId}
              className={styles.productCard}
              onClick={() => onProductClick(product.voId)}
            >
              <div className={styles.productImage}>
                {product.voCoverImage ? (
                  <img src={product.voCoverImage} alt={product.voName} />
                ) : product.voIcon ? (
                  <img src={product.voIcon} alt={product.voName} />
                ) : (
                  <div className={styles.defaultProductImage}>
                    <span>🎁</span>
                  </div>
                )}

                {product.voHasDiscount && (
                  <div className={styles.discountBadge}>
                    特价
                  </div>
                )}

                {!product.voInStock && (
                  <div className={styles.outOfStockOverlay}>
                    <span>缺货</span>
                  </div>
                )}
              </div>

              <div className={styles.productInfo}>
                <h3 className={styles.productName}>{product.voName}</h3>

                <div className={styles.productPrice}>
                  <span className={styles.currentPrice}>
                    {product.voPrice.toLocaleString()} 胡萝卜
                  </span>
                  {product.voOriginalPrice && product.voOriginalPrice > product.voPrice && (
                    <span className={styles.originalPrice}>
                      {product.voOriginalPrice.toLocaleString()}
                    </span>
                  )}
                </div>

                <div className={styles.productMeta}>
                  <span className={styles.soldCount}>
                    已售 {product.voSoldCount ?? 0}
                  </span>
                  <span className={styles.duration}>
                    {product.voDurationDisplay ?? ''}
                  </span>
                </div>
              </div>
            </div>
          ))}
        </div>

        {featuredProducts.length === 0 && (
          <div className={styles.emptyState}>
            <div className={styles.emptyIcon}>🛍️</div>
            <p>暂无推荐商品</p>
          </div>
        )}
      </section>

      {/* 购物指南 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>购物指南</h2>
        </div>

        <div className={styles.guideGrid}>
          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>💰</div>
            <h3>获取胡萝卜</h3>
            <p>通过发帖、评论、点赞等活动获得胡萝卜奖励</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>🎁</div>
            <h3>购买权益</h3>
            <p>使用胡萝卜购买徽章、头像框、称号等装饰权益</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>🎮</div>
            <h3>使用道具</h3>
            <p>购买改名卡、经验卡等消耗品道具提升体验</p>
          </div>

          <div className={styles.guideCard}>
            <div className={styles.guideIcon}>📦</div>
            <h3>管理背包</h3>
            <p>在背包中查看已购买的权益和道具，激活使用</p>
          </div>
        </div>
      </section>
    </div>
  );
};
