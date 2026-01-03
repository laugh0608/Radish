/**
 * 萝卜币系统相关的 API 调用
 */

import { parseApiResponse, type ApiResponse } from '@/api/client';
import type { TFunction } from 'i18next';

const defaultApiBase = 'https://localhost:5000';

/**
 * 获取 API Base URL
 */
function getApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return (configured ?? defaultApiBase).replace(/\/$/, '');
}

/**
 * 带认证的 fetch 封装
 */
interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

/**
 * 用户余额信息
 */
export interface UserBalance {
  userId: string;
  balance: number;
  balanceDisplay: string;
  frozenBalance: number;
  frozenBalanceDisplay: string;
  totalEarned: number;
  totalSpent: number;
  totalTransferredIn: number;
  totalTransferredOut: number;
  version: number;
}

/**
 * 交易记录
 */
export interface CoinTransaction {
  id: string;
  transactionNo: string;
  fromUserId: string | null;
  fromUserName: string;
  toUserId: string | null;
  toUserName: string;
  amount: number;
  amountDisplay: string;
  fee: number;
  feeDisplay: string;
  transactionType: string;
  transactionTypeDisplay: string;
  status: string;
  statusDisplay: string;
  businessType: string | null;
  businessId: string | null;
  remark: string | null;
  createTime: string;
}

/**
 * 分页响应
 */
export interface PagedResponse<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

/**
 * 获取当前用户余额信息
 * @param t i18n 翻译函数
 * @returns 用户余额信息
 */
export async function getBalance(t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Coin/GetBalance`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取余额失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UserBalance>;
  return parseApiResponse(json, t);
}

/**
 * 获取当前用户交易记录（分页）
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量
 * @param transactionType 交易类型（可选）
 * @param status 交易状态（可选）
 * @param t i18n 翻译函数
 * @returns 分页的交易记录
 */
export async function getTransactions(
  pageIndex: number,
  pageSize: number,
  transactionType: string | null,
  status: string | null,
  t: TFunction
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

  const url = `${getApiBaseUrl()}/api/v1/Coin/GetTransactions?${params.toString()}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取交易记录失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<PagedResponse<CoinTransaction>>;
  return parseApiResponse(json, t);
}

/**
 * 根据交易流水号获取交易详情
 * @param transactionNo 交易流水号
 * @param t i18n 翻译函数
 * @returns 交易详情
 */
export async function getTransactionByNo(transactionNo: string, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Coin/GetTransactionByNo?transactionNo=${encodeURIComponent(transactionNo)}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('交易记录不存在');
    }
    throw new Error(`获取交易详情失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<CoinTransaction>;
  return parseApiResponse(json, t);
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
export function formatCoinAmount(amount: number, mode: 'carrot' | 'radish' | 'mixed' = 'carrot'): string {
  if (mode === 'carrot') {
    return `${amount.toLocaleString()} 胡萝卜`;
  }

  const radish = amount / 1000;
  if (mode === 'radish') {
    return `${radish.toFixed(3)} 白萝卜`;
  }

  // mixed mode
  const wholeRadish = Math.floor(radish);
  const remainingCarrot = amount % 1000;

  if (wholeRadish === 0) {
    return `${remainingCarrot} 胡萝卜`;
  }

  if (remainingCarrot === 0) {
    return `${wholeRadish} 白萝卜`;
  }

  return `${wholeRadish} 白萝卜 ${remainingCarrot} 胡萝卜`;
}
