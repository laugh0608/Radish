import { useState, useCallback } from 'react';
import { log } from '@/utils/logger';
import type { TFunction } from 'i18next';
import type { Product } from '@/types/shop';
import * as shopApi from '@/api/shop';
import type { ShopAppState } from '../ShopApp';

interface UseShopActionsProps {
  t: TFunction;
  isAuthenticated: boolean;
  appState: ShopAppState;
  setError: (error: string | null) => void;
  loadProducts: (categoryId?: string, productType?: shopApi.ProductTypeValue, keyword?: string, pageIndex?: number, pageSize?: number) => Promise<void>;
  loadProductDetail: (productId: number) => Promise<void>;
  checkCanBuy: (productId: number, quantity?: number) => Promise<void>;
  loadOrders: (status?: shopApi.OrderStatusValue, pageIndex?: number, pageSize?: number) => Promise<void>;
  loadOrderDetail: (orderId: number) => Promise<void>;
  loadInventory: () => Promise<void>;
  searchProducts: (keyword: string) => Promise<void>;
  selectedProduct: Product | null;
}

export const useShopActions = (props: UseShopActionsProps) => {
  const {
    t,
    isAuthenticated,
    appState,
    setError,
    loadProducts,
    loadProductDetail,
    checkCanBuy,
    loadOrders,
    loadOrderDetail,
    loadInventory,
    selectedProduct
  } = props;

  // 购买相关状态
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);
  const [purchasing, setPurchasing] = useState(false);

  // 处理分页变化
  const handlePageChange = useCallback(async (page: number) => {
    switch (appState.currentView) {
      case 'products':
        await loadProducts(appState.selectedCategoryId, undefined, appState.searchKeyword, page);
        break;
      case 'orders':
        await loadOrders(undefined, page);
        break;
    }
  }, [appState, loadProducts, loadOrders]);

  // 处理购买点击
  const handlePurchaseClick = useCallback(async (productId: number) => {
    if (!isAuthenticated) {
      setError(t('shop.loginRequired'));
      return;
    }

    // 加载商品详情（如果还没有）
    if (!selectedProduct || selectedProduct.voId !== productId) {
      await loadProductDetail(productId);
    }

    // 检查是否可以购买
    await checkCanBuy(productId);

    setIsPurchaseModalOpen(true);
  }, [isAuthenticated, selectedProduct, loadProductDetail, checkCanBuy, setError]);

  // 关闭购买弹窗
  const handleClosePurchaseModal = useCallback(() => {
    setIsPurchaseModalOpen(false);
  }, []);

  // 确认购买
  const handleConfirmPurchase = useCallback(async (productId: number, quantity: number = 1) => {
    if (!isAuthenticated) {
      setError(t('shop.loginRequired'));
      return;
    }

    setPurchasing(true);
    try {
      const result = await shopApi.purchaseProduct({
        productId,
        quantity
      }, t);

      if (result.ok && result.data?.success) {
        setError(null);
        setIsPurchaseModalOpen(false);

        log.debug(
          t('shop.purchase.success'),
          t('shop.purchase.successDetail', {
            deducted: result.data.deductedCoins ?? 0,
            remaining: result.data.remainingBalance ?? 0
          })
        );

        // 刷新相关数据
        if (appState.currentView === 'product-detail') {
          await checkCanBuy(productId);
        }
      } else {
        throw new Error(result.data?.errorMessage || result.message || t('shop.error.purchaseFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.purchaseFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.purchaseFailed'));
    } finally {
      setPurchasing(false);
    }
  }, [isAuthenticated, t, appState.currentView, checkCanBuy, setError]);

  // 取消订单
  const handleCancelOrder = useCallback(async (orderId: number, reason?: string) => {
    try {
      const result = await shopApi.cancelOrder(orderId, t, reason);
      if (result.ok) {
        setError(null);
        // 刷新订单详情
        await loadOrderDetail(orderId);
      } else {
        throw new Error(result.message || t('shop.error.cancelOrderFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.cancelOrderFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.cancelOrderFailed'));
    }
  }, [t, loadOrderDetail, setError]);

  // 激活权益
  const handleActivateBenefit = useCallback(async (benefitId: number) => {
    try {
      const result = await shopApi.activateBenefit(benefitId, t);
      if (result.ok) {
        setError(null);
        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.message || t('shop.error.activateBenefitFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.activateBenefitFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.activateBenefitFailed'));
    }
  }, [t, loadInventory, setError]);

  // 取消激活权益
  const handleDeactivateBenefit = useCallback(async (benefitId: number) => {
    try {
      const result = await shopApi.deactivateBenefit(benefitId, t);
      if (result.ok) {
        setError(null);
        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.message || t('shop.error.deactivateBenefitFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.deactivateBenefitFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.deactivateBenefitFailed'));
    }
  }, [t, loadInventory, setError]);

  // 使用道具
  const handleUseItem = useCallback(async (inventoryId: number, quantity: number = 1, targetId?: number) => {
    try {
      const result = await shopApi.useItem({
        inventoryId,
        quantity,
        targetId
      }, t);

      if (result.ok && result.data?.success) {
        setError(null);

        if (result.data.effectDescription) {
          log.debug(t('shop.inventory.useEffect'), result.data.effectDescription);
        }

        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.data?.errorMessage || result.message || t('shop.error.useItemFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.useItemFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.useItemFailed'));
    }
  }, [t, loadInventory, setError]);

  // 使用改名卡
  const handleUseRenameCard = useCallback(async (inventoryId: number, newNickname: string) => {
    try {
      const result = await shopApi.useRenameCard(inventoryId, newNickname, t);
      if (result.ok && result.data?.success) {
        setError(null);

        if (result.data.effectDescription) {
          log.debug(t('shop.inventory.renameSuccess'), result.data.effectDescription);
        }

        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.data?.errorMessage || result.message || t('shop.error.useRenameCardFailed'));
      }
    } catch (error) {
      log.error(t('shop.error.useRenameCardFailed'), error);
      setError(error instanceof Error ? error.message : t('shop.error.useRenameCardFailed'));
    }
  }, [t, loadInventory, setError]);

  return {
    // 购买相关
    isPurchaseModalOpen,
    purchasing,
    handlePurchaseClick,
    handleClosePurchaseModal,
    handleConfirmPurchase,

    // 分页
    handlePageChange,

    // 订单操作
    handleCancelOrder,

    // 权益操作
    handleActivateBenefit,
    handleDeactivateBenefit,

    // 道具操作
    handleUseItem,
    handleUseRenameCard
  };
};
