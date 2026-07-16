import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { createApiResponseError, isApiResponseNotFoundError } from '@radish/http';
import { PurchaseModal } from '@/apps/shop/components/PurchaseModal';
import {
  checkCanBuy,
  getCategories,
  getProduct,
  getProducts,
  purchaseProduct,
} from '@/api/shop';
import type { LongId } from '@/api/user';
import type { Product, ProductCategory, ProductListItem } from '@/types/shop';
import { resolveMediaUrl } from '@/utils/media';
import { getApiBaseUrl } from '@/config/env';
import { redirectToLogin } from '@/services/auth';
import { hydrateAuthUser } from '@/services/authBootstrap';
import { buildShopOrderReturnPath, buildShopOrdersReturnPath, buildShopProductPurchaseReturnPath } from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import { isPaymentPasscodeUpgradeRequiredError } from '@/utils/paymentPasscode';
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
import { buildLocalizedPublicRouteHead, buildPublicShareUrl } from '../publicHead';
import { buildShopProductStructuredData } from '../publicStructuredData';
import { usePublicHeadSnapshot } from '../publicHeadLifecycleContext';
import { isCurrentShopProductHeadSource } from '../publicHeadSourceIdentity';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { PublicShopDetailView, PublicShopHomeView, PublicShopProductsView } from './PublicShopViews';
import styles from './PublicShopApp.module.css';

interface PublicShopAppProps {
  route: PublicShopRoute;
  fallbackProductsRoute: PublicShopProductsRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    href?: string;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicShopRoute, options?: { replace?: boolean }) => void;
}

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound';

interface PublicStatusCardAction {
  label: string;
  href?: string;
  onClick: () => void;
}

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  primaryAction?: PublicStatusCardAction;
  secondaryAction?: PublicStatusCardAction;
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

function PublicStatusCard({ tone, title, description, primaryAction, secondaryAction }: PublicStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:store-search-outline'
      : tone === 'notFound'
        ? 'mdi:package-variant-closed-remove'
        : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      kind: 'primary',
      onClick: primaryAction.href
        ? (event) => handlePublicShopLinkClick(event as MouseEvent<HTMLAnchorElement>, primaryAction.onClick)
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => handlePublicShopLinkClick(event as MouseEvent<HTMLAnchorElement>, secondaryAction.onClick)
        : () => secondaryAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      actions={actions}
    />
  );
}

function buildProductsRouteKey(route: PublicShopProductsRoute): string {
  return buildPublicShopPath(route);
}

function isProductAvailableForPurchase(product: Product): boolean {
  return product.voInStock === true && product.voIsOnSale === true;
}

export const PublicShopApp = ({
  route,
  fallbackProductsRoute,
  detailBackAction,
  onNavigate
}: PublicShopAppProps) => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
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
  const [productNotFound, setProductNotFound] = useState(false);
  const [currentPage, setCurrentPage] = useState(() => route.kind === 'products' ? route.page : 1);
  const [totalPages, setTotalPages] = useState(1);
  const [reloadToken, setReloadToken] = useState(0);
  const [categoriesResolved, setCategoriesResolved] = useState(false);
  const [authReady, setAuthReady] = useState(false);
  const [purchaseError, setPurchaseError] = useState<string | null>(null);
  const [checkingPurchaseProductId, setCheckingPurchaseProductId] = useState<LongId | null>(null);
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);
  const [purchasing, setPurchasing] = useState(false);
  const [purchasePasscodeUpgradePrompt, setPurchasePasscodeUpgradePrompt] = useState<string | null>(null);
  const [handledPurchaseIntentKey, setHandledPurchaseIntentKey] = useState<string | null>(null);

  const pageTitle = route.kind === 'detail'
    ? t('shop.public.detailTitle')
    : route.kind === 'products'
      ? t('shop.public.productsTitle')
      : t('shop.public.homeTitle');

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
  const visibleCategories = useMemo(
    () => categories.filter((category) => (category.voProductCount ?? 0) > 0),
    [categories]
  );

  const validatedCategoryId = useMemo(() => {
    if (!productsRouteState?.categoryId) {
      return undefined;
    }

    if (!categoriesResolved || categoriesLoading || categoriesError) {
      return productsRouteState.categoryId;
    }

    return visibleCategories.some((category) => String(category.voId) === productsRouteState.categoryId)
      ? productsRouteState.categoryId
      : undefined;
  }, [categoriesError, categoriesLoading, categoriesResolved, productsRouteState, visibleCategories]);

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
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('PublicShopApp', '公开商城登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    pageRef.current?.scrollTo({ top: 0, behavior: 'auto' });
  }, [route]);

  const publicHeadSnapshot = useMemo(() => {
    if (route.kind === 'products' && route.categoryId) {
      const selectedCategory = categories.find(
        (category) => String(category.voId) === route.categoryId,
      );
      if (!selectedCategory) {
        return null;
      }

      const routeHead = buildLocalizedPublicRouteHead({ app: 'shop', route }, t);
      return {
        head: {
          ...routeHead,
          title: `${selectedCategory.voName} · ${t('desktop.apps.shop.name')}`,
          description: selectedCategory.voDescription?.trim() || routeHead.description,
        },
      };
    }

    if (
      route.kind !== 'detail'
      || !selectedProduct
      || !isCurrentShopProductHeadSource(route, selectedProduct.voId)
    ) {
      return null;
    }

    const canonicalRoute: PublicShopRoute = {
      kind: 'detail',
      productId: String(selectedProduct.voId),
    };
    const canonicalPath = buildPublicShopPath(canonicalRoute);
    const imageUrl = resolveMediaUrl(selectedProduct.voCoverImage || selectedProduct.voIcon);
    const routeHead = buildLocalizedPublicRouteHead({ app: 'shop', route: canonicalRoute }, t);
    const head = {
      ...routeHead,
      title: `${pageTitle} · ${t('desktop.apps.shop.name')}`,
      description: selectedProduct.voDescription?.trim() || routeHead.description,
      imageUrl: imageUrl || undefined,
    };

    return {
      head,
      structuredData: buildShopProductStructuredData({
        product: selectedProduct,
        imageUrl,
        canonicalPath,
      }),
    };
  }, [categories, pageTitle, route, selectedProduct, t]);
  usePublicHeadSnapshot(publicHeadSnapshot);

  const buildShopShareUrl = useCallback(() => {
    const productId = route.kind === 'detail' ? route.productId : String(selectedProduct?.voId ?? '');
    return buildPublicShareUrl(buildPublicShopPath({ kind: 'detail', productId }));
  }, [route, selectedProduct?.voId]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildShopShareUrl,
  });

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
      setProductNotFound(false);
      setSelectedProduct(null);

      try {
        const result = await getProduct(route.productId, t);
        if (requestId !== detailRequestIdRef.current) {
          return;
        }

        if (!result.ok || !result.data) {
          throw createApiResponseError(result, t('shop.public.loadFailedDescription'));
        }

        setSelectedProduct(result.data);
      } catch (error) {
        if (requestId !== detailRequestIdRef.current) {
          return;
        }

        setSelectedProduct(null);
        setProductNotFound(isApiResponseNotFoundError(error));
        setProductError(error instanceof Error ? error.message : String(error));
      } finally {
        if (requestId === detailRequestIdRef.current) {
          setProductLoading(false);
        }
      }
    };

    void loadProductDetail();
  }, [reloadToken, route, t]);

  useEffect(() => {
    setPurchaseError(null);
    setPurchasePasscodeUpgradePrompt(null);
    setIsPurchaseModalOpen(false);
  }, [route]);

  const handleRequestPurchase = useCallback(async (product: Product) => {
    const returnPath = buildShopProductPurchaseReturnPath(product.voId);
    if (!returnPath) {
      setPurchaseError(t('shop.public.purchaseUnavailable'));
      return;
    }

    if (!authReady) {
      setPurchaseError(t('shop.public.purchaseAuthChecking'));
      return;
    }

    if (!loggedIn) {
      redirectToLogin({ returnPath });
      return;
    }

    if (!isProductAvailableForPurchase(product)) {
      setPurchaseError(product.voInStock === false ? t('shop.outOfStock') : t('shop.unavailable'));
      return;
    }

    setCheckingPurchaseProductId(product.voId);
    setPurchaseError(null);
    setPurchasePasscodeUpgradePrompt(null);

    try {
      const result = await checkCanBuy(product.voId, 1, t);
      if (!result.ok || !result.data) {
        throw new Error(result.message || t('shop.public.purchaseCheckFailed'));
      }

      if (!result.data.canBuy) {
        setPurchaseError(result.data.reason?.trim() || t('shop.unavailable'));
        return;
      }

      setIsPurchaseModalOpen(true);
    } catch (error) {
      log.error('PublicShopApp', '公开商城购买检查失败', error);
      setPurchaseError(error instanceof Error ? error.message : t('shop.public.purchaseCheckFailed'));
    } finally {
      setCheckingPurchaseProductId(null);
    }
  }, [authReady, loggedIn, t]);

  const handleConfirmPurchase = useCallback(async (
    productId: LongId,
    quantity: number = 1,
    paymentPassword: string,
    idempotencyKey: string
  ) => {
    const returnPath = buildShopProductPurchaseReturnPath(productId);
    if (!loggedIn) {
      redirectToLogin({ returnPath });
      return;
    }

    setPurchasing(true);
    setPurchaseError(null);
    try {
      const result = await purchaseProduct({
        productId,
        quantity,
        paymentPassword,
        idempotencyKey
      }, t);

      if (result.ok && result.data?.success) {
        setPurchasePasscodeUpgradePrompt(null);
        setIsPurchaseModalOpen(false);
        window.location.href = result.data.orderId
          ? buildShopOrderReturnPath(result.data.orderId) ?? buildShopOrdersReturnPath()
          : buildShopOrdersReturnPath();
        return;
      }

      const errorMessage = result.data?.errorMessage || result.message || t('shop.error.purchaseFailed');
      const requiresPasscodeUpgrade = Boolean(result.data?.requiresPasscodeUpgrade)
        || isPaymentPasscodeUpgradeRequiredError({
          code: result.data?.errorCode
        });

      if (requiresPasscodeUpgrade) {
        setPurchasePasscodeUpgradePrompt(errorMessage);
        return;
      }

      throw new Error(errorMessage);
    } catch (error) {
      log.error('PublicShopApp', '公开商城购买失败', error);
      setPurchaseError(error instanceof Error ? error.message : t('shop.error.purchaseFailed'));
    } finally {
      setPurchasing(false);
    }
  }, [loggedIn, t]);

  useEffect(() => {
    if (route.kind !== 'detail' || route.intent !== 'purchase' || !selectedProduct || productLoading || !authReady) {
      return;
    }

    const intentKey = `purchase:${selectedProduct.voId}`;
    if (handledPurchaseIntentKey === intentKey || isPurchaseModalOpen || checkingPurchaseProductId !== null || purchasing) {
      return;
    }

    setHandledPurchaseIntentKey(intentKey);
    void handleRequestPurchase(selectedProduct);
  }, [
    authReady,
    checkingPurchaseProductId,
    handleRequestPurchase,
    handledPurchaseIntentKey,
    isPurchaseModalOpen,
    productLoading,
    purchasing,
    route,
    selectedProduct
  ]);

  const handleClosePurchaseModal = useCallback(() => {
    setPurchasePasscodeUpgradePrompt(null);
    setIsPurchaseModalOpen(false);
  }, []);

  const handlePurchaseLinkClick = useCallback((event: MouseEvent<HTMLAnchorElement>, product: Product) => {
    if (!shouldHandlePublicShopLink(event)) {
      return;
    }

    event.preventDefault();
    void handleRequestPurchase(product);
  }, [handleRequestPurchase]);

  const handleOpenProducts = (categoryId?: string) => {
    onNavigate({
      kind: 'products',
      categoryId,
      page: 1
    });
  };

  const handleOpenProductDetail = (productId: string) => {
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
  const detailBackHref = detailBackAction?.href ?? buildPublicShopPath(fallbackProductsRoute);
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

        <PublicShopHomeView
          categories={categories}
          featuredProducts={featuredProducts}
          loading={categoriesLoading || featuredLoading}
          loggedIn={loggedIn}
          getCategoryHref={(categoryId) => buildPublicShopPath({ kind: 'products', categoryId, page: 1 })}
          getProductHref={(productId) => buildPublicShopPath({ kind: 'detail', productId: String(productId) })}
          viewAllProductsHref={buildPublicShopPath(createDefaultPublicShopProductsRoute())}
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
            href: buildPublicShopPath(createDefaultPublicShopRoute()),
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

        <PublicShopProductsView
          categories={visibleCategories}
          products={products}
          selectedCategoryId={validatedCategoryId}
          currentPage={currentPage}
          totalPages={totalPages}
          searchKeyword={route.keyword}
          loading={productsLoading}
          loggedIn={loggedIn}
          backHref={buildPublicShopPath(createDefaultPublicShopRoute())}
          getCategoryHref={(categoryId) => buildPublicShopPath({
            kind: 'products',
            categoryId,
            keyword: route.keyword,
            page: 1
          })}
          getProductHref={(productId) => buildPublicShopPath({ kind: 'detail', productId: String(productId) })}
          getPageHref={(page) => buildPublicShopPath({
            kind: 'products',
            categoryId: validatedCategoryId,
            keyword: route.keyword,
            page
          })}
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
              categoryId: validatedCategoryId,
              keyword: keyword || undefined,
              page: 1
            });
          }}
          onPageChange={(page) => {
            onNavigate({
              kind: 'products',
              categoryId: validatedCategoryId,
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
    if (route.kind !== 'detail') {
      return null;
    }

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
          tone={productNotFound ? 'notFound' : 'error'}
          title={productNotFound ? t('shop.public.notFoundTitle') : t('shop.public.loadFailedTitle')}
          description={productNotFound ? t('shop.public.notFoundDescription') : productError}
          primaryAction={productNotFound
            ? undefined
            : {
                label: t('common.retry'),
                onClick: () => setReloadToken((current) => current + 1)
              }}
          secondaryAction={{
            label: detailBackLabel,
            href: detailBackHref,
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
            href: detailBackHref,
            onClick: handleBackFromDetail
          }}
        />
      );
    }

    const purchaseReturnPath = buildShopProductPurchaseReturnPath(selectedProduct.voId);
    const purchaseBusy = checkingPurchaseProductId === selectedProduct.voId || purchasing;
    const productAvailableForPurchase = isProductAvailableForPurchase(selectedProduct);
    const purchaseActionLabel = !authReady
      ? t('shop.public.purchaseAuthChecking')
      : productAvailableForPurchase
        ? purchaseBusy
          ? t('shop.checkingAvailability')
          : loggedIn
            ? t('shop.buyNow')
            : t('shop.loginAndContinuePurchase')
        : selectedProduct.voInStock === false
          ? t('shop.outOfStock')
          : t('shop.unavailable');
    const purchaseActionIcon = !authReady || purchaseBusy
      ? 'mdi:progress-clock'
      : loggedIn
        ? 'mdi:cart-arrow-right'
        : 'mdi:login-variant';

    return (
      <PublicShopDetailView
        product={selectedProduct}
        loggedIn={loggedIn}
        authReady={authReady}
        detailBackLabel={detailBackLabel}
        detailBackHref={detailBackHref}
        detailBackHint={detailBackHint}
        shareBusy={shareBusy}
        shareState={shareState}
        purchaseError={purchaseError}
        purchaseReturnPath={purchaseReturnPath}
        purchaseBusy={purchaseBusy}
        productAvailableForPurchase={productAvailableForPurchase}
        purchaseActionLabel={purchaseActionLabel}
        purchaseActionIcon={purchaseActionIcon}
        onBack={handleBackFromDetail}
        onCopyShare={() => void copyShareLink()}
        onPurchaseLinkClick={handlePurchaseLinkClick}
      />
    );
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="商"
        brandName={t('desktop.apps.shop.name')}
        brandSubline={t('shop.public.shellLabel')}
        onBrandClick={() => onNavigate(createDefaultPublicShopRoute())}
        loginLabel={t('public.shell.loginAction')}
      />

      <main className={styles.main}>
        <section className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionHeading}>
              <div className={styles.sectionTitleRow}>
                <p className={styles.kicker}>{t('shop.public.guideKicker')}</p>
                <span className={styles.readOnlyBadge}>{t('shop.public.readOnlyBadge')}</span>
              </div>
              <h1 className={styles.pageTitle}>{pageTitle}</h1>
              <p className={styles.pageIntro}>{t('shop.public.pageIntro')}</p>
            </div>
            <div className={styles.sectionActions}>
              <a
                className={styles.ghostButton}
                href={buildPublicShopPath(createDefaultPublicShopProductsRoute())}
                onClick={(event) => handlePublicShopLinkClick(event, () => onNavigate(createDefaultPublicShopProductsRoute()))}
              >
                {t('shop.public.browseProducts')}
              </a>
            </div>
          </div>

          <div className={styles.contentWrap}>
            {route.kind === 'home' ? renderHome() : route.kind === 'products' ? renderProducts() : renderDetail()}
          </div>
        </section>
      </main>

      {purchaseError && isPurchaseModalOpen && (
        <div className={styles.purchaseToast} role="alert">
          {purchaseError}
        </div>
      )}

      {isPurchaseModalOpen && (
        <PurchaseModal
          isOpen={isPurchaseModalOpen}
          product={selectedProduct}
          loading={purchasing}
          passcodeUpgradePrompt={purchasePasscodeUpgradePrompt}
          onClose={handleClosePurchaseModal}
          onConfirm={handleConfirmPurchase}
        />
      )}
    </div>
  );
};
