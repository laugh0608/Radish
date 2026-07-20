/**
 * 萝卜币系统相关的 API 调用。
 */

import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type PagedResponse,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';
import type { LongId } from './user';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type CoinAmount = LongId;

export interface UserBalance {
  voUserId: LongId;
  voBalance: CoinAmount;
  voBalanceDisplay: string;
  voFrozenBalance: CoinAmount;
  voFrozenBalanceDisplay: string;
  voTotalEarned: CoinAmount;
  voTotalSpent: CoinAmount;
  voTotalTransferredIn: CoinAmount;
  voTotalTransferredOut: CoinAmount;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface CoinTransaction {
  voId: LongId;
  voTransactionNo: string;
  voFromUserId: LongId | null;
  voFromUserName: string | null;
  voToUserId: LongId | null;
  voToUserName: string | null;
  voAmount: CoinAmount;
  voAmountDisplay: string;
  voFee: CoinAmount;
  voFeeDisplay: string;
  voTransactionType: string;
  /** 后端兼容展示字段；client 的控制与本地化只使用稳定 voTransactionType。 */
  voTransactionTypeDisplay: string;
  voStatus: string;
  /** 后端兼容展示字段；client 的控制与本地化只使用稳定 voStatus。 */
  voStatusDisplay: string;
  voBusinessType: string | null;
  voBusinessId: LongId | null;
  voRemark: string | null;
  voCreateTime: string;
  voTheoreticalAmount?: number | null;
  voRoundingDiff?: number | null;
}

function ensureRequiredResponse<T>(response: ParsedApiResponse<T>, fallbackMessage: string): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? { ...response, message: undefined } : response,
      fallbackMessage,
    );
  }

  return response.data;
}

export async function getBalance(t: TFunction): Promise<UserBalance> {
  const response = await apiGet<UserBalance>('/api/v1/Coin/GetBalance', { withAuth: true });
  return ensureRequiredResponse(response, t('pit.api.balanceFailed'));
}

export async function getTransactions(
  pageIndex: number,
  pageSize: number,
  transactionType: string | null,
  status: string | null,
  t: TFunction,
): Promise<PagedResponse<CoinTransaction>> {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString(),
  });

  if (transactionType) {
    params.append('transactionType', transactionType);
  }
  if (status) {
    params.append('status', status);
  }

  const response = await apiGet<PagedResponse<CoinTransaction>>(
    `/api/v1/Coin/GetTransactions?${params.toString()}`,
    { withAuth: true },
  );
  return ensureRequiredResponse(response, t('pit.api.transactionsFailed'));
}

export async function getTransactionByNo(transactionNo: string, t: TFunction): Promise<CoinTransaction> {
  const response = await apiGet<CoinTransaction>(
    `/api/v1/Coin/GetTransactionByNo?transactionNo=${encodeURIComponent(transactionNo)}`,
    { withAuth: true },
  );
  return ensureRequiredResponse(response, t('pit.api.transactionDetailFailed'));
}

export const TransactionType = {
  SYSTEM_GRANT: 'SYSTEM_GRANT',
  LIKE_REWARD: 'LIKE_REWARD',
  COMMENT_REWARD: 'COMMENT_REWARD',
  HIGHLIGHT_REWARD: 'HIGHLIGHT_REWARD',
  GODLIKE_REWARD: 'GODLIKE_REWARD',
  SOFA_REWARD: 'SOFA_REWARD',
  TRANSFER: 'TRANSFER',
  TIP: 'TIP',
  CONSUME: 'CONSUME',
  REFUND: 'REFUND',
  PENALTY: 'PENALTY',
  ADMIN_ADJUST: 'ADMIN_ADJUST',
} as const;

export const TransactionStatus = {
  PENDING: 'PENDING',
  SUCCESS: 'SUCCESS',
  FAILED: 'FAILED',
} as const;

export interface TransferRequest {
  toUserId: LongId;
  amount: number;
  remark?: string;
  paymentPassword: string;
  idempotencyKey?: string;
}

export interface TransferResponse {
  voTransactionNo: string;
  voErrorCode?: string;
  voRequiresPasscodeUpgrade?: boolean;
}

export async function transfer(request: TransferRequest, t: TFunction): Promise<TransferResponse> {
  const response = await apiPost<TransferResponse>('/api/v1/Coin/Transfer', request, { withAuth: true });
  return ensureRequiredResponse(response, t('pit.api.transferFailed'));
}

export interface TrendDataItem {
  voDate: string;
  voIncome: CoinAmount;
  voExpense: CoinAmount;
}

export interface CategoryStatItem {
  /** 稳定分类代码，例如 IN_TRANSFER / OUT_CONSUME。 */
  voCategory: string;
  voAmount: CoinAmount;
  voCount: number;
}

export interface CoinStatistics {
  voTrendData: TrendDataItem[];
  voCategoryStats: CategoryStatItem[];
}

export async function getStatistics(
  timeRange: 'month' | 'quarter' | 'year',
  t: TFunction,
): Promise<CoinStatistics> {
  const response = await apiGet<CoinStatistics>(
    `/api/v1/Coin/GetStatistics?timeRange=${timeRange}`,
    { withAuth: true },
  );
  return ensureRequiredResponse(response, t('pit.api.statisticsFailed'));
}

export const coinApi = {
  getBalance,
  getTransactions,
  getTransactionByNo,
  transfer,
  getStatistics,
  TransactionType,
  TransactionStatus,
};
