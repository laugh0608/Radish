import { apiGet, apiPost, createApiResponseError, type PagedResponse } from '@radish/http';

export interface UserBalanceVo {
  voUserId: string;
  voBalance: string;
  voBalanceDisplay: string;
  voFrozenBalance: string;
  voFrozenBalanceDisplay: string;
  voTotalEarned: string;
  voTotalSpent: string;
  voTotalTransferredIn: string;
  voTotalTransferredOut: string;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface CoinTransactionVo {
  voId: string;
  voTransactionNo: string;
  voFromUserId?: string | null;
  voFromUserName?: string | null;
  voToUserId?: string | null;
  voToUserName?: string | null;
  voAmount: string;
  voAmountDisplay: string;
  voFee: string;
  voFeeDisplay: string;
  voTransactionType: string;
  voTransactionTypeDisplay: string;
  voStatus: string;
  voStatusDisplay: string;
  voBusinessType?: string | null;
  voBusinessId?: string | null;
  voRemark?: string | null;
  voCreateBy: string;
  voCreateId: string;
  voCreateTime: string;
}

export interface AdminAdjustBalanceRequest {
  userId: string;
  deltaAmount: string;
  reason: string;
}

export async function getBalanceByUserId(userId: string): Promise<UserBalanceVo> {
  const response = await apiGet<UserBalanceVo>(
    `/api/v1/Coin/GetBalanceByUserId?userId=${encodeURIComponent(String(userId))}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'coins.feedback.loadBalanceFailed');
  }

  return response.data;
}

export async function getTransactionsByUserId(params: {
  userId: string;
  pageIndex?: number;
  pageSize?: number;
  transactionType?: string;
  status?: string;
  businessType?: string;
  businessId?: string;
}): Promise<PagedResponse<CoinTransactionVo>> {
  const searchParams = new URLSearchParams({
    userId: String(params.userId),
    pageIndex: String(params.pageIndex ?? 1),
    pageSize: String(params.pageSize ?? 20),
  });

  if (params.transactionType) {
    searchParams.set('transactionType', params.transactionType);
  }

  if (params.status) {
    searchParams.set('status', params.status);
  }

  if (params.businessType) {
    searchParams.set('businessType', params.businessType);
  }

  if (params.businessId !== undefined) {
    searchParams.set('businessId', String(params.businessId));
  }

  const response = await apiGet<PagedResponse<CoinTransactionVo>>(
    `/api/v1/Coin/AdminGetTransactions?${searchParams.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'coins.feedback.loadTransactionsFailed');
  }

  return response.data;
}

export async function adminAdjustBalance(request: AdminAdjustBalanceRequest): Promise<void> {
  const response = await apiPost('/api/v1/Coin/AdminAdjustBalance', request, { withAuth: true });
  if (!response.ok) {
    throw createApiResponseError(response, 'coins.feedback.adjustFailed');
  }
}
