import type { TFunction } from 'i18next';
import { formatLocalizedRelativeTime } from '@radish/ui/intl';
import type { CoinAmount, CoinTransaction } from '@/api/coin';
import { getIntlLocale } from '../locales/language.ts';
import { formatDateTimeByTimeZone } from '../utils/dateTime.ts';

export type CoinNumericValue = CoinAmount | number | bigint | null | undefined;
export type CoinDisplayMode = 'carrot' | 'white';
export type TransactionDirection = 'in' | 'out';

const transactionTypeKeys: Record<string, string> = {
  SYSTEM_GRANT: 'pit.transactionType.SYSTEM_GRANT',
  LIKE_REWARD: 'pit.transactionType.LIKE_REWARD',
  COMMENT_REWARD: 'pit.transactionType.COMMENT_REWARD',
  HIGHLIGHT_REWARD: 'pit.transactionType.HIGHLIGHT_REWARD',
  GODCOMMENT_REWARD: 'pit.transactionType.GOD_COMMENT_REWARD',
  GODLIKE_REWARD: 'pit.transactionType.GOD_COMMENT_REWARD',
  SOFA_REWARD: 'pit.transactionType.SOFA_REWARD',
  TRANSFER: 'pit.transactionType.TRANSFER',
  TRANSFER_IN: 'pit.transactionType.TRANSFER_IN',
  TRANSFER_OUT: 'pit.transactionType.TRANSFER_OUT',
  TIP: 'pit.transactionType.TIP',
  CONSUME: 'pit.transactionType.CONSUME',
  PURCHASE: 'pit.transactionType.PURCHASE',
  REFUND: 'pit.transactionType.REFUND',
  PENALTY: 'pit.transactionType.PENALTY',
  ADMIN_ADJUST: 'pit.transactionType.ADMIN_ADJUST',
};

const transactionStatusKeys: Record<string, string> = {
  PENDING: 'pit.transactionStatus.PENDING',
  SUCCESS: 'pit.transactionStatus.SUCCESS',
  FAILED: 'pit.transactionStatus.FAILED',
  CANCELLED: 'pit.transactionStatus.CANCELLED',
};

const statisticsCategoryKeys: Record<string, string> = {
  IN_SYSTEM_GRANT: 'pit.statistics.category.IN_SYSTEM_GRANT',
  IN_LIKE_REWARD: 'pit.statistics.category.IN_LIKE_REWARD',
  IN_COMMENT_REWARD: 'pit.statistics.category.IN_COMMENT_REWARD',
  IN_GOD_COMMENT_REWARD: 'pit.statistics.category.IN_GOD_COMMENT_REWARD',
  IN_SOFA_REWARD: 'pit.statistics.category.IN_SOFA_REWARD',
  IN_TRANSFER: 'pit.statistics.category.IN_TRANSFER',
  IN_REFUND: 'pit.statistics.category.IN_REFUND',
  IN_OTHER: 'pit.statistics.category.IN_OTHER',
  OUT_TRANSFER: 'pit.statistics.category.OUT_TRANSFER',
  OUT_CONSUME: 'pit.statistics.category.OUT_CONSUME',
  OUT_PENALTY: 'pit.statistics.category.OUT_PENALTY',
  OUT_OTHER: 'pit.statistics.category.OUT_OTHER',
};

const securityLogTypeKeys: Record<string, string> = {
  password_verify: 'pit.security.log.type.password_verify',
  password_change: 'pit.security.log.type.password_change',
  password_set: 'pit.security.log.type.password_set',
  account_lock: 'pit.security.log.type.account_lock',
  account_unlock: 'pit.security.log.type.account_unlock',
  payment_password: 'pit.security.log.type.payment_password',
};

function normalizeInteger(value: CoinNumericValue): string | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const normalized = String(value).trim();
  return /^-?\d+$/.test(normalized) ? normalized : null;
}

function asBigInt(value: CoinNumericValue): bigint | null {
  const normalized = normalizeInteger(value);
  return normalized === null ? null : BigInt(normalized);
}

export function compareCoinValues(left: CoinNumericValue, right: CoinNumericValue): number {
  const leftInteger = asBigInt(left);
  const rightInteger = asBigInt(right);
  if (leftInteger !== null && rightInteger !== null) {
    return leftInteger === rightInteger ? 0 : leftInteger > rightInteger ? 1 : -1;
  }

  const difference = Number(left ?? 0) - Number(right ?? 0);
  return difference === 0 ? 0 : difference > 0 ? 1 : -1;
}

export function addCoinValues(...values: CoinNumericValue[]): string {
  const integers = values.map(asBigInt);
  if (integers.every((value): value is bigint => value !== null)) {
    return integers.reduce((total, value) => total + value, 0n).toString();
  }

  return String(values.reduce<number>((total, value) => total + Number(value ?? 0), 0));
}

export function subtractCoinValues(left: CoinNumericValue, right: CoinNumericValue): string {
  const leftInteger = asBigInt(left);
  const rightInteger = asBigInt(right);
  if (leftInteger !== null && rightInteger !== null) {
    return (leftInteger - rightInteger).toString();
  }
  return String(Number(left ?? 0) - Number(right ?? 0));
}

export function divideCoinValue(value: CoinNumericValue, divisor: number): string {
  const safeDivisor = Math.max(1, Math.trunc(divisor));
  const integer = asBigInt(value);
  if (integer !== null) {
    return (integer / BigInt(safeDivisor)).toString();
  }
  return String(Math.trunc(Number(value ?? 0) / safeDivisor));
}

export function absoluteCoinValue(value: CoinNumericValue): string {
  const integer = asBigInt(value);
  if (integer !== null) {
    return (integer < 0n ? -integer : integer).toString();
  }
  return String(Math.abs(Number(value ?? 0)));
}

export function formatCoinNumber(value: CoinNumericValue, language?: string): string {
  const integer = asBigInt(value);
  if (integer !== null) {
    return new Intl.NumberFormat(getIntlLocale(language)).format(integer);
  }

  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric)
    ? new Intl.NumberFormat(getIntlLocale(language)).format(numeric)
    : '-';
}

function formatWhiteRadishNumber(value: CoinNumericValue, language?: string): string {
  const integer = asBigInt(value);
  if (integer === null) {
    const numeric = Number(value ?? 0) / 1000;
    return Number.isFinite(numeric)
      ? new Intl.NumberFormat(getIntlLocale(language), {
        minimumFractionDigits: 3,
        maximumFractionDigits: 3,
      }).format(numeric)
      : '-';
  }

  const isNegative = integer < 0n;
  const absolute = isNegative ? -integer : integer;
  const whole = absolute / 1000n;
  const fraction = String(absolute % 1000n).padStart(3, '0');
  const decimalSeparator = new Intl.NumberFormat(getIntlLocale(language))
    .formatToParts(1.1)
    .find((part) => part.type === 'decimal')?.value ?? '.';
  return `${isNegative ? '-' : ''}${formatCoinNumber(whole, language)}${decimalSeparator}${fraction}`;
}

export function formatCoinAmount(
  value: CoinNumericValue,
  language: string,
  t: TFunction,
  mode: CoinDisplayMode = 'carrot',
  showUnit = true,
): string {
  const formatted = mode === 'white'
    ? formatWhiteRadishNumber(value, language)
    : formatCoinNumber(value, language);
  if (!showUnit) {
    return formatted;
  }

  return t(mode === 'white' ? 'pit.currency.whiteAmount' : 'pit.currency.carrotAmount', {
    value: formatted,
  });
}

export function toCoinChartNumber(value: CoinNumericValue): number {
  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric) ? numeric : 0;
}

export function formatCoinRatio(value: number, language?: string): string {
  return new Intl.NumberFormat(getIntlLocale(language), {
    style: 'percent',
    maximumFractionDigits: 1,
  }).format(Number.isFinite(value) ? value : 0);
}

export function formatCoinDateTime(
  value: string | number | Date | null | undefined,
  timeZone: string,
  language?: string,
  fallback = '-',
): string {
  return formatDateTimeByTimeZone(value, timeZone, fallback, getIntlLocale(language));
}

export function formatCoinRelativeDateTime(
  value: string | number | Date,
  timeZone: string,
  language?: string,
  now = Date.now(),
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  if (Math.abs(now - date.getTime()) < 7 * 24 * 60 * 60 * 1000) {
    return formatLocalizedRelativeTime(date, language, now);
  }
  return formatCoinDateTime(date, timeZone, language);
}

export function formatCoinChartDate(dateKey: string, language?: string): string {
  const date = new Date(`${dateKey}T12:00:00.000Z`);
  if (Number.isNaN(date.getTime())) {
    return dateKey;
  }
  return new Intl.DateTimeFormat(getIntlLocale(language), {
    timeZone: 'UTC',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

function formatStableToken(value: string, keys: Record<string, string>, t: TFunction): string {
  const normalized = value.trim();
  const key = keys[normalized];
  return key ? t(key) : normalized;
}

export function formatTransactionType(value: string, t: TFunction): string {
  return formatStableToken(value, transactionTypeKeys, t);
}

export function formatTransactionStatus(value: string, t: TFunction): string {
  return formatStableToken(value, transactionStatusKeys, t);
}

export function formatStatisticsCategory(value: string, t: TFunction): string {
  return formatStableToken(value, statisticsCategoryKeys, t);
}

export function formatSecurityLogType(value: string, t: TFunction): string {
  return formatStableToken(value, securityLogTypeKeys, t);
}

export function formatPasscodeStrength(level: number | null | undefined, t: TFunction): string {
  return t(`pit.security.strength.${level ?? 0}`, { defaultValue: t('pit.common.unknown') });
}

export function getTransactionStatusTone(status: string): 'warning' | 'success' | 'error' | 'default' {
  if (status === 'PENDING') return 'warning';
  if (status === 'SUCCESS') return 'success';
  if (status === 'FAILED') return 'error';
  return 'default';
}

export function getTransactionIcon(transactionType: string): string {
  const icons: Record<string, string> = {
    SYSTEM_GRANT: '🎁',
    LIKE_REWARD: '👍',
    COMMENT_REWARD: '💬',
    HIGHLIGHT_REWARD: '✨',
    GODCOMMENT_REWARD: '⭐',
    GODLIKE_REWARD: '⭐',
    SOFA_REWARD: '🛋️',
    TRANSFER: '💸',
    TRANSFER_IN: '📥',
    TRANSFER_OUT: '📤',
    TIP: '🎁',
    CONSUME: '🛒',
    PURCHASE: '🛒',
    REFUND: '↩️',
    PENALTY: '⚠️',
    ADMIN_ADJUST: '⚙️',
  };
  return icons[transactionType] ?? '💰';
}

export function resolveTransactionDirection(
  transaction: CoinTransaction,
  currentUserId: string,
): TransactionDirection {
  if (transaction.voToUserId === currentUserId) return 'in';
  if (transaction.voFromUserId === currentUserId) return 'out';
  if (transaction.voTransactionType === 'TRANSFER_IN') return 'in';
  if (transaction.voTransactionType === 'TRANSFER_OUT') return 'out';
  return compareCoinValues(transaction.voAmount, 0) >= 0 ? 'in' : 'out';
}

export function getSignedTransactionAmount(transaction: CoinTransaction, currentUserId: string): string {
  const absolute = absoluteCoinValue(transaction.voAmount);
  return resolveTransactionDirection(transaction, currentUserId) === 'in' ? absolute : `-${absolute}`;
}

export function isTransferTransaction(transaction: CoinTransaction): boolean {
  return ['TRANSFER', 'TRANSFER_IN', 'TRANSFER_OUT'].includes(transaction.voTransactionType);
}

export function validateTransferAmount(
  amount: number,
  balance: CoinAmount,
  t: TFunction,
): { isValid: boolean; message?: string } {
  if (!Number.isInteger(amount) || amount <= 0) {
    return { isValid: false, message: t('pit.transfer.validation.amountPositive') };
  }
  if (compareCoinValues(amount, balance) > 0) {
    return { isValid: false, message: t('pit.transfer.validation.insufficientBalance') };
  }
  if (amount > 100000) {
    return { isValid: false, message: t('pit.transfer.validation.singleLimit') };
  }
  return { isValid: true };
}

export function getSafeUserDisplayName(
  userName: string,
  isCurrentUser: boolean,
  currentUserLabel: string,
): string {
  if (isCurrentUser) {
    return currentUserLabel;
  }
  if (userName.length <= 2) {
    return userName;
  }

  const firstChar = userName.charAt(0);
  const lastChar = userName.charAt(userName.length - 1);
  const middleStars = '*'.repeat(Math.min(userName.length - 2, 3));
  return `${firstChar}${middleStars}${lastChar}`;
}
