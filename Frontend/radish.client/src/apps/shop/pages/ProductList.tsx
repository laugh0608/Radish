import { useEffect, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { LongId } from '@/api/user';
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
  titleLevel?: 'h1' | 'h2';
  backHref?: string;
  getCategoryHref?: (categoryId?: string) => string;
  getProductHref?: (productId: LongId) => string;
  getPageHref?: (page: number) => string;
  onCategoryChange: (categoryId?: string) => void;
  onProductClick: (productId: LongId) => void;
  onSearchChange: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
}

function shouldHandleProductListLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handleProductListLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandleProductListLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

export const ProductList = ({
  categories,
  products,
  selectedCategoryId,
  currentPage,
  totalPages,
  searchKeyword,
  loading,
  titleLevel = 'h1',
  backHref,
  getCategoryHref,
  getProductHref,
  getPageHref,
  onCategoryChange,
  onProductClick,
  onSearchChange,
  onPageChange,
  onBack
}: ProductListProps) => {
  const { t } = useTranslation();
  const [searchInput, setSearchInput] = useState(searchKeyword || '');
  const visibleCategories = categories.filter((category) => (category.voProductCount ?? 0) > 0);
  const hasSelectedVisibleCategory = !!selectedCategoryId
    && visibleCategories.some((category) => String(category.voId) === selectedCategoryId);
  const Title = titleLevel;

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
    const category = visibleCategories.find(c => String(c.voId) === selectedCategoryId);
    return category?.voName || t('shop.allProducts');
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        {backHref ? (
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handleProductListLinkClick(event, onBack)}
          >
            ← {t('shop.back')}
          </a>
        ) : (
          <button type="button" className={styles.backButton} onClick={onBack}>
            ← {t('shop.back')}
          </button>
        )}
        <Title className={styles.title}>{getCurrentCategoryName()}</Title>
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
          {getCategoryHref ? (
            <a
              className={`${styles.categoryTab} ${!hasSelectedVisibleCategory ? styles.active : ''}`}
              href={getCategoryHref(undefined)}
              onClick={(event) => handleProductListLinkClick(event, () => onCategoryChange(undefined))}
            >
              {t('shop.filter.all')}
            </a>
          ) : (
            <button
              type="button"
              className={`${styles.categoryTab} ${!hasSelectedVisibleCategory ? styles.active : ''}`}
              onClick={() => onCategoryChange(undefined)}
            >
              {t('shop.filter.all')}
            </button>
          )}
          {visibleCategories.map((category) => (
            getCategoryHref ? (
              <a
                key={category.voId}
                className={`${styles.categoryTab} ${selectedCategoryId === String(category.voId) ? styles.active : ''}`}
                href={getCategoryHref(String(category.voId))}
                onClick={(event) => handleProductListLinkClick(event, () => onCategoryChange(String(category.voId)))}
              >
                {category.voName}
              </a>
            ) : (
              <button
                key={category.voId}
                type="button"
                className={`${styles.categoryTab} ${selectedCategoryId === String(category.voId) ? styles.active : ''}`}
                onClick={() => onCategoryChange(String(category.voId))}
              >
                {category.voName}
              </button>
            )
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
                  </>
                );

                return productHref ? (
                  <a
                    key={product.voId}
                    className={styles.productCard}
                    href={productHref}
                    onClick={(event) => handleProductListLinkClick(event, () => onProductClick(product.voId))}
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

            {/* 分页 */}
            {totalPages > 1 && (
              <div className={styles.pagination}>
                {getPageHref && currentPage > 1 ? (
                  <a
                    className={styles.pageButton}
                    href={getPageHref(currentPage - 1)}
                    onClick={(event) => handleProductListLinkClick(event, () => onPageChange(currentPage - 1))}
                  >
                    {t('shop.orders.prevPage')}
                  </a>
                ) : (
                  <button
                    type="button"
                    className={styles.pageButton}
                    disabled={currentPage <= 1}
                    onClick={() => onPageChange(currentPage - 1)}
                  >
                    {t('shop.orders.prevPage')}
                  </button>
                )}

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

                    return getPageHref ? (
                      <a
                        key={pageNum}
                        className={`${styles.pageNumber} ${currentPage === pageNum ? styles.active : ''}`}
                        href={getPageHref(pageNum)}
                        aria-current={currentPage === pageNum ? 'page' : undefined}
                        onClick={(event) => handleProductListLinkClick(event, () => onPageChange(pageNum))}
                      >
                        {pageNum}
                      </a>
                    ) : (
                      <button
                        key={pageNum}
                        type="button"
                        className={`${styles.pageNumber} ${currentPage === pageNum ? styles.active : ''}`}
                        onClick={() => onPageChange(pageNum)}
                      >
                        {pageNum}
                      </button>
                    );
                  })}
                </div>

                {getPageHref && currentPage < totalPages ? (
                  <a
                    className={styles.pageButton}
                    href={getPageHref(currentPage + 1)}
                    onClick={(event) => handleProductListLinkClick(event, () => onPageChange(currentPage + 1))}
                  >
                    {t('shop.orders.nextPage')}
                  </a>
                ) : (
                  <button
                    type="button"
                    className={styles.pageButton}
                    disabled={currentPage >= totalPages}
                    onClick={() => onPageChange(currentPage + 1)}
                  >
                    {t('shop.orders.nextPage')}
                  </button>
                )}
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
