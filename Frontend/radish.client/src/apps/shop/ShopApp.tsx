import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import type { LongId } from '@/api/user';
import { ContentReportModal } from '@/components/ContentReportModal';
import { ShopHome } from './pages/ShopHome';
import { ProductList } from './pages/ProductList';
import { ProductDetail } from './pages/ProductDetail';
import { OrderList } from './pages/OrderList';
import { OrderDetail } from './pages/OrderDetail';
import { Inventory } from './pages/Inventory';
import { PurchaseModal } from './components/PurchaseModal';
import { useShopData } from './hooks/useShopData';
import { useShopActions } from './hooks/useShopActions';
import styles from './ShopApp.module.css';

export type ShopView = 'home' | 'products' | 'product-detail' | 'orders' | 'order-detail' | 'inventory';

export interface ShopAppState {
  currentView: ShopView;
  selectedCategoryId?: string;
  selectedProductId?: LongId;
  selectedOrderId?: LongId;
  searchKeyword?: string;
}

function normalizePositiveLongId(value: unknown): LongId | undefined {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? value : undefined;
  }

  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function parseShopWindowParams(appParams?: Record<string, unknown> | null): { productId?: LongId; orderId?: LongId } {
  if (!appParams) {
    return {};
  }

  return {
    productId: normalizePositiveLongId(appParams.productId),
    orderId: normalizePositiveLongId(appParams.orderId)
  };
}

export const ShopApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated } = useUserStore();
  const currentWindow = useCurrentWindow();
  const loggedIn = isAuthenticated();
  const [reportProductId, setReportProductId] = useState<LongId | null>(null);
  const windowParams = useMemo(
    () => parseShopWindowParams(currentWindow?.appParams),
    [currentWindow?.appParams]
  );

  // 应用状态
  const [appState, setAppState] = useState<ShopAppState>(() => {
    if (windowParams.productId) {
      return {
        currentView: 'product-detail',
        selectedProductId: windowParams.productId
      };
    }

    if (windowParams.orderId) {
      return {
        currentView: 'order-detail',
        selectedOrderId: windowParams.orderId
      };
    }

    return {
      currentView: 'home'
    };
  });

  useEffect(() => {
    if (!windowParams.productId) {
      return;
    }

    setAppState((current) => {
      if (
        current.currentView === 'product-detail'
        && String(current.selectedProductId) === String(windowParams.productId)
      ) {
        return current;
      }

      return {
        ...current,
        currentView: 'product-detail',
        selectedProductId: windowParams.productId
      };
    });
  }, [windowParams.productId]);

  useEffect(() => {
    if (!windowParams.orderId) {
      return;
    }

    setAppState((current) => {
      if (
        current.currentView === 'order-detail'
        && String(current.selectedOrderId) === String(windowParams.orderId)
      ) {
        return current;
      }

      return {
        ...current,
        currentView: 'order-detail',
        selectedOrderId: windowParams.orderId
      };
    });
  }, [windowParams.orderId]);

  // 数据管理
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
    selectedProduct
  } = dataState;

  // 事件处理
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
    selectedProduct
  });

  // 导航方法
  const navigate = {
    toHome: () => setAppState({ currentView: 'home' }),
    toProducts: (categoryId?: string) => setAppState({
      currentView: 'products',
      selectedCategoryId: categoryId
    }),
    toProductDetail: (productId: LongId) => setAppState({
      currentView: 'product-detail',
      selectedProductId: productId
    }),
    toOrders: () => setAppState({ currentView: 'orders' }),
    toOrderDetail: (orderId: LongId) => setAppState({
      currentView: 'order-detail',
      selectedOrderId: orderId
    }),
    toInventory: () => setAppState({ currentView: 'inventory' }),
    back: () => {
      switch (appState.currentView) {
        case 'product-detail':
          setAppState({ currentView: 'products', selectedCategoryId: appState.selectedCategoryId });
          break;
        case 'order-detail':
          setAppState({ currentView: 'orders' });
          break;
        default:
          setAppState({ currentView: 'home' });
      }
    }
  };

  const handleOpenProductReport = (productId: LongId) => {
    if (!loggedIn) {
      setError(t('report.loginRequired'));
      return;
    }

    setError(null);
    setReportProductId(productId);
  };

  // 监听视图变化，加载对应数据
  useEffect(() => {
    switch (appState.currentView) {
      case 'product-detail':
        if (appState.selectedProductId) {
          loadProductDetail(appState.selectedProductId);
          if (loggedIn) {
            checkCanBuy(appState.selectedProductId);
          }
        }
        break;
      case 'products':
        loadProducts(appState.selectedCategoryId);
        break;
      case 'orders':
        if (loggedIn) {
          loadOrders();
        }
        break;
      case 'order-detail':
        if (appState.selectedOrderId && loggedIn) {
          loadOrderDetail(appState.selectedOrderId);
        }
        break;
      case 'inventory':
        if (loggedIn) {
          loadInventory();
        }
        break;
    }
  }, [
    appState.currentView,
    appState.selectedProductId,
    appState.selectedOrderId,
    appState.selectedCategoryId,
    loggedIn,
    loadProductDetail,
    checkCanBuy,
    loadProducts,
    loadOrders,
    loadOrderDetail,
    loadInventory
  ]);

  // 渲染当前视图
  const renderCurrentView = () => {
    switch (appState.currentView) {
      case 'home':
        return (
          <ShopHome
            categories={dataState.categories}
            featuredProducts={dataState.featuredProducts}
            loading={dataState.loadingCategories || dataState.loadingFeatured}
            onCategoryClick={navigate.toProducts}
            onProductClick={navigate.toProductDetail}
            onViewAllProducts={() => navigate.toProducts()}
          />
        );

      case 'products':
        return (
          <ProductList
            categories={dataState.categories}
            products={dataState.products}
            selectedCategoryId={appState.selectedCategoryId}
            currentPage={dataState.currentPage}
            totalPages={dataState.totalPages}
            searchKeyword={appState.searchKeyword}
            loading={dataState.loadingProducts}
            onCategoryChange={(categoryId) => {
              setAppState(prev => ({ ...prev, selectedCategoryId: categoryId }));
              loadProducts(categoryId);
            }}
            onProductClick={navigate.toProductDetail}
            onSearchChange={(keyword) => {
              setAppState(prev => ({ ...prev, searchKeyword: keyword }));
              searchProducts(keyword);
            }}
            onPageChange={actionsState.handlePageChange}
            onBack={navigate.back}
          />
        );

      case 'product-detail':
        return (
          <ProductDetail
            productId={appState.selectedProductId!}
            product={dataState.selectedProduct}
            loading={dataState.loadingProductDetail}
            canBuy={dataState.canBuyProduct}
            checkingCanBuy={dataState.checkingCanBuy}
            isAuthenticated={loggedIn}
            onBack={navigate.back}
            onPurchase={actionsState.handlePurchaseClick}
            onReport={handleOpenProductReport}
          />
        );

      case 'orders':
        return (
          <OrderList
            orders={dataState.orders}
            currentPage={dataState.currentPage}
            totalPages={dataState.totalPages}
            loading={dataState.loadingOrders}
            onOrderClick={navigate.toOrderDetail}
            onPageChange={actionsState.handlePageChange}
            onBack={navigate.back}
          />
        );

      case 'order-detail':
        return (
          <OrderDetail
            orderId={appState.selectedOrderId!}
            order={dataState.selectedOrder}
            loading={dataState.loadingOrderDetail}
            onBack={navigate.back}
            onCancelOrder={actionsState.handleCancelOrder}
          />
        );

      case 'inventory':
        return (
          <Inventory
            benefits={dataState.userBenefits}
            inventory={dataState.userInventory}
            loading={dataState.loadingInventory}
            onActivateBenefit={actionsState.handleActivateBenefit}
            onDeactivateBenefit={actionsState.handleDeactivateBenefit}
            onUseItem={actionsState.handleUseItem}
            onUseRenameCard={actionsState.handleUseRenameCard}
            onBack={navigate.back}
          />
        );

      default:
        return <div>{t('shop.pageNotFound')}</div>;
    }
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航栏 */}
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <h1 className={styles.title} onClick={navigate.toHome}>
            🛒 {t('shop.title')}
          </h1>

          {loggedIn && (
            <div className={styles.nav}>
              <button
                className={`${styles.navButton} ${appState.currentView === 'home' ? styles.active : ''}`}
                onClick={navigate.toHome}
              >
                {t('shop.nav.home')}
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'products' ? styles.active : ''}`}
                onClick={() => navigate.toProducts()}
              >
                {t('shop.nav.products')}
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'orders' ? styles.active : ''}`}
                onClick={navigate.toOrders}
              >
                {t('shop.nav.orders')}
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'inventory' ? styles.active : ''}`}
                onClick={navigate.toInventory}
              >
                {t('shop.nav.inventory')}
              </button>
            </div>
          )}
        </div>
      </div>

      {/* 主要内容区域 */}
      <div className={styles.content}>
        {renderCurrentView()}
      </div>

      {/* 购买确认弹窗 */}
      <PurchaseModal
        isOpen={actionsState.isPurchaseModalOpen}
        product={dataState.selectedProduct}
        loading={actionsState.purchasing}
        passcodeUpgradePrompt={actionsState.purchasePasscodeUpgradePrompt}
        onClose={actionsState.handleClosePurchaseModal}
        onConfirm={actionsState.handleConfirmPurchase}
      />

      {reportProductId !== null && (
        <ContentReportModal
          isOpen={reportProductId !== null}
          targetType="Product"
          targetId={reportProductId}
          onClose={() => setReportProductId(null)}
        />
      )}

      {/* 错误提示 */}
      {dataState.error && (
        <div className={styles.errorToast}>
          {dataState.error}
        </div>
      )}
    </div>
  );
};
