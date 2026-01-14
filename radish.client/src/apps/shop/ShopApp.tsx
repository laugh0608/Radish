import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
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
  selectedProductId?: number;
  selectedOrderId?: number;
  searchKeyword?: string;
}

export const ShopApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userId } = useUserStore();
  const loggedIn = isAuthenticated();

  // 应用状态
  const [appState, setAppState] = useState<ShopAppState>({
    currentView: 'home'
  });

  // 数据管理
  const dataState = useShopData(t);

  // 事件处理
  const actionsState = useShopActions({
    t,
    isAuthenticated: loggedIn,
    appState,
    setError: dataState.setError,
    loadProducts: dataState.loadProducts,
    loadProductDetail: dataState.loadProductDetail,
    checkCanBuy: dataState.checkCanBuy,
    loadOrders: dataState.loadOrders,
    loadOrderDetail: dataState.loadOrderDetail,
    loadInventory: dataState.loadInventory,
    searchProducts: dataState.searchProducts,
    selectedProduct: dataState.selectedProduct
  });

  // 导航方法
  const navigate = {
    toHome: () => setAppState({ currentView: 'home' }),
    toProducts: (categoryId?: string) => setAppState({
      currentView: 'products',
      selectedCategoryId: categoryId
    }),
    toProductDetail: (productId: number) => setAppState({
      currentView: 'product-detail',
      selectedProductId: productId
    }),
    toOrders: () => setAppState({ currentView: 'orders' }),
    toOrderDetail: (orderId: number) => setAppState({
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
              dataState.loadProducts(categoryId);
            }}
            onProductClick={navigate.toProductDetail}
            onSearchChange={(keyword) => {
              setAppState(prev => ({ ...prev, searchKeyword: keyword }));
              dataState.searchProducts(keyword);
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
            onBack={navigate.back}
          />
        );

      default:
        return <div>页面不存在</div>;
    }
  };

  return (
    <div className={styles.container}>
      {/* 顶部导航栏 */}
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <h1 className={styles.title} onClick={navigate.toHome}>
            🛒 萝卜商城
          </h1>

          {loggedIn && (
            <div className={styles.nav}>
              <button
                className={`${styles.navButton} ${appState.currentView === 'home' ? styles.active : ''}`}
                onClick={navigate.toHome}
              >
                首页
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'products' ? styles.active : ''}`}
                onClick={() => navigate.toProducts()}
              >
                商品
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'orders' ? styles.active : ''}`}
                onClick={navigate.toOrders}
              >
                订单
              </button>
              <button
                className={`${styles.navButton} ${appState.currentView === 'inventory' ? styles.active : ''}`}
                onClick={navigate.toInventory}
              >
                背包
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
        onClose={actionsState.handleClosePurchaseModal}
        onConfirm={actionsState.handleConfirmPurchase}
      />

      {/* 错误提示 */}
      {dataState.error && (
        <div className={styles.errorToast}>
          {dataState.error}
        </div>
      )}
    </div>
  );
};