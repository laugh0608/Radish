import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ProductCategory, ProductListItem } from '@/types/shop';
import { getProductTypeDisplay } from '@/api/shop';
import { resolveMediaUrl } from '@/utils/media';
import styles from './ProductList.module.css';

interface ProductListProps {
  categories: ProductCategory[];
  products: ProductListItem[];
  selectedCategoryId?: string;
  currentPage: number;
  totalPages: number;
  searchKeyword?: string;
  loading: boolean;
  onCategoryChange: (categoryId?: string) => void;
  onProductClick: (productId: number) => void;
  onSearchChange: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
}

export const ProductList = ({
  categories,
  products,
  selectedCategoryId,
  currentPage,
  totalPages,
  searchKeyword,
  loading,
  onCategoryChange,
  onProductClick,
  onSearchChange,
  onPageChange,
  onBack
}: ProductListProps) => {
  const { t } = useTranslation();
  const [searchInput, setSearchInput] = useState(searchKeyword || '');

  useEffect(() => {
    setSearchInput(searchKeyword || '');
  }, [searchKeyword]);

  // 处理搜索
  const handleSearch = () => {
    onSearchChange(searchInput);
  };

  const handleSearchKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSearch();
    }
  };

  // 清除搜索
  const clearSearch = () => {
    setSearchInput('');
    onSearchChange('');
  };

  // 获取当前分类名称
  const getCurrentCategoryName = () => {
    if (!selectedCategoryId) return t('shop.allProducts');
    const category = categories.find(c => String(c.voId) === selectedCategoryId);
    return category?.voName || t('shop.unknownCategory');
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← {t('shop.back')}
        </button>
        <h1 className={styles.title}>{getCurrentCategoryName()}</h1>
      </div>

      {/* 搜索和筛选栏 */}
      <div className={styles.filterBar}>
        <div className={styles.searchBox}>
          <input
            type="text"
            placeholder={t('shop.searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyPress={handleSearchKeyPress}
            className={styles.searchInput}
          />
          <button className={styles.searchButton} onClick={handleSearch}>
            🔍
          </button>
          {searchKeyword && (
            <button className={styles.clearButton} onClick={clearSearch}>
              ✕
            </button>
          )}
        </div>

        <div className={styles.categoryTabs}>
          <button
            className={`${styles.categoryTab} ${!selectedCategoryId ? styles.active : ''}`}
            onClick={() => onCategoryChange(undefined)}
          >
            {t('shop.filter.all')}
          </button>
          {categories.map((category) => (
            <button
              key={category.voId}
              className={`${styles.categoryTab} ${selectedCategoryId === String(category.voId) ? styles.active : ''}`}
              onClick={() => onCategoryChange(String(category.voId))}
            >
              {category.voName}
            </button>
          ))}
        </div>
      </div>

      {/* 搜索结果提示 */}
      {searchKeyword && (
        <div className={styles.searchResult}>
          {t('shop.searchResult', { keyword: searchKeyword, count: products.length })}
        </div>
      )}

      {/* 商品列表 */}
      <div className={styles.content}>
        {loading ? (
          <div className={styles.loading}>
            <div className={styles.spinner}></div>
            <p>{t('shop.loading')}</p>
          </div>
        ) : products.length > 0 ? (
          <>
            <div className={styles.productsGrid}>
              {products.map((product) => {
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

            {/* 分页 */}
            {totalPages > 1 && (
              <div className={styles.pagination}>
                <button
                  className={styles.pageButton}
                  disabled={currentPage <= 1}
                  onClick={() => onPageChange(currentPage - 1)}
                >
                  {t('shop.orders.prevPage')}
                </button>

                <div className={styles.pageNumbers}>
                  {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
                    let pageNum;
                    if (totalPages <= 7) {
                      pageNum = i + 1;
                    } else if (currentPage <= 4) {
                      pageNum = i + 1;
                    } else if (currentPage >= totalPages - 3) {
                      pageNum = totalPages - 6 + i;
                    } else {
                      pageNum = currentPage - 3 + i;
                    }

                    return (
                      <button
                        key={pageNum}
                        className={`${styles.pageNumber} ${currentPage === pageNum ? styles.active : ''}`}
                        onClick={() => onPageChange(pageNum)}
                      >
                        {pageNum}
                      </button>
                    );
                  })}
                </div>

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
            <div className={styles.emptyIcon}>🛍️</div>
            <h3>{t('shop.emptyProducts')}</h3>
            <p>
              {searchKeyword
                ? t('shop.emptyProductsSearch', { keyword: searchKeyword })
                : selectedCategoryId
                ? t('shop.emptyProductsCategory')
                : t('shop.emptyProductsGeneral')
              }
            </p>
            {searchKeyword && (
              <button className={styles.clearSearchButton} onClick={clearSearch}>
                {t('shop.clearSearch')}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
