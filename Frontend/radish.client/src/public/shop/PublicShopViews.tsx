import { useEffect, useMemo, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getProductTypeDisplay, isUnlimitedStockType } from '@/api/shop';
import type { LongId } from '@/api/user';
import type { Product, ProductCategory, ProductListItem } from '@/types/shop';
import { resolveMediaUrl } from '@/utils/media';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import styles from './PublicShopApp.module.css';

type PublicShopTranslate = (key: string, options?: Record<string, unknown>) => string;

const publicBrowseGuideItems = [
  {
    labelKey: 'shop.public.guideBrowseLabel',
    valueKey: 'shop.public.guideBrowseValue',
  },
  {
    labelKey: 'shop.public.guideNextLabel',
    valueKey: 'shop.public.guideNextValue',
  },
  {
    labelKey: 'shop.public.guideBoundaryLabel',
    valueKey: 'shop.public.guideBoundaryValue',
  },
] as const;

const publicDetailGuideItems = [
  {
    labelKey: 'shop.public.detailGuideFocusLabel',
    valueKey: 'shop.public.detailGuideFocusValue',
  },
  {
    labelKey: 'shop.public.detailGuideNextLabel',
    valueKey: 'shop.public.detailGuideNextValue',
  },
  {
    labelKey: 'shop.public.detailGuideBoundaryLabel',
    valueKey: 'shop.public.detailGuideBoundaryValue',
  },
] as const;

interface PublicShopHomeViewProps {
  categories: ProductCategory[];
  featuredProducts: ProductListItem[];
  loading: boolean;
  loggedIn: boolean;
  viewAllProductsHref: string;
  getCategoryHref: (categoryId: string) => string;
  getProductHref: (productId: LongId) => string;
  onCategoryClick: (categoryId: string) => void;
  onProductClick: (productId: LongId) => void;
  onViewAllProducts: () => void;
}

interface PublicShopProductsViewProps {
  categories: ProductCategory[];
  products: ProductListItem[];
  selectedCategoryId?: string;
  currentPage: number;
  totalPages: number;
  searchKeyword?: string;
  loading: boolean;
  loggedIn: boolean;
  backHref: string;
  getCategoryHref: (categoryId?: string) => string;
  getProductHref: (productId: LongId) => string;
  getPageHref: (page: number) => string;
  onCategoryChange: (categoryId?: string) => void;
  onProductClick: (productId: LongId) => void;
  onSearchChange: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onBack: () => void;
}

interface PublicShopDetailViewProps {
  product: Product;
  loggedIn: boolean;
  authReady: boolean;
  detailBackLabel: string;
  detailBackHref: string;
  detailBackHint: string;
  shareBusy: boolean;
  shareState: 'idle' | 'success' | 'error';
  purchaseError: string | null;
  purchaseReturnPath: string | null;
  purchaseBusy: boolean;
  productAvailableForPurchase: boolean;
  purchaseActionLabel: string;
  purchaseActionIcon: string;
  onBack: () => void;
  onCopyShare: () => void;
  onPurchaseLinkClick: (event: MouseEvent<HTMLAnchorElement>, product: Product) => void;
}

function shouldHandlePublicShopLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handlePublicShopLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandlePublicShopLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

function formatProductPrice(value: number): string {
  return value.toLocaleString();
}

function toGuideItems(
  t: PublicShopTranslate,
  items: readonly { labelKey: string; valueKey: string }[]
) {
  return items.map((item) => ({
    label: t(item.labelKey),
    value: t(item.valueKey),
  }));
}

function getProductImageUrl(product: Product | ProductListItem): string | null {
  return resolveMediaUrl(product.voCoverImage || product.voIcon);
}

function toProductTypeText(product: Product | ProductListItem): string {
  if ('voProductTypeDisplay' in product && product.voProductTypeDisplay?.trim()) {
    return product.voProductTypeDisplay;
  }

  return getProductTypeDisplay(product.voProductType);
}

function toStockText(t: PublicShopTranslate, product: Product): string {
  return isUnlimitedStockType(product.voStockType)
    ? t('shop.stock.unlimited')
    : t('shop.productCount', { count: product.voStock ?? 0 });
}

function toLimitText(t: PublicShopTranslate, product: Product): string {
  return (product.voLimitPerUser ?? 0) > 0
    ? t('shop.limit.perUser', { count: product.voLimitPerUser })
    : t('shop.limit.unlimited');
}

function toProductStatusText(t: PublicShopTranslate, product: Product): string {
  if (product.voInStock === false) {
    return t('shop.public.statusOutOfStock');
  }

  if (product.voIsOnSale === false) {
    return t('shop.public.statusOffSale');
  }

  return t('shop.public.statusOnSale');
}

function toProductListStatusText(t: PublicShopTranslate, product: ProductListItem): string {
  return product.voInStock === false
    ? t('shop.public.statusOutOfStock')
    : t('shop.public.statusOnSale');
}

function toProductListStockSignalText(t: PublicShopTranslate, product: ProductListItem): string {
  return product.voInStock === false
    ? t('shop.public.rowStockSignalUnavailable')
    : t('shop.public.rowStockSignalAvailable');
}

function toCategoryNameById(categories: ProductCategory[]): Map<string, string> {
  return new Map(
    categories
      .map((category) => [String(category.voId), category.voName.trim()] as const)
      .filter(([, categoryName]) => categoryName.length > 0)
  );
}

function ProductImage({ product, size = 'normal' }: { product: Product | ProductListItem; size?: 'normal' | 'small' }) {
  const { t } = useTranslation();
  const imageUrl = getProductImageUrl(product);

  return (
    <div className={size === 'small' ? styles.productThumbSmall : styles.productThumb}>
      {imageUrl ? (
        <img src={imageUrl} alt={product.voName} />
      ) : (
        <div className={styles.productThumbFallback}>
          <Icon icon="mdi:gift-outline" size={size === 'small' ? 20 : 30} />
          <span>{t('desktop.apps.shop.name')}</span>
        </div>
      )}
    </div>
  );
}

function ProductTagRow({ product, categoryName }: { product: ProductListItem; categoryName?: string }) {
  const { t } = useTranslation();
  const tags = [
    toProductTypeText(product),
    categoryName?.trim() || null,
    `${t('shop.public.rowStatusLabel')}${toProductListStatusText(t, product)}`,
    `${t('shop.public.rowStockSignalLabel')}${toProductListStockSignalText(t, product)}`,
    product.voDurationDisplay || t('shop.public.durationFallback'),
  ].filter((tag): tag is string => Boolean(tag));

  return (
    <div className={styles.productTagRow}>
      {tags.map((tag) => (
        <span key={tag} className={styles.metaChip}>{tag}</span>
      ))}
    </div>
  );
}

function ProductRow({
  product,
  href,
  actionLabel,
  categoryName,
  onOpen
}: {
  product: ProductListItem;
  href: string;
  actionLabel: string;
  categoryName?: string;
  onOpen: (productId: LongId) => void;
}) {
  const { t } = useTranslation();

  return (
    <a
      className={styles.productRow}
      href={href}
      onClick={(event) => handlePublicShopLinkClick(event, () => onOpen(product.voId))}
    >
      <ProductImage product={product} size="small" />
      <div className={styles.productRowInfo}>
        <h3 className={styles.productRowTitle}>{product.voName}</h3>
        <p className={styles.productRowCopy}>
          {product.voDurationDisplay || t('shop.public.productRowFallback')}
        </p>
        <ProductTagRow product={product} categoryName={categoryName} />
      </div>
      <div className={styles.productRowMeta}>
        <span className={styles.productRowPrice}>
          {formatProductPrice(product.voPrice)} {t('shop.currency.carrot')}
        </span>
        <span className={styles.productRowSold}>{t('shop.soldCount', { count: product.voSoldCount ?? 0 })}</span>
        <span className={styles.productRowAction}>{actionLabel}</span>
      </div>
    </a>
  );
}

function ProductRows({
  products,
  categoryNameById,
  getProductHref,
  actionLabel,
  onProductClick
}: {
  products: ProductListItem[];
  categoryNameById?: Map<string, string>;
  getProductHref: (productId: LongId) => string;
  actionLabel: string;
  onProductClick: (productId: LongId) => void;
}) {
  const { t } = useTranslation();

  if (products.length === 0) {
    return (
      <div className={styles.shopEmptyState}>
        <Icon icon="mdi:shopping-search-outline" size={30} />
        <h3>{t('shop.emptyProducts')}</h3>
        <p>{t('shop.emptyProductsGeneral')}</p>
      </div>
    );
  }

  return (
    <div className={styles.productRows}>
      {products.map((product) => (
        <ProductRow
          key={product.voId}
          product={product}
          href={getProductHref(product.voId)}
          actionLabel={actionLabel}
          categoryName={categoryNameById?.get(String(product.voCategoryId))}
          onOpen={onProductClick}
        />
      ))}
    </div>
  );
}

function CategoryPills({
  categories,
  selectedCategoryId,
  allHref,
  getCategoryHref,
  onSelectAll,
  onSelectCategory
}: {
  categories: ProductCategory[];
  selectedCategoryId?: string;
  allHref: string;
  getCategoryHref: (categoryId: string) => string;
  onSelectAll: () => void;
  onSelectCategory: (categoryId: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className={styles.categoryPills} aria-label={t('shop.categoryTitle')}>
      <a
        className={`${styles.categoryPill} ${!selectedCategoryId ? styles.categoryPillActive : ''}`}
        href={allHref}
        onClick={(event) => handlePublicShopLinkClick(event, onSelectAll)}
      >
        {t('shop.filter.all')}
      </a>
      {categories.slice(0, 8).map((category) => {
        const categoryId = String(category.voId);

        return (
          <a
            key={category.voId}
            className={`${styles.categoryPill} ${selectedCategoryId === categoryId ? styles.categoryPillActive : ''}`}
            href={getCategoryHref(categoryId)}
            onClick={(event) => handlePublicShopLinkClick(event, () => onSelectCategory(categoryId))}
          >
            {category.voName}
          </a>
        );
      })}
    </div>
  );
}

function PublicShopBrowseRail({
  categoriesCount,
  productCount,
  loggedIn
}: {
  categoriesCount: number;
  productCount: number;
  loggedIn: boolean;
}) {
  const { t } = useTranslation();

  return (
    <aside className={styles.shopRail} aria-label={t('shop.public.railLabel')}>
      <PublicReadingGuide
        label={t('shop.public.guideKicker')}
        title={t('shop.public.guideTitle')}
        description={t('shop.public.guideDescription')}
        items={toGuideItems(t, publicBrowseGuideItems)}
      />

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:shopping-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('shop.public.railBrowseTitle')}</h2>
            <p className={styles.railText}>{t('shop.public.railBrowseDescription')}</p>
          </div>
        </div>
        <div className={styles.railStatGrid}>
          <span className={styles.railStat}>
            <strong>{categoriesCount}</strong>
            <span>{t('shop.public.railCategoryStat')}</span>
          </span>
          <span className={styles.railStat}>
            <strong>{productCount}</strong>
            <span>{t('shop.public.railProductStat')}</span>
          </span>
        </div>
      </section>

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon={loggedIn ? 'mdi:cart-check' : 'mdi:login-variant'} size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('shop.public.railPurchaseTitle')}</h2>
            <p className={styles.railText}>
              {loggedIn ? t('shop.public.railPurchaseSignedIn') : t('shop.public.railPurchaseGuest')}
            </p>
          </div>
        </div>
      </section>

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:shield-account-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('shop.public.railPrivateTitle')}</h2>
            <p className={styles.railText}>{t('shop.public.railPrivateDescription')}</p>
          </div>
        </div>
      </section>
    </aside>
  );
}

function BrowseLoading() {
  const { t } = useTranslation();

  return (
    <div className={styles.shopLoadingState}>
      <Icon icon="mdi:progress-clock" size={28} />
      <span>{t('shop.loading')}</span>
    </div>
  );
}

export function PublicShopHomeView({
  categories,
  featuredProducts,
  loading,
  loggedIn,
  viewAllProductsHref,
  getCategoryHref,
  getProductHref,
  onCategoryClick,
  onProductClick,
  onViewAllProducts
}: PublicShopHomeViewProps) {
  const { t } = useTranslation();
  const featuredProduct = featuredProducts[0] ?? null;
  const rowProducts = featuredProduct ? featuredProducts.slice(1, 6) : featuredProducts.slice(0, 5);
  const actionLabel = loggedIn ? t('shop.public.rowDetailActionSignedIn') : t('shop.public.rowDetailActionGuest');
  const categoryNameById = useMemo(() => toCategoryNameById(categories), [categories]);
  const featuredCategoryName = featuredProduct ? categoryNameById.get(String(featuredProduct.voCategoryId)) : null;

  return (
    <div className={styles.shopGrid}>
      <div className={styles.shopMain}>
        <section className={styles.shopToolbar}>
          <div>
            <p className={styles.kicker}>{t('shop.public.guideKicker')}</p>
            <h2 className={styles.shopToolbarTitle}>{t('shop.public.homeToolbarTitle')}</h2>
            <p className={styles.shopToolbarDescription}>{t('shop.public.homeToolbarDescription')}</p>
          </div>
          <a
            className={styles.secondaryButton}
            href={viewAllProductsHref}
            onClick={(event) => handlePublicShopLinkClick(event, onViewAllProducts)}
          >
            <Icon icon="mdi:view-grid-outline" size={18} />
            <span>{t('shop.public.browseProducts')}</span>
          </a>
        </section>

        <CategoryPills
          categories={categories}
          allHref={viewAllProductsHref}
          getCategoryHref={getCategoryHref}
          onSelectAll={onViewAllProducts}
          onSelectCategory={onCategoryClick}
        />

        {loading ? (
          <BrowseLoading />
        ) : featuredProduct ? (
          <>
            <a
              className={styles.featuredProduct}
              href={getProductHref(featuredProduct.voId)}
              onClick={(event) => handlePublicShopLinkClick(event, () => onProductClick(featuredProduct.voId))}
            >
              <ProductImage product={featuredProduct} />
              <div className={styles.featuredProductBody}>
                <div className={styles.productTagRow}>
                  <span className={styles.metaChip}>{t('shop.featuredProducts')}</span>
                  <span className={styles.metaChip}>{toProductTypeText(featuredProduct)}</span>
                  {featuredCategoryName ? (
                    <span className={styles.metaChip}>{featuredCategoryName}</span>
                  ) : null}
                  <span className={styles.metaChip}>
                    {t('shop.public.rowStatusLabel')}{toProductListStatusText(t, featuredProduct)}
                  </span>
                  <span className={styles.metaChip}>
                    {t('shop.public.rowStockSignalLabel')}{toProductListStockSignalText(t, featuredProduct)}
                  </span>
                </div>
                <h3 className={styles.featuredProductTitle}>{featuredProduct.voName}</h3>
                <p className={styles.featuredProductCopy}>{t('shop.public.featuredProductDescription')}</p>
              </div>
              <div className={styles.featuredProductMeta}>
                <span className={styles.featuredProductPrice}>
                  {formatProductPrice(featuredProduct.voPrice)} {t('shop.currency.carrot')}
                </span>
                <span className={styles.metaChip}>{t('shop.soldCount', { count: featuredProduct.voSoldCount ?? 0 })}</span>
                <span className={styles.primaryButton}>{actionLabel}</span>
              </div>
            </a>

            <ProductRows
              products={rowProducts}
              categoryNameById={categoryNameById}
              getProductHref={getProductHref}
              actionLabel={actionLabel}
              onProductClick={onProductClick}
            />
          </>
        ) : (
          <div className={styles.shopEmptyState}>
            <Icon icon="mdi:shopping-search-outline" size={30} />
            <h3>{t('shop.featuredEmpty')}</h3>
            <p>{t('shop.emptyProductsGeneral')}</p>
          </div>
        )}
      </div>

      <PublicShopBrowseRail
        categoriesCount={categories.length}
        productCount={featuredProducts.length}
        loggedIn={loggedIn}
      />
    </div>
  );
}

export function PublicShopProductsView({
  categories,
  products,
  selectedCategoryId,
  currentPage,
  totalPages,
  searchKeyword,
  loading,
  loggedIn,
  backHref,
  getCategoryHref,
  getProductHref,
  getPageHref,
  onCategoryChange,
  onProductClick,
  onSearchChange,
  onPageChange,
  onBack
}: PublicShopProductsViewProps) {
  const { t } = useTranslation();
  const [searchInput, setSearchInput] = useState(searchKeyword || '');
  const actionLabel = loggedIn ? t('shop.public.rowDetailActionSignedIn') : t('shop.public.rowDetailActionGuest');
  const categoryNameById = useMemo(() => toCategoryNameById(categories), [categories]);

  useEffect(() => {
    setSearchInput(searchKeyword || '');
  }, [searchKeyword]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSearchChange(searchInput.trim());
  };

  return (
    <div className={styles.shopGrid}>
      <div className={styles.shopMain}>
        <section className={styles.shopToolbar}>
          <div>
            <p className={styles.kicker}>{t('shop.public.guideKicker')}</p>
            <h2 className={styles.shopToolbarTitle}>{t('shop.public.productsToolbarTitle')}</h2>
            <p className={styles.shopToolbarDescription}>{t('shop.public.productsToolbarDescription')}</p>
          </div>
          <a
            className={styles.secondaryButton}
            href={backHref}
            onClick={(event) => handlePublicShopLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{t('shop.public.backToHome')}</span>
          </a>
        </section>

        <form className={styles.shopSearchForm} onSubmit={handleSubmit}>
          <div className={styles.shopSearchInputWrap}>
            <Icon icon="mdi:magnify" size={18} />
            <input
              className={styles.shopSearchInput}
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder={t('shop.searchPlaceholder')}
            />
          </div>
          <button type="submit" className={styles.primaryButton}>
            {t('shop.public.searchSubmit')}
          </button>
          {searchKeyword ? (
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={() => {
                setSearchInput('');
                onSearchChange('');
              }}
            >
              {t('shop.clearSearch')}
            </button>
          ) : null}
        </form>

        <CategoryPills
          categories={categories}
          selectedCategoryId={selectedCategoryId}
          allHref={getCategoryHref(undefined)}
          getCategoryHref={(categoryId) => getCategoryHref(categoryId)}
          onSelectAll={() => onCategoryChange(undefined)}
          onSelectCategory={onCategoryChange}
        />

        {searchKeyword ? (
          <div className={styles.searchResultLine}>
            {t('shop.searchResult', { keyword: searchKeyword, count: products.length })}
          </div>
        ) : null}

        {loading ? (
          <BrowseLoading />
        ) : (
          <>
            <ProductRows
              products={products}
              categoryNameById={categoryNameById}
              getProductHref={getProductHref}
              actionLabel={actionLabel}
              onProductClick={onProductClick}
            />
            {totalPages > 1 ? (
              <div className={styles.paginationBar}>
                {currentPage > 1 ? (
                  <a
                    className={styles.secondaryButton}
                    href={getPageHref(currentPage - 1)}
                    onClick={(event) => handlePublicShopLinkClick(event, () => onPageChange(currentPage - 1))}
                  >
                    {t('shop.orders.prevPage')}
                  </a>
                ) : (
                  <button type="button" className={styles.secondaryButton} disabled>
                    {t('shop.orders.prevPage')}
                  </button>
                )}
                <span className={styles.paginationInfo}>{t('shop.orders.pageInfo', { current: currentPage, total: totalPages })}</span>
                {currentPage < totalPages ? (
                  <a
                    className={styles.secondaryButton}
                    href={getPageHref(currentPage + 1)}
                    onClick={(event) => handlePublicShopLinkClick(event, () => onPageChange(currentPage + 1))}
                  >
                    {t('shop.orders.nextPage')}
                  </a>
                ) : (
                  <button type="button" className={styles.secondaryButton} disabled>
                    {t('shop.orders.nextPage')}
                  </button>
                )}
              </div>
            ) : null}
          </>
        )}
      </div>

      <PublicShopBrowseRail
        categoriesCount={categories.length}
        productCount={products.length}
        loggedIn={loggedIn}
      />
    </div>
  );
}

export function PublicShopDetailView({
  product,
  loggedIn,
  authReady,
  detailBackLabel,
  detailBackHref,
  detailBackHint,
  shareBusy,
  shareState,
  purchaseError,
  purchaseReturnPath,
  purchaseBusy,
  productAvailableForPurchase,
  purchaseActionLabel,
  purchaseActionIcon,
  onBack,
  onCopyShare,
  onPurchaseLinkClick
}: PublicShopDetailViewProps) {
  const { t } = useTranslation();
  const stockText = useMemo(() => toStockText(t, product), [product, t]);
  const limitText = useMemo(() => toLimitText(t, product), [product, t]);
  const statusText = useMemo(() => toProductStatusText(t, product), [product, t]);
  const purchaseStateDescription = productAvailableForPurchase
    ? loggedIn
      ? t('shop.public.detailRailPurchaseSignedIn')
      : t('shop.public.detailRailPurchaseGuest')
    : t('shop.public.detailRailPurchaseUnavailable');

  return (
    <div className={styles.detailLayout}>
      <article className={styles.detailCard}>
        <div className={styles.detailTopbar}>
          <div className={styles.detailTopbarActions}>
            <a
              className={styles.secondaryButton}
              href={detailBackHref}
              onClick={(event) => handlePublicShopLinkClick(event, onBack)}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{detailBackLabel}</span>
            </a>
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={onCopyShare}
              disabled={shareBusy}
            >
              <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
              <span>{shareBusy ? t('shop.public.shareSubmitting') : t('shop.public.shareAction')}</span>
            </button>
          </div>
          <span className={styles.readOnlyBadge}>{t('shop.public.readOnlyBadge')}</span>
        </div>
        {shareState !== 'idle' ? (
          <p className={styles.shareFeedback} data-state={shareState}>
            {shareState === 'success' ? t('shop.public.shareSuccess') : t('shop.public.shareFailed')}
          </p>
        ) : null}

        <div className={styles.detailHero}>
          <ProductImage product={product} />

          <div className={styles.detailBody}>
            <div className={styles.detailTitleRow}>
              <p className={styles.kicker}>{t('shop.public.guideKicker')}</p>
              <span className={styles.metaChip}>{toProductTypeText(product)}</span>
              {product.voCategoryName?.trim() ? (
                <span className={styles.metaChip}>{product.voCategoryName}</span>
              ) : null}
              <span className={styles.metaChip}>{statusText}</span>
            </div>
            <h2 className={styles.detailTitle}>{product.voName}</h2>
            <p className={styles.detailSummary}>{t('shop.public.detailIntro')}</p>

            <div className={styles.priceBlock}>
              <span className={styles.priceValue}>
                {formatProductPrice(product.voPrice)} {t('shop.currency.carrot')}
              </span>
              {product.voOriginalPrice && product.voOriginalPrice > product.voPrice ? (
                <span className={styles.priceOriginal}>
                  {t('shop.originalPrice', { price: product.voOriginalPrice.toLocaleString() })}
                </span>
              ) : null}
            </div>

            <div className={styles.metaGrid}>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.sold')}</span>
                <span className={styles.metaValue}>{t('shop.soldCount', { count: product.voSoldCount ?? 0 })}</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.stock')}</span>
                <span className={styles.metaValue}>{stockText}</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.limit')}</span>
                <span className={styles.metaValue}>{limitText}</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.duration')}</span>
                <span className={styles.metaValue}>{product.voDurationDisplay || t('shop.public.durationFallback')}</span>
              </div>
            </div>
          </div>
        </div>

        <section className={styles.detailSection}>
          <h2 className={styles.sectionTitle}>{t('shop.section.detail')}</h2>
          {product.voDescription ? (
            <div className={styles.description}>
              {product.voDescription.split('\n').map((line, index) => (
                <p key={index}>{line}</p>
              ))}
            </div>
          ) : (
            <p className={styles.placeholderText}>{t('shop.noDescription')}</p>
          )}
        </section>

        {product.voBenefitValue ? (
          <section className={styles.detailSection}>
            <h2 className={styles.sectionTitle}>{t('shop.section.benefit')}</h2>
            <div className={styles.benefitBox}>{product.voBenefitValue}</div>
          </section>
        ) : null}
      </article>

      <aside className={styles.shopRail} aria-label={t('shop.public.detailRailLabel')}>
        <section className={styles.railPanel}>
          <div className={styles.railPanelHeader}>
            <span className={styles.railIcon}>
              <Icon icon="mdi:arrow-u-left-top" size={18} />
            </span>
            <div>
              <h2 className={styles.railTitle}>{t('shop.public.detailRailSourceTitle')}</h2>
              <p className={styles.railText}>{detailBackHint}</p>
            </div>
          </div>
          <a
            className={`${styles.secondaryButton} ${styles.railAction}`}
            href={detailBackHref}
            onClick={(event) => handlePublicShopLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{detailBackLabel}</span>
          </a>
        </section>

        <section className={styles.railPanel}>
          <div className={styles.railPanelHeader}>
            <span className={styles.railIcon}>
              <Icon icon={loggedIn ? 'mdi:cart-check' : 'mdi:login-variant'} size={18} />
            </span>
            <div>
              <h2 className={styles.railTitle}>{t('shop.public.detailRailPurchaseTitle')}</h2>
              <p className={styles.railText}>{purchaseStateDescription}</p>
            </div>
          </div>
          {purchaseError ? (
            <p className={styles.purchaseFeedback} data-state="error">
              {purchaseError}
            </p>
          ) : null}
          {purchaseReturnPath ? (
            <a
              className={`${styles.primaryLink} ${styles.railAction}`}
              href={purchaseReturnPath}
              aria-disabled={!authReady || purchaseBusy || !productAvailableForPurchase}
              data-disabled={!authReady || purchaseBusy || !productAvailableForPurchase}
              onClick={(event) => onPurchaseLinkClick(event, product)}
            >
              <Icon icon={purchaseActionIcon} size={18} />
              <span>{purchaseActionLabel}</span>
            </a>
          ) : null}
        </section>

        <section className={styles.railPanel}>
          <div className={styles.railPanelHeader}>
            <span className={styles.railIcon}>
              <Icon icon="mdi:package-variant-closed" size={18} />
            </span>
            <div>
              <h2 className={styles.railTitle}>{t('shop.public.detailRailStateTitle')}</h2>
              <p className={styles.railText}>{t('shop.public.detailRailStateDescription')}</p>
            </div>
          </div>
          <div className={styles.railChipRow}>
            <span className={styles.metaChip}>{statusText}</span>
            <span className={styles.metaChip}>{stockText}</span>
            <span className={styles.metaChip}>{t('shop.soldCount', { count: product.voSoldCount ?? 0 })}</span>
            <span className={styles.metaChip}>{product.voDurationDisplay || t('shop.public.durationFallback')}</span>
          </div>
        </section>

        <PublicReadingGuide
          label={t('shop.public.guideKicker')}
          title={t('shop.public.detailGuideTitle')}
          description={t('shop.public.detailGuideDescription')}
          items={toGuideItems(t, publicDetailGuideItems)}
        />

        <section className={styles.railPanel}>
          <div className={styles.railPanelHeader}>
            <span className={styles.railIcon}>
              <Icon icon="mdi:shield-account-outline" size={18} />
            </span>
            <div>
              <h2 className={styles.railTitle}>{t('shop.section.notice')}</h2>
              <p className={styles.railText}>{t('shop.public.noticeReadOnly')}</p>
            </div>
          </div>
          <ul className={styles.railNoticeList}>
            <li>{t('shop.notice.balance')}</li>
            <li>{t('shop.notice.benefit')}</li>
            <li>{t('shop.notice.item')}</li>
            <li>{t('shop.notice.expire')}</li>
          </ul>
        </section>
      </aside>
    </div>
  );
}
