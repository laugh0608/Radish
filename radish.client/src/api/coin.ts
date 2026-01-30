/**
 * 萝卜币系统相关的 API 调用
 */

import { parseApiResponseWithI18n, apiGet, configureApiClient, type PagedResponse } from '@radish/ui';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 用户余额信息（后端ViewModel使用Vo前缀）
 */
export interface UserBalance {
  voUserId: string;
  voBalance: number;
  voBalanceDisplay: string;
  voFrozenBalance: number;
  voFrozenBalanceDisplay: string;
  voTotalEarned: number;
  voTotalSpent: number;
  voTotalTransferredIn: number;
  voTotalTransferredOut: number;
  voVersion: number;
}

/**
 * 交易记录（后端ViewModel使用Vo前缀）
 */
export interface CoinTransaction {
  voId: string;
  voTransactionNo: string;
  voFromUserId: string | null;
  voFromUserName: string;
  voToUserId: string | null;
  voToUserName: string;
  voAmount: number;
  voAmountDisplay: string;
  voFee: number;
  voFeeDisplay: string;
  voTransactionType: string;
  voTransactionTypeDisplay: string;
  voStatus: string;
  voStatusDisplay: string;
  voBusinessType: string | null;
  voBusinessId: string | null;
  voRemark: string | null;
  voCreateTime: string;
}

/**
 * 获取当前用户余额信息
 * @param t i18n 翻译函数（可选）
 * @returns 用户余额信息
 */
export async function getBalance(t?: TFunction) {
  const response = await apiGet<UserBalance>('/api/v1/Coin/GetBalance', { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '获取余额失败');
  }

  return response.data;
}

/**
 * 获取当前用户交易记录（分页）
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量
 * @param transactionType 交易类型（可选）
 * @param status 交易状态（可选）
 * @param t i18n 翻译函数（可选）
 * @returns 分页的交易记录
 */
export async function getTransactions(
  pageIndex: number,
  pageSize: number,
  transactionType: string | null = null,
  status: string | null = null,
  t?: TFunction
) {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (transactionType) {
    params.append('transactionType', transactionType);
  }

  if (status) {
    params.append('status', status);
  }

  const response = await apiGet<PagedResponse<CoinTransaction>>(
    `/api/v1/Coin/GetTransactions?${params.toString()}`,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '获取交易记录失败');
  }

  return response.data;
}

/**
 * 根据交易流水号获取交易详情
 * @param transactionNo 交易流水号
 * @param t i18n 翻译函数（可选）
 * @returns 交易详情
 */
export async function getTransactionByNo(transactionNo: string, t?: TFunction) {
  const response = await apiGet<CoinTransaction>(
    `/api/v1/Coin/GetTransactionByNo?transactionNo=${encodeURIComponent(transactionNo)}`,
    { withAuth: true }
  );

  if (!response.ok) {
    if (response.statusCode === 404) {
      throw new Error('交易记录不存在');
    }
    throw new Error(response.message || '获取交易详情失败');
  }

  return response.data;
}

/**
 * 交易类型枚举（用于筛选）
 */
export const TransactionType = {
  SYSTEM_GRANT: 'SYSTEM_GRANT',
  LIKE_REWARD: 'LIKE_REWARD',
  COMMENT_REWARD: 'COMMENT_REWARD',
  HIGHLIGHT_REWARD: 'HIGHLIGHT_REWARD',
  TRANSFER: 'TRANSFER',
  TIP: 'TIP',
  CONSUME: 'CONSUME',
  REFUND: 'REFUND',
  PENALTY: 'PENALTY',
  ADMIN_ADJUST: 'ADMIN_ADJUST'
} as const;

/**
 * 交易状态枚举（用于筛选）
 */
export const TransactionStatus = {
  PENDING: 'PENDING',
  SUCCESS: 'SUCCESS',
  FAILED: 'FAILED'
} as const;

/**
 * 格式化萝卜币数量显示
 * @param amount 胡萝卜数量
 * @param mode 显示模式：'carrot' | 'radish' | 'mixed'
 * @returns 格式化后的字符串
 */
export function formatCoinAmount(amount: number | undefined | null, mode: 'carrot' | 'radish' | 'mixed' = 'carrot'): string {
  // 防御性检查：处理 undefined、null 或非数字值
  if (amount === undefined || amount === null || isNaN(amount)) {
    return '0 胡萝卜';
  }

  // 确保 amount 是数字
  const numAmount = Number(amount);

  if (mode === 'carrot') {
    return `${numAmount.toLocaleString()} 胡萝卜`;
  }

  const radish = numAmount / 1000;
  if (mode === 'radish') {
    return `${radish.toFixed(3)} 白萝卜`;
  }

  // mixed mode
  const wholeRadish = Math.floor(radish);
  const remainingCarrot = numAmount % 1000;

  if (wholeRadish === 0) {
    return `${remainingCarrot} 胡萝卜`;
  }

  if (remainingCarrot === 0) {
    return `${wholeRadish} 白萝卜`;
  }

  return `${wholeRadish} 白萝卜 ${remainingCarrot} 胡萝卜`;
}

/**
 * 转账请求参数
 */
export interface TransferRequest {
  toUserId: number;
  amount: number;
  remark?: string;
  paymentPassword: string;
}

/**
 * 转账响应（返回交易流水号）
 */
export interface TransferResponse {
  transactionNo: string;
}

/**
 * 用户转账
 * @param request 转账请求参数
 * @param t i18n 翻译函数（可选）
 * @returns 交易流水号
 */
export async function transfer(request: TransferRequest, t?: TFunction) {
  const { apiPost } = await import('@radish/ui');
  const response = await apiPost<TransferResponse>(
    '/api/v1/Coin/Transfer',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '转账失败');
  }

  return response.data;
}

/**
 * 趋势数据项
 */
export interface TrendDataItem {
  voDate: string;
  voIncome: number;
  voExpense: number;
}

/**
 * 分类统计项
 */
export interface CategoryStatItem {
  voCategory: string;
  voAmount: number;
  voCount: number;
}

/**
 * 统计数据响应
 */
export interface CoinStatistics {
  voTrendData: TrendDataItem[];
  voCategoryStats: CategoryStatItem[];
}

/**
 * 获取统计数据
 * @param timeRange 时间范围（month/quarter/year）
 * @param t i18n 翻译函数（可选）
 * @returns 统计数据
 */
export async function getStatistics(timeRange: 'month' | 'quarter' | 'year' = 'month', t?: TFunction) {
  const response = await apiGet<CoinStatistics>(
    `/api/v1/Coin/GetStatistics?timeRange=${timeRange}`,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '获取统计数据失败');
  }

  return response.data;
}

/**
 * 萝卜币 API 对象（用于统一导入）
 */
export const coinApi = {
  getBalance,
  getTransactions,
  getTransactionByNo,
  transfer,
  getStatistics,
  formatCoinAmount,
  TransactionType,
  TransactionStatus,
};