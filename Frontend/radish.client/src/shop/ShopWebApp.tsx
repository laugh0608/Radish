import { lazy, Suspense, useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { LongId } from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { useShopActions } from '@/apps/shop/hooks/useShopActions';
import { useShopData, type ShopLoadError } from '@/apps/shop/hooks/useShopData';
import type { ShopAppState } from '@/apps/shop/ShopApp';
import { buildPublicShopPath } from '@/public/shopRouteState';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import {
  buildShopInventoryReturnPath,
  buildShopOrderReturnPath,
  buildShopOrdersReturnPath,
} from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import { copyRecoveryDiagnostics } from '@/utils/recoveryDiagnostics';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildShopPath, createDefaultShopRoute, parseShopRoute, type ShopRoute } from './shopRouteState';
import styles from '@/apps/shop/ShopApp.module.css';

const OrderList = lazy(() => import('@/apps/shop/pages/OrderList').then((module) => ({ default: module.OrderList })));
const OrderDetail = lazy(() => import('@/apps/shop/pages/OrderDetail').then((module) => ({ default: module.OrderDetail })));
const Inventory = lazy(() => import('@/apps/shop/pages/Inventory').then((module) => ({ default: module.Inventory })));

function resolveInitialShopRoute(): ShopRoute {
  if (typeof window === 'undefined') {
    return createDefaultShopRoute();
  }

  return parseShopRoute(window.location.pathname) ?? createDefaultShopRoute();
}

function shouldHandleShopWebLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function buildShopRouteReturnPath(route: ShopRoute): string {
  if (route.kind === 'inventory') {
    return buildShopInventoryReturnPath();
  }

  if (route.kind === 'order-detail') {
    return buildShopOrderReturnPath(route.orderId) ?? buildShopOrdersReturnPath();
  }

  return buildShopOrdersReturnPath();
}

function buildAppState(route: ShopRoute): ShopAppState {
  if (route.kind === 'inventory') {
    return {
      currentView: 'inventory'
    };
  }

  if (route.kind === 'order-detail') {
    return {
      currentView: 'order-detail',
      selectedOrderId: route.orderId
    };
  }

  return {
    currentView: 'orders'
  };
}

function renderShopLoadingFallback() {
  return (
    <div className={styles.lazyLoading}>
      <div className={styles.lazySpinner}></div>
    </div>
  );
}

export function ShopWebApp() {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [route, setRoute] = useState<ShopRoute>(() => resolveInitialShopRoute());
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [diagnosticCopyState, setDiagnosticCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');
  const dataState = useShopData(t);
  const {
    setError,
    loadProducts,
    loadProductDetail,
    checkCanBuy,
    loadOrders,
    loadOrderDetail,
    loadInventory,
    searchProducts,
    selectedProduct,
  } = dataState;
  const appState = useMemo(() => buildAppState(route), [route]);
  const publicShopHref = buildPublicShopPath({ kind: 'home' });
  const ordersHref = buildShopPath({ kind: 'orders' });
  const inventoryHref = buildShopPath({ kind: 'inventory' });
  const diagnosticActionLabel = t(diagnosticCopyState === 'copied'
    ? 'common.diagnosticsCopied'
    : diagnosticCopyState === 'failed'
      ? 'common.diagnosticsCopyFailed'
      : 'common.copyDiagnostics');

  const navigateToRoute = useCallback((nextRoute: ShopRoute) => {
    const nextPath = buildShopPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== nextPath) {
      window.history.pushState(window.history.state, '', nextPath);
    }
    setRoute(nextRoute);
  }, []);

  const navigate = useMemo(() => ({
    toPublicShop: () => {
      window.location.href = publicShopHref;
    },
    toOrders: () => navigateToRoute({ kind: 'orders' }),
    toOrderDetail: (orderId: LongId) => navigateToRoute({ kind: 'order-detail', orderId }),
    toInventory: () => navigateToRoute({ kind: 'inventory' }),
    toProductDetail: (productId: LongId) => {
      window.location.href = buildPublicShopPath({ kind: 'detail', productId: String(productId) });
    },
    back: () => {
      if (route.kind === 'order-detail') {
        navigateToRoute({ kind: 'orders' });
        return;
      }

      window.location.href = publicShopHref;
    }
  }), [navigateToRoute, publicShopHref, route.kind]);

  const actionsState = useShopActions({
    t,
    isAuthenticated: loggedIn,
    appState,
    setError,
    loadProducts,
    loadProductDetail,
    checkCanBuy,
    loadOrders,
    loadOrderDetail,
    loadInventory,
    searchProducts,
    selectedProduct,
  });

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('ShopWebApp', '商城交易页登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    const handlePopState = () => {
      setRoute(resolveInitialShopRoute());
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  useEffect(() => {
    const canonicalPath = buildShopPath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  useEffect(() => {
    document.title = `${t('shop.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildShopRouteReturnPath(route)
    });
  }, [authReady, loggedIn, redirecting, route]);

  useEffect(() => {
    if (!authReady || !loggedIn) {
      return;
    }

    if (route.kind === 'orders') {
      void loadOrders();
      return;
    }

    if (route.kind === 'order-detail') {
      void loadOrderDetail(route.orderId);
      return;
    }

    void loadInventory();
  }, [authReady, loadInventory, loadOrderDetail, loadOrders, loggedIn, route]);

  useEffect(() => {
    setDiagnosticCopyState('idle');
  }, [route.kind, dataState.loadError?.scope, dataState.loadError?.message]);

  const handleOrdersLinkClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (!shouldHandleShopWebLinkClick(event)) {
      return;
    }

    event.preventDefault();
    navigate.toOrders();
  };

  const handleInventoryLinkClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (!shouldHandleShopWebLinkClick(event)) {
      return;
    }

    event.preventDefault();
    navigate.toInventory();
  };

  const handleCopyShopDiagnostics = useCallback(async (error: ShopLoadError) => {
    try {
      await copyRecoveryDiagnostics({
        module: 'shop.private',
        stage: error.scope,
        error: error.message,
        target: {
          routeKind: route.kind,
          ...error.target,
        },
      });
      setDiagnosticCopyState('copied');
    } catch (copyError) {
      log.warn('ShopWebApp', '复制商城恢复诊断失败', copyError);
      setDiagnosticCopyState('failed');
    }
  }, [route.kind]);

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return (
        <div className={styles.authRequired}>
          <div className={styles.authRequiredIcon}>🔐</div>
          <h2 className={styles.authRequiredTitle}>{t('shop.privateLogin.title')}</h2>
          <p className={styles.authRequiredDescription}>
            {t('shop.privateLogin.description')}
          </p>
        </div>
      );
    }

    if (route.kind === 'order-detail') {
      const backHref = buildShopPath({ kind: 'orders' });
      return (
        <OrderDetail
          orderId={route.orderId}
          order={dataState.selectedOrder}
          loading={dataState.loadingOrderDetail}
          loadError={dataState.loadError}
          backHref={backHref}
          inventoryHref={inventoryHref}
          productHref={dataState.selectedOrder
            ? buildPublicShopPath({ kind: 'detail', productId: String(dataState.selectedOrder.voProductId) })
            : undefined}
          onBack={navigate.back}
          onInventoryClick={navigate.toInventory}
          onProductClick={navigate.toProductDetail}
          onCancelOrder={actionsState.handleCancelOrder}
          onRetry={() => {
            void loadOrderDetail(route.orderId);
          }}
          diagnosticActionLabel={diagnosticActionLabel}
          onCopyDiagnostics={handleCopyShopDiagnostics}
        />
      );
    }

    if (route.kind === 'inventory') {
      return (
        <Inventory
          benefits={dataState.userBenefits}
          inventory={dataState.userInventory}
          loading={dataState.loadingInventory}
          loadError={dataState.loadError}
          backHref={publicShopHref}
          getSourceOrderHref={(orderId) => buildShopPath({ kind: 'order-detail', orderId })}
          getSourceProductHref={(productId) => buildPublicShopPath({ kind: 'detail', productId: String(productId) })}
          onActivateBenefit={actionsState.handleActivateBenefit}
          onDeactivateBenefit={actionsState.handleDeactivateBenefit}
          onUseItem={actionsState.handleUseItem}
          onUseRenameCard={actionsState.handleUseRenameCard}
          onSourceOrderClick={navigate.toOrderDetail}
          onSourceProductClick={navigate.toProductDetail}
          onBack={navigate.back}
          onRetry={() => {
            void loadInventory();
          }}
          diagnosticActionLabel={diagnosticActionLabel}
          onCopyDiagnostics={handleCopyShopDiagnostics}
        />
      );
    }

    return (
      <OrderList
        orders={dataState.orders}
        currentPage={dataState.currentPage}
        totalPages={dataState.totalPages}
        loading={dataState.loadingOrders}
        loadError={dataState.loadError}
        backHref={publicShopHref}
        getOrderHref={(orderId) => buildShopPath({ kind: 'order-detail', orderId })}
        onOrderClick={navigate.toOrderDetail}
        onPageChange={actionsState.handlePageChange}
        onBack={navigate.back}
        onRetry={() => {
          void loadOrders();
        }}
        diagnosticActionLabel={diagnosticActionLabel}
        onCopyDiagnostics={handleCopyShopDiagnostics}
      />
    );
  };

  return (
    <div className={styles.container}>
      <PublicShellHeader
        variant="private"
        activeKey="more"
        brandMark="商"
        brandName={t('shop.title')}
        brandSubline={t('shop.privateShellSubline', { defaultValue: '订单、背包与购买回流' })}
        onBrandClick={navigate.toPublicShop}
      />

      <div className={styles.content}>
        <nav className={styles.privateRouteNav} aria-label={t('shop.title')}>
          <a className={styles.privateRouteNavItem} href={publicShopHref}>
            {t('shop.nav.home')}
          </a>
          <a
            className={`${styles.privateRouteNavItem} ${route.kind === 'orders' || route.kind === 'order-detail' ? styles.privateRouteNavItemActive : ''}`}
            href={ordersHref}
            onClick={handleOrdersLinkClick}
          >
            {t('shop.nav.orders')}
          </a>
          <a
            className={`${styles.privateRouteNavItem} ${route.kind === 'inventory' ? styles.privateRouteNavItemActive : ''}`}
            href={inventoryHref}
            onClick={handleInventoryLinkClick}
          >
            {t('shop.nav.inventory')}
          </a>
        </nav>
        <Suspense fallback={renderShopLoadingFallback()}>
          {renderContent()}
        </Suspense>
      </div>

      {dataState.error && (
        <div className={styles.errorToast}>
          {dataState.error}
        </div>
      )}
    </div>
  );
}
