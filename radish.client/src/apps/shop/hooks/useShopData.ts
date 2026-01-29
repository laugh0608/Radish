import { useState, useEffect, useCallback } from 'react';
import { log } from '@/utils/logger';
import type { TFunction } from 'i18next';
import type {
  ProductCategory,
  ProductListItem,
  Product,
  OrderListItem,
  Order,
  UserBenefit,
  UserInventoryItem
} from '@/types/shop';
import * as shopApi from '@/api/shop';

export interface ShopDataState {
  // 分类数据
  categories: ProductCategory[];
  loadingCategories: boolean;

  // 商品数据
  products: ProductListItem[];
  featuredProducts: ProductListItem[];
  selectedProduct: Product | null;
  loadingProducts: boolean;
  loadingFeatured: boolean;
  loadingProductDetail: boolean;

  // 分页数据
  currentPage: number;
  totalPages: number;

  // 购买相关
  canBuyProduct: { canBuy: boolean; reason: string } | null;
  checkingCanBuy: boolean;

  // 订单数据
  orders: OrderListItem[];
  selectedOrder: Order | null;
  loadingOrders: boolean;
  loadingOrderDetail: boolean;

  // 用户权益和背包
  userBenefits: UserBenefit[];
  userInventory: UserInventoryItem[];
  loadingInventory: boolean;

  // 错误状态
  error: string | null;
}

export const useShopData = (t: TFunction) => {
  const [state, setState] = useState<ShopDataState>({
    categories: [],
    loadingCategories: false,
    products: [],
    featuredProducts: [],
    selectedProduct: null,
    loadingProducts: false,
    loadingFeatured: false,
    loadingProductDetail: false,
    currentPage: 1,
    totalPages: 1,
    canBuyProduct: null,
    checkingCanBuy: false,
    orders: [],
    selectedOrder: null,
    loadingOrders: false,
    loadingOrderDetail: false,
    userBenefits: [],
    userInventory: [],
    loadingInventory: false,
    error: null
  });

  // 设置错误
  const setError = useCallback((error: string | null) => {
    setState(prev => ({ ...prev, error }));
    if (error) {
      // 3秒后自动清除错误
      setTimeout(() => {
        setState(prev => ({ ...prev, error: null }));
      }, 3000);
    }
  }, []);

  // 加载分类列表
  const loadCategories = useCallback(async () => {
    setState(prev => ({ ...prev, loadingCategories: true }));
    try {
      const result = await shopApi.getCategories(t);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          categories: result.data || [],
          loadingCategories: false
        }));
      } else {
        throw new Error(result.message || '获取分类失败');
      }
    } catch (error) {
      log.error('加载分类失败:', error);
      setError(error instanceof Error ? error.message : '加载分类失败');
      setState(prev => ({ ...prev, loadingCategories: false }));
    }
  }, [t, setError]);

  // 加载商品列表
  const loadProducts = useCallback(async (
    categoryId?: string,
    productType?: shopApi.ProductTypeValue,
    keyword?: string,
    pageIndex: number = 1,
    pageSize: number = 20
  ) => {
    setState(prev => ({ ...prev, loadingProducts: true }));
    try {
      const result = await shopApi.getProducts(t, categoryId, productType, keyword, pageIndex, pageSize);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          products: result.data?.data || [],
          currentPage: result.data?.page || 1,
          totalPages: result.data?.pageCount || 1,
          loadingProducts: false
        }));
      } else {
        throw new Error(result.message || '获取商品列表失败');
      }
    } catch (error) {
      log.error('加载商品列表失败:', error);
      setError(error instanceof Error ? error.message : '加载商品列表失败');
      setState(prev => ({ ...prev, loadingProducts: false }));
    }
  }, [t, setError]);

  // 加载推荐商品
  const loadFeaturedProducts = useCallback(async () => {
    setState(prev => ({ ...prev, loadingFeatured: true }));
    try {
      // 获取前8个商品作为推荐商品
      const result = await shopApi.getProducts(t, undefined, undefined, undefined, 1, 8);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          featuredProducts: result.data?.data || [],
          loadingFeatured: false
        }));
      } else {
        throw new Error(result.message || '获取推荐商品失败');
      }
    } catch (error) {
      log.error('加载推荐商品失败:', error);
      setError(error instanceof Error ? error.message : '加载推荐商品失败');
      setState(prev => ({ ...prev, loadingFeatured: false }));
    }
  }, [t, setError]);

  // 加载商品详情
  const loadProductDetail = useCallback(async (productId: number) => {
    setState(prev => ({ ...prev, loadingProductDetail: true, selectedProduct: null }));
    try {
      const result = await shopApi.getProduct(productId, t);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          selectedProduct: result.data || null,
          loadingProductDetail: false
        }));
      } else {
        throw new Error(result.message || '获取商品详情失败');
      }
    } catch (error) {
      log.error('加载商品详情失败:', error);
      setError(error instanceof Error ? error.message : '加载商品详情失败');
      setState(prev => ({ ...prev, loadingProductDetail: false }));
    }
  }, [t, setError]);

  // 检查是否可以购买
  const checkCanBuy = useCallback(async (productId: number, quantity: number = 1) => {
    setState(prev => ({ ...prev, checkingCanBuy: true }));
    try {
      const result = await shopApi.checkCanBuy(productId, quantity, t);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          canBuyProduct: result.data || null,
          checkingCanBuy: false
        }));
      } else {
        throw new Error(result.message || '检查购买权限失败');
      }
    } catch (error) {
      log.error('检查购买权限失败:', error);
      setError(error instanceof Error ? error.message : '检查购买权限失败');
      setState(prev => ({ ...prev, checkingCanBuy: false }));
    }
  }, [t, setError]);

  // 加载订单列表
  const loadOrders = useCallback(async (
    status?: shopApi.OrderStatusValue,
    pageIndex: number = 1,
    pageSize: number = 20
  ) => {
    setState(prev => ({ ...prev, loadingOrders: true }));
    try {
      const result = await shopApi.getMyOrders(t, status, pageIndex, pageSize);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          orders: result.data?.data || [],
          currentPage: result.data?.page || 1,
          totalPages: result.data?.pageCount || 1,
          loadingOrders: false
        }));
      } else {
        throw new Error(result.message || '获取订单列表失败');
      }
    } catch (error) {
      log.error('加载订单列表失败:', error);
      setError(error instanceof Error ? error.message : '加载订单列表失败');
      setState(prev => ({ ...prev, loadingOrders: false }));
    }
  }, [t, setError]);

  // 加载订单详情
  const loadOrderDetail = useCallback(async (orderId: number) => {
    setState(prev => ({ ...prev, loadingOrderDetail: true, selectedOrder: null }));
    try {
      const result = await shopApi.getOrder(orderId, t);
      if (result.ok && result.data) {
        setState(prev => ({
          ...prev,
          selectedOrder: result.data || null,
          loadingOrderDetail: false
        }));
      } else {
        throw new Error(result.message || '获取订单详情失败');
      }
    } catch (error) {
      log.error('加载订单详情失败:', error);
      setError(error instanceof Error ? error.message : '加载订单详情失败');
      setState(prev => ({ ...prev, loadingOrderDetail: false }));
    }
  }, [t, setError]);

  // 加载用户权益和背包
  const loadInventory = useCallback(async () => {
    setState(prev => ({ ...prev, loadingInventory: true }));
    try {
      const [benefitsResult, inventoryResult] = await Promise.all([
        shopApi.getMyBenefits(false, t),
        shopApi.getMyInventory(t)
      ]);

      if (benefitsResult.ok && inventoryResult.ok) {
        setState(prev => ({
          ...prev,
          userBenefits: benefitsResult.data || [],
          userInventory: inventoryResult.data || [],
          loadingInventory: false
        }));
      } else {
        throw new Error('获取背包数据失败');
      }
    } catch (error) {
      log.error('加载背包数据失败:', error);
      setError(error instanceof Error ? error.message : '加载背包数据失败');
      setState(prev => ({ ...prev, loadingInventory: false }));
    }
  }, [t, setError]);

  // 搜索商品
  const searchProducts = useCallback(async (keyword: string) => {
    await loadProducts(undefined, undefined, keyword, 1, 20);
  }, [loadProducts]);

  // 初始化加载
  useEffect(() => {
    loadCategories();
    loadFeaturedProducts();
  }, [loadCategories, loadFeaturedProducts]);

  return {
    ...state,
    setError,
    loadCategories,
    loadProducts,
    loadFeaturedProducts,
    loadProductDetail,
    checkCanBuy,
    loadOrders,
    loadOrderDetail,
    loadInventory,
    searchProducts
  };
};
