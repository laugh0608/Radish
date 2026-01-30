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
      setError('请先登录');
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
      setError('请先登录');
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

        // 显示成功消息
        const successMessage = `购买成功！${result.data.deductedCoins ? `消费 ${result.data.deductedCoins} 胡萝卜，` : ''}剩余余额 ${result.data.remainingBalance || 0} 胡萝卜`;

        // 这里可以显示成功 Toast
        log.debug(successMessage);

        // 刷新相关数据
        if (appState.currentView === 'product-detail') {
          await checkCanBuy(productId);
        }
      } else {
        throw new Error(result.data?.errorMessage || result.message || '购买失败');
      }
    } catch (error) {
      log.error('购买失败:', error);
      setError(error instanceof Error ? error.message : '购买失败');
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
        throw new Error(result.message || '取消订单失败');
      }
    } catch (error) {
      log.error('取消订单失败:', error);
      setError(error instanceof Error ? error.message : '取消订单失败');
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
        throw new Error(result.message || '激活权益失败');
      }
    } catch (error) {
      log.error('激活权益失败:', error);
      setError(error instanceof Error ? error.message : '激活权益失败');
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
        throw new Error(result.message || '取消激活权益失败');
      }
    } catch (error) {
      log.error('取消激活权益失败:', error);
      setError(error instanceof Error ? error.message : '取消激活权益失败');
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

        // 显示使用效果
        if (result.data.effectDescription) {
          log.debug('使用效果:', result.data.effectDescription);
        }

        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.data?.errorMessage || result.message || '使用道具失败');
      }
    } catch (error) {
      log.error('使用道具失败:', error);
      setError(error instanceof Error ? error.message : '使用道具失败');
    }
  }, [t, loadInventory, setError]);

  // 使用改名卡
  const handleUseRenameCard = useCallback(async (inventoryId: number, newNickname: string) => {
    try {
      const result = await shopApi.useRenameCard(inventoryId, newNickname, t);
      if (result.ok && result.data?.success) {
        setError(null);

        // 显示使用效果
        if (result.data.effectDescription) {
          log.debug('改名成功:', result.data.effectDescription);
        }

        // 刷新背包数据
        await loadInventory();
      } else {
        throw new Error(result.data?.errorMessage || result.message || '使用改名卡失败');
      }
    } catch (error) {
      log.error('使用改名卡失败:', error);
      setError(error instanceof Error ? error.message : '使用改名卡失败');
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
