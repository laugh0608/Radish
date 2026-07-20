import type { CoinAmount, CoinStatistics } from '@/api/coin';
import type { LongId } from '@/api/user';

export type TabType = 'overview' | 'transfer' | 'history' | 'security' | 'statistics';

export interface AccountStats {
  totalEarned: CoinAmount;
  totalSpent: CoinAmount;
  totalTransferredIn: CoinAmount;
  totalTransferredOut: CoinAmount;
  recentTransactionCount: number;
}

export interface TransferFormData {
  recipientId: LongId;
  recipientName: string;
  amount: number;
  note?: string;
  paymentPassword: string;
  idempotencyKey?: string;
}

export interface TransferResult {
  success: boolean;
  transactionNo?: string;
  message: string;
  requiresPasscodeUpgrade?: boolean;
}

export interface SecurityStatus {
  hasPaymentPassword: boolean;
  isLegacyPasscode?: boolean;
  requiresPasscodeUpgrade?: boolean;
  lastPasswordChangeTime?: string;
  lastPasswordUsedTime?: string;
  failedAttempts: number;
  isLocked: boolean;
  lockedUntil?: string;
  lockedRemainingMinutes?: number;
  strengthLevel?: number;
}

export type StatisticsData = CoinStatistics;
