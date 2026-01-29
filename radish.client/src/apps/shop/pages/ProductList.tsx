import { useState } from 'react';
import type { ProductCategoryData, ProductListItemData } from '@/utils/viewModelMapper';
import { getProductTypeDisplay } from '@/api/shop';
import styles from './ProductList.module.css';

interface ProductListProps {
  categories: ProductCategoryData[];
  products: ProductListItemData[];
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
  // const { t } = useTranslation(); // 暂时不使用
  const [searchInput, setSearchInput] = useState(searchKeyword || '');

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
    if (!selectedCategoryId) return '全部商品';
    const category = categories.find(c => c.id === selectedCategoryId);
    return category?.name || '未知分类';
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航 */}
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← 返回
        </button>
        <h1 className={styles.title}>{getCurrentCategoryName()}</h1>
      </div>

      {/* 搜索和筛选栏 */}
      <div className={styles.filterBar}>
        <div className={styles.searchBox}>
          <input
            type="text"
            placeholder="搜索商品..."
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
            全部
          </button>
          {categories.map((category) => (
            <button
              key={category.id}
              className={`${styles.categoryTab} ${selectedCategoryId === category.id ? styles.active : ''}`}
              onClick={() => onCategoryChange(category.id)}
            >
              {category.name}
            </button>
          ))}
        </div>
      </div>

      {/* 搜索结果提示 */}
      {searchKeyword && (
        <div className={styles.searchResult}>
          搜索 "{searchKeyword}" 的结果，共找到 {products.length} 件商品
        </div>
      )}

      {/* 商品列表 */}
      <div className={styles.content}>
        {loading ? (
          <div className={styles.loading}>
            <div className={styles.spinner}></div>
            <p>加载中...</p>
          </div>
        ) : products.length > 0 ? (
          <>
            <div className={styles.productsGrid}>
              {products.map((product) => (
                <div
                  key={product.id}
                  className={styles.productCard}
                  onClick={() => onProductClick(product.id)}
                >
                  <div className={styles.productImage}>
                    {product.coverImage ? (
                      <img src={product.coverImage} alt={product.name} />
                    ) : product.icon ? (
                      <img src={product.icon} alt={product.name} />
                    ) : (
                      <div className={styles.defaultProductImage}>
                        <span>🎁</span>
                      </div>
                    )}

                    {product.hasDiscount && (
                      <div className={styles.discountBadge}>
                        特价
                      </div>
                    )}

                    {!product.inStock && (
                      <div className={styles.outOfStockOverlay}>
                        <span>缺货</span>
                      </div>
                    )}
                  </div>

                  <div className={styles.productInfo}>
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
                          {product.originalPrice.toLocaleString()}
                        </span>
                      )}
                    </div>

                    <div className={styles.productMeta}>
                      <span className={styles.soldCount}>
                        已售 {product.soldCount}
                      </span>
                      <span className={styles.duration}>
                        {product.durationDisplay}
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {/* 分页 */}
            {totalPages > 1 && (
              <div className={styles.pagination}>
                <button
                  className={styles.pageButton}
                  disabled={currentPage <= 1}
                  onClick={() => onPageChange(currentPage - 1)}
                >
                  上一页
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
                  下一页
                </button>
              </div>
            )}
          </>
        ) : (
          <div className={styles.emptyState}>
            <div className={styles.emptyIcon}>🛍️</div>
            <h3>暂无商品</h3>
            <p>
              {searchKeyword
                ? `没有找到包含 "${searchKeyword}" 的商品`
                : selectedCategoryId
                ? '该分类下暂无商品'
                : '暂无商品上架'
              }
            </p>
            {searchKeyword && (
              <button className={styles.clearSearchButton} onClick={clearSearch}>
                清除搜索条件
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};