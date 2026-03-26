import { apiGet, apiPost } from '@radish/http';

export interface UserBalanceVo {
  voUserId: number;
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

export interface AdminAdjustBalanceRequest {
  userId: number;
  deltaAmount: number;
  reason: string;
}

export async function getBalanceByUserId(userId: number): Promise<UserBalanceVo> {
  const response = await apiGet<UserBalanceVo>(`/api/v1/Coin/GetBalanceByUserId?userId=${userId}`, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户余额失败');
  }

  return response.data;
}

export async function adminAdjustBalance(request: AdminAdjustBalanceRequest): Promise<void> {
  const response = await apiPost('/api/v1/Coin/AdminAdjustBalance', request, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '调整胡萝卜余额失败');
  }
}
