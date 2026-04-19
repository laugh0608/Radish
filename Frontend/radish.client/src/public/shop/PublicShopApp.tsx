import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { ShopHome } from '@/apps/shop/pages/ShopHome';
import { ProductList } from '@/apps/shop/pages/ProductList';
import {
  getCategories,
  getProduct,
  getProducts,
  getProductTypeDisplay,
  StockType,
} from '@/api/shop';
import type { Product, ProductCategory, ProductListItem } from '@/types/shop';
import { resolveMediaUrl } from '@/utils/media';
import type { PublicShopProductsRoute, PublicShopRoute } from '../shopRouteState';
import {
  buildPublicShopPath,
  createDefaultPublicShopProductsRoute,
  createDefaultPublicShopRoute,
} from '../shopRouteState';
import {
  getPublicDetailBackLabelKey,
  type PublicDetailBackMode,
} from '../publicRouteNavigation';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import styles from './PublicShopApp.module.css';

interface PublicShopAppProps {
  route: PublicShopRoute;
  fallbackProductsRoute: PublicShopProductsRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicShopRoute, options?: { replace?: boolean }) => void;
  onNavigateToDiscover?: () => void;
}

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  primaryAction?: {
    label: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    onClick: () => void;
  };
}

interface PublicGuideItemDefinition {
  labelKey: string;
  valueKey: string;
}

interface PublicGuideDefinition {
  titleKey: string;
  descriptionKey: string;
  items: readonly PublicGuideItemDefinition[];
}

function PublicStatusCard({ tone, title, description, primaryAction, secondaryAction }: PublicStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:store-search-outline'
      : tone === 'notFound'
        ? 'mdi:package-variant-closed-remove'
        : 'mdi:alert-circle-outline';

  return (
    <div className={styles.statusCard} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {(primaryAction || secondaryAction) && (
          <div className={styles.statusActions}>
            {primaryAction && (
              <button type="button" className={styles.primaryButton} onClick={primaryAction.onClick}>
                {primaryAction.label}
              </button>
            )}
            {secondaryAction && (
              <button type="button" className={styles.secondaryButton} onClick={secondaryAction.onClick}>
                {secondaryAction.label}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function isProductNotFound(message: string | null): boolean {
  if (!message) {
    return false;
  }

  return /商品不存在|not\s+found|404/i.test(message);
}

function formatProductPrice(value: number): string {
  return value.toLocaleString();
}

function buildProductsRouteKey(route: PublicShopProductsRoute): string {
  return buildPublicShopPath(route);
}

const publicBrowseGuideItems: readonly PublicGuideItemDefinition[] = [
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

const publicDetailGuideItems: readonly PublicGuideItemDefinition[] = [
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

export const PublicShopApp = ({
  route,
  fallbackProductsRoute,
  detailBackAction,
  onNavigate,
  onNavigateToDiscover
}: PublicShopAppProps) => {
  const { t } = useTranslation();
  const pageRef = useRef<HTMLDivElement>(null);
  const categoryRequestIdRef = useRef(0);
  const listRequestIdRef = useRef(0);
  const detailRequestIdRef = useRef(0);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [featuredProducts, setFeaturedProducts] = useState<ProductListItem[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [featuredLoading, setFeaturedLoading] = useState(false);
  const [productsLoading, setProductsLoading] = useState(false);
  const [productLoading, setProductLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [featuredError, setFeaturedError] = useState<string | null>(null);
  const [productsError, setProductsError] = useState<string | null>(null);
  const [productError, setProductError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(() => route.kind === 'products' ? route.page : 1);
  const [totalPages, setTotalPages] = useState(1);
  const [reloadToken, setReloadToken] = useState(0);
  const [categoriesResolved, setCategoriesResolved] = useState(false);

  const pageTitle = route.kind === 'detail'
    ? t('shop.public.detailTitle')
    : route.kind === 'products'
      ? t('shop.public.productsTitle')
      : t('shop.public.homeTitle');
  const guideDefinition = useMemo<PublicGuideDefinition>(() => (
    route.kind === 'detail'
      ? {
          titleKey: 'shop.public.detailGuideTitle',
          descriptionKey: 'shop.public.detailGuideDescription',
          items: publicDetailGuideItems,
        }
      : {
          titleKey: 'shop.public.guideTitle',
          descriptionKey: 'shop.public.guideDescription',
          items: publicBrowseGuideItems,
        }
  ), [route.kind]);

  const listNotice = useMemo(() => {
    if (categoriesError && featuredError && route.kind === 'home') {
      return t('shop.public.partialBothFailed');
    }

    if (route.kind === 'home') {
      if (categoriesError) {
        return t('shop.public.partialCategoriesFailed');
      }

      if (featuredError) {
        return t('shop.public.partialFeaturedFailed');
      }
    }

    if (route.kind === 'products' && categoriesError) {
      return t('shop.public.partialCategoriesFailed');
    }

    return null;
  }, [categoriesError, featuredError, route.kind, t]);

  const productsRouteState = route.kind === 'products' ? route : null;

  const validatedCategoryId = useMemo(() => {
    if (!productsRouteState?.categoryId) {
      return undefined;
    }

    if (!categoriesResolved || categoriesLoading || categoriesError) {
      return productsRouteState.categoryId;
    }

    return categories.some((category) => String(category.voId) === productsRouteState.categoryId)
      ? productsRouteState.categoryId
      : undefined;
  }, [categories, categoriesError, categoriesLoading, categoriesResolved, productsRouteState]);

  const canonicalProductsRoute = useMemo<PublicShopProductsRoute>(() => {
    if (!productsRouteState) {
      return createDefaultPublicShopProductsRoute();
    }

    return {
      kind: 'products',
      categoryId: validatedCategoryId,
      keyword: productsRouteState.keyword,
      page: currentPage
    };
  }, [currentPage, productsRouteState, validatedCategoryId]);

  usePublicReplaceRouteSync({
    currentRouteKey: productsRouteState ? buildProductsRouteKey(productsRouteState) : '__shop-products-noop__',
    nextRoute: canonicalProductsRoute,
    nextRouteKey: productsRouteState ? buildProductsRouteKey(canonicalProductsRoute) : '__shop-products-noop__',
    onRouteStateChange: onNavigate
  });

  useEffect(() => {
    pageRef.current?.scrollTo({ top: 0, behavior: 'auto' });
  }, [route]);

  useEffect(() => {
    document.title = `${pageTitle} · ${t('desktop.apps.shop.name')}`;
  }, [pageTitle, t]);

  useEffect(() => {
    const requestId = ++categoryRequestIdRef.current;

    const loadCategories = async () => {
      setCategoriesLoading(true);
      setCategoriesError(null);
      setCategoriesResolved(false);

      try {
        const result = await getCategories(t);
        if (requestId !== categoryRequestIdRef.current) {
          return;
        }

        if (!result.ok || !result.data) {
          throw new Error(result.message || t('shop.public.loadFailedDescription'));
        }

        setCategories(result.data);
      } catch (error) {
        if (requestId !== categoryRequestIdRef.current) {
          return;
        }

        setCategories([]);
        setCategoriesError(error instanceof Error ? error.message : String(error));
      } finally {
        if (requestId === categoryRequestIdRef.current) {
          setCategoriesLoading(false);
          setCategoriesResolved(true);
        }
      }
    };

    void loadCategories();
  }, [reloadToken, t]);

  useEffect(() => {
    if (route.kind !== 'home') {
      return;
    }

    const requestId = ++listRequestIdRef.current;

    const loadFeaturedProducts = async () => {
      setFeaturedLoading(true);
      setFeaturedError(null);

      try {
        const result = await getProducts(t, undefined, undefined, undefined, 1, 8);
        if (requestId !== listRequestIdRef.current) {
          return;
        }

        if (!result.ok || !result.data) {
          throw new Error(result.message || t('shop.public.loadFailedDescription'));
        }

        setFeaturedProducts(result.data.data || []);
      } catch (error) {
        if (requestId !== listRequestIdRef.current) {
          return;
        }

        setFeaturedProducts([]);
        setFeaturedError(error instanceof Error ? error.message : String(error));
      } finally {
        if (requestId === listRequestIdRef.current) {
          setFeaturedLoading(false);
        }
      }
    };

    void loadFeaturedProducts();
  }, [reloadToken, route.kind, t]);

  useEffect(() => {
    if (!productsRouteState) {
      setCurrentPage(1);
      return;
    }

    setCurrentPage(productsRouteState.page);
  }, [productsRouteState]);

  useEffect(() => {
    if (!productsRouteState) {
      return;
    }

    if (productsRouteState.categoryId && categoriesLoading) {
      return;
    }

    if (productsRouteState.categoryId && !categoriesError && validatedCategoryId !== productsRouteState.categoryId) {
      return;
    }

    const requestId = ++listRequestIdRef.current;

    const loadProductList = async () => {
      setProductsLoading(true);
      setProductsError(null);

      try {
        const result = await getProducts(t, validatedCategoryId, undefined, productsRouteState.keyword, currentPage, 20);
        if (requestId !== listRequestIdRef.current) {
          return;
        }

        if (!result.ok || !result.data) {
          throw new Error(result.message || t('shop.public.loadFailedDescription'));
        }

        const nextTotalPages = Math.max(result.data.pageCount || 1, 1);
        if (currentPage > nextTotalPages) {
          setCurrentPage(nextTotalPages);
          return;
        }

        setProducts(result.data.data || []);
        setCurrentPage(result.data.page || 1);
        setTotalPages(nextTotalPages);
      } catch (error) {
        if (requestId !== listRequestIdRef.current) {
          return;
        }

        setProducts([]);
        setCurrentPage(1);
        setTotalPages(1);
        setProductsError(error instanceof Error ? error.message : String(error));
      } finally {
        if (requestId === listRequestIdRef.current) {
          setProductsLoading(false);
        }
      }
    };

    void loadProductList();
  }, [categoriesError, categoriesLoading, currentPage, productsRouteState, reloadToken, t, validatedCategoryId]);

  useEffect(() => {
    if (route.kind !== 'detail') {
      return;
    }

    const requestId = ++detailRequestIdRef.current;

    const loadProductDetail = async () => {
      setProductLoading(true);
      setProductError(null);
      setSelectedProduct(null);

      try {
        const result = await getProduct(route.productId, t);
        if (requestId !== detailRequestIdRef.current) {
          return;
        }

        if (!result.ok || !result.data) {
          throw new Error(result.message || t('shop.public.loadFailedDescription'));
        }

        setSelectedProduct(result.data);
      } catch (error) {
        if (requestId !== detailRequestIdRef.current) {
          return;
        }

        setSelectedProduct(null);
        setProductError(error instanceof Error ? error.message : String(error));
      } finally {
        if (requestId === detailRequestIdRef.current) {
          setProductLoading(false);
        }
      }
    };

    void loadProductDetail();
  }, [reloadToken, route, t]);

  const handleOpenProducts = (categoryId?: string) => {
    onNavigate({
      kind: 'products',
      categoryId,
      page: 1
    });
  };

  const handleOpenProductDetail = (productId: number | string) => {
    onNavigate({
      kind: 'detail',
      productId: String(productId)
    });
  };

  const handleBackFromDetail = () => {
    if (detailBackAction) {
      detailBackAction.onBack();
      return;
    }

    onNavigate(fallbackProductsRoute);
  };

  const detailBackLabelKey = getPublicDetailBackLabelKey(detailBackAction?.mode);
  const detailBackLabel = detailBackLabelKey ? t(detailBackLabelKey) : t('shop.public.backToProducts');
  const detailBackHint = detailBackAction?.mode === 'discover'
    ? t('shop.public.detailBackHintDiscover')
    : detailBackAction
      ? t('shop.public.detailBackHintSpecific', { target: detailBackLabel })
      : t('shop.public.detailBackHintDefault');

  const renderHome = () => {
    if (categoriesError && featuredError && categories.length === 0 && featuredProducts.length === 0 && !categoriesLoading && !featuredLoading) {
      return (
        <PublicStatusCard
          tone="error"
          title={t('shop.public.loadFailedTitle')}
          description={t('shop.public.loadFailedDescription')}
          primaryAction={{
            label: t('common.retry'),
            onClick: () => setReloadToken((current) => current + 1)
          }}
        />
      );
    }

    return (
      <>
        {listNotice && (
          <div className={styles.inlineNotice}>
            <span className={styles.inlineNoticeText}>{listNotice}</span>
            <button
              type="button"
              className={styles.inlineTextButton}
              onClick={() => setReloadToken((current) => current + 1)}
            >
              {t('common.retry')}
            </button>
          </div>
        )}

        <ShopHome
          categories={categories}
          featuredProducts={featuredProducts}
          loading={categoriesLoading || featuredLoading}
          onCategoryClick={handleOpenProducts}
          onProductClick={handleOpenProductDetail}
          onViewAllProducts={() => handleOpenProducts()}
        />
      </>
    );
  };

  const renderProducts = () => {
    if (route.kind !== 'products') {
      return null;
    }

    if (productsError && products.length === 0 && !productsLoading) {
      return (
        <PublicStatusCard
          tone="error"
          title={t('shop.public.loadFailedTitle')}
          description={productsError}
          primaryAction={{
            label: t('common.retry'),
            onClick: () => setReloadToken((current) => current + 1)
          }}
          secondaryAction={{
            label: t('shop.public.backToHome'),
            onClick: () => onNavigate(createDefaultPublicShopRoute())
          }}
        />
      );
    }

    return (
      <>
        {listNotice && (
          <div className={styles.inlineNotice}>
            <span className={styles.inlineNoticeText}>{listNotice}</span>
            <button
              type="button"
              className={styles.inlineTextButton}
              onClick={() => setReloadToken((current) => current + 1)}
            >
              {t('common.retry')}
            </button>
          </div>
        )}

        <ProductList
          categories={categories}
          products={products}
          selectedCategoryId={route.categoryId}
          currentPage={currentPage}
          totalPages={totalPages}
          searchKeyword={route.keyword}
          loading={productsLoading}
          onCategoryChange={(categoryId) => {
            onNavigate({
              kind: 'products',
              categoryId,
              keyword: route.keyword,
              page: 1
            });
          }}
          onProductClick={handleOpenProductDetail}
          onSearchChange={(keyword) => {
            onNavigate({
              kind: 'products',
              categoryId: route.categoryId,
              keyword: keyword || undefined,
              page: 1
            });
          }}
          onPageChange={(page) => {
            onNavigate({
              kind: 'products',
              categoryId: route.categoryId,
              keyword: route.keyword,
              page
            });
          }}
          onBack={() => onNavigate(createDefaultPublicShopRoute())}
        />
      </>
    );
  };

  const renderDetail = () => {
    if (productLoading) {
      return (
        <PublicStatusCard
          tone="loading"
          title={t('shop.public.detailLoadingTitle')}
          description={t('shop.public.detailLoadingDescription')}
        />
      );
    }

    if (productError && !selectedProduct) {
      return (
        <PublicStatusCard
          tone={isProductNotFound(productError) ? 'notFound' : 'error'}
          title={isProductNotFound(productError) ? t('shop.public.notFoundTitle') : t('shop.public.loadFailedTitle')}
          description={isProductNotFound(productError) ? t('shop.public.notFoundDescription') : productError}
          primaryAction={isProductNotFound(productError)
            ? undefined
            : {
                label: t('common.retry'),
                onClick: () => setReloadToken((current) => current + 1)
              }}
          secondaryAction={{
            label: detailBackLabel,
            onClick: handleBackFromDetail
          }}
        />
      );
    }

    if (!selectedProduct) {
      return (
        <PublicStatusCard
          tone="empty"
          title={t('shop.public.notFoundTitle')}
          description={t('shop.public.notFoundDescription')}
          secondaryAction={{
            label: detailBackLabel,
            onClick: handleBackFromDetail
          }}
        />
      );
    }

    const coverImageUrl = resolveMediaUrl(selectedProduct.voCoverImage);
    const iconImageUrl = resolveMediaUrl(selectedProduct.voIcon);
    const stockText = selectedProduct.voStockType === StockType.Unlimited
      ? t('shop.stock.unlimited')
      : t('shop.productCount', { count: selectedProduct.voStock ?? 0 });
    const limitText = (selectedProduct.voLimitPerUser ?? 0) > 0
      ? t('shop.limit.perUser', { count: selectedProduct.voLimitPerUser })
      : t('shop.limit.unlimited');

    return (
      <article className={styles.detailCard}>
        <div className={styles.detailTopbar}>
          <button type="button" className={styles.secondaryButton} onClick={handleBackFromDetail}>
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{detailBackLabel}</span>
          </button>
          <span className={styles.readOnlyBadge}>{t('shop.public.readOnlyBadge')}</span>
        </div>
        <p className={styles.detailBackHint}>{detailBackHint}</p>

        <div className={styles.detailHero}>
          <div className={styles.detailImagePanel}>
            {coverImageUrl ? (
              <img className={styles.detailImage} src={coverImageUrl} alt={selectedProduct.voName} />
            ) : iconImageUrl ? (
              <img className={styles.detailImage} src={iconImageUrl} alt={selectedProduct.voName} />
            ) : (
              <div className={styles.detailImageFallback}>
                <Icon icon="mdi:gift-outline" size={36} />
              </div>
            )}
          </div>

          <div className={styles.detailBody}>
            <div className={styles.detailTitleRow}>
              <p className={styles.kicker}>Phase 2-2</p>
              <span className={styles.metaChip}>{getProductTypeDisplay(selectedProduct.voProductType)}</span>
              {selectedProduct.voCategoryName?.trim() && (
                <span className={styles.metaChip}>{selectedProduct.voCategoryName}</span>
              )}
            </div>
            <h1 className={styles.detailTitle}>{selectedProduct.voName}</h1>
            <p className={styles.detailSummary}>{t('shop.public.detailIntro')}</p>

            <div className={styles.priceBlock}>
              <span className={styles.priceValue}>
                {formatProductPrice(selectedProduct.voPrice)} {t('shop.currency.carrot')}
              </span>
              {selectedProduct.voOriginalPrice && selectedProduct.voOriginalPrice > selectedProduct.voPrice && (
                <span className={styles.priceOriginal}>
                  {t('shop.originalPrice', { price: selectedProduct.voOriginalPrice.toLocaleString() })}
                </span>
              )}
            </div>

            <div className={styles.metaGrid}>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>{t('shop.meta.sold')}</span>
                <span className={styles.metaValue}>{t('shop.soldCount', { count: selectedProduct.voSoldCount ?? 0 })}</span>
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
                <span className={styles.metaValue}>{selectedProduct.voDurationDisplay || t('shop.public.durationFallback')}</span>
              </div>
            </div>

            <div className={styles.readOnlyPanel}>
              <h2 className={styles.readOnlyTitle}>{t('shop.public.purchaseTitle')}</h2>
              <p className={styles.readOnlyDescription}>{t('shop.public.purchaseDescription')}</p>
              <a className={styles.primaryLink} href="/">
                <Icon icon="mdi:view-dashboard-outline" size={18} />
                <span>{t('shop.public.openDesktop')}</span>
              </a>
            </div>
          </div>
        </div>

        <section className={styles.detailSection}>
          <h2 className={styles.sectionTitle}>{t('shop.section.detail')}</h2>
          {selectedProduct.voDescription ? (
            <div className={styles.description}>
              {selectedProduct.voDescription.split('\n').map((line, index) => (
                <p key={index}>{line}</p>
              ))}
            </div>
          ) : (
            <p className={styles.placeholderText}>{t('shop.noDescription')}</p>
          )}
        </section>

        {selectedProduct.voBenefitValue && (
          <section className={styles.detailSection}>
            <h2 className={styles.sectionTitle}>{t('shop.section.benefit')}</h2>
            <div className={styles.benefitBox}>{selectedProduct.voBenefitValue}</div>
          </section>
        )}

        <section className={styles.detailSection}>
          <h2 className={styles.sectionTitle}>{t('shop.section.notice')}</h2>
          <ul className={styles.noticeList}>
            <li>{t('shop.public.noticeReadOnly')}</li>
            <li>{t('shop.notice.balance')}</li>
            <li>{t('shop.notice.benefit')}</li>
            <li>{t('shop.notice.item')}</li>
            <li>{t('shop.notice.expire')}</li>
          </ul>
        </section>
      </article>
    );
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="商"
        brandName={t('desktop.apps.shop.name')}
        brandSubline={t('shop.public.shellLabel')}
        onBrandClick={() => onNavigate(createDefaultPublicShopRoute())}
        onNavigateToDiscover={onNavigateToDiscover}
        discoverLabel={t('public.shell.discoverAction')}
      />

      <main className={styles.main}>
        <section className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionHeading}>
              <div className={styles.sectionTitleRow}>
                <p className={styles.kicker}>Phase 2-2</p>
                <span className={styles.readOnlyBadge}>{t('shop.public.readOnlyBadge')}</span>
              </div>
              <h1 className={styles.pageTitle}>{pageTitle}</h1>
              <p className={styles.pageIntro}>{t('shop.public.pageIntro')}</p>
            </div>
            <div className={styles.sectionActions}>
              <button
                type="button"
                className={styles.ghostButton}
                onClick={() => onNavigate(createDefaultPublicShopProductsRoute())}
              >
                {t('shop.public.browseProducts')}
              </button>
            </div>
          </div>

          <div className={styles.guideSection}>
            <PublicReadingGuide
              label={t('shop.public.guideKicker')}
              title={t(guideDefinition.titleKey)}
              description={t(guideDefinition.descriptionKey)}
              items={guideDefinition.items.map((item) => ({
                label: t(item.labelKey),
                value: t(item.valueKey),
              }))}
            />
          </div>

          <div className={styles.contentWrap}>
            {route.kind === 'home' ? renderHome() : route.kind === 'products' ? renderProducts() : renderDetail()}
          </div>
        </section>
      </main>
    </div>
  );
};
