export {
  absoluteCoinValue,
  addCoinValues,
  compareCoinValues,
  divideCoinValue,
  formatCoinAmount,
  formatCoinChartDate,
  formatCoinDateTime,
  formatCoinNumber,
  formatCoinRatio,
  formatCoinRelativeDateTime,
  formatPasscodeStrength,
  formatSecurityLogType,
  formatStatisticsCategory,
  formatTransactionStatus,
  formatTransactionType,
  getSafeUserDisplayName,
  getSignedTransactionAmount,
  getTransactionIcon,
  getTransactionStatusTone,
  isTransferTransaction,
  resolveTransactionDirection,
  subtractCoinValues,
  toCoinChartNumber,
  validateTransferAmount,
} from '@/coin/coinPresentation';

export const debounce = <TArgs extends unknown[], TResult>(
  func: (...args: TArgs) => TResult,
  delay: number,
): ((...args: TArgs) => void) => {
  let timeoutId: ReturnType<typeof setTimeout>;

  return (...args: TArgs) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => func(...args), delay);
  };
};

export const throttle = <TArgs extends unknown[], TResult>(
  func: (...args: TArgs) => TResult,
  delay: number,
): ((...args: TArgs) => void) => {
  let lastCall = 0;

  return (...args: TArgs) => {
    const now = Date.now();
    if (now - lastCall >= delay) {
      lastCall = now;
      func(...args);
    }
  };
};
