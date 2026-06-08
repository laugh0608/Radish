import { apiGet, apiPost, type PagedResponse } from '@radish/http';

export interface UserBalanceVo {
  voUserId: string;
  voBalance: number;
  voBalanceDisplay: string;
  voFrozenBalance: number;
  voFrozenBalanceDisplay: string;
  voTotalEarned: number;
  voTotalSpent: number;
  voTotalTransferredIn: number;
  voTotalTransferredOut: number;
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
  voAmount: number;
  voAmountDisplay: string;
  voFee: number;
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
  deltaAmount: number;
  reason: string;
}

export async function getBalanceByUserId(userId: string): Promise<UserBalanceVo> {
  const response = await apiGet<UserBalanceVo>(
    `/api/v1/Coin/GetBalanceByUserId?userId=${encodeURIComponent(String(userId))}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户余额失败');
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
    throw new Error(response.message || '获取用户萝卜币流水失败');
  }

  return response.data;
}

export async function adminAdjustBalance(request: AdminAdjustBalanceRequest): Promise<void> {
  const response = await apiPost('/api/v1/Coin/AdminAdjustBalance', request, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '调整胡萝卜余额失败');
  }
}
