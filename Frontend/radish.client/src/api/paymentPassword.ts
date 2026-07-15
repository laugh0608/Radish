/**
 * 支付口令相关的 API 调用。
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

export interface PaymentPasswordStatus {
  voId: LongId;
  voUserId: LongId;
  voFailedAttempts: number;
  voIsLocked: boolean;
  voLockedUntil?: string | null;
  voLockedRemainingMinutes: number;
  voLastUsedTime?: string | null;
  voLastUsedTimeDisplay: string;
  voLastModifiedTime?: string | null;
  voLastModifiedTimeDisplay: string;
  voStrengthLevel: number;
  voStrengthLevelDisplay: string;
  voPasscodeVersion?: number | null;
  voIsLegacyPasscode: boolean;
  voRequiresPasscodeUpgrade: boolean;
  voIsEnabled: boolean;
  voHasPaymentPassword: boolean;
  voSecurityStatus: string;
  voSecuritySuggestions: string[];
  voCreatedAt: string;
  voCreatedAtDisplay: string;
}

export interface PasswordStrength {
  voLevel: number;
  voDisplay: string;
  voIsValid: boolean;
}

export interface SetPaymentPasswordRequest {
  newPassword: string;
  confirmPassword: string;
}

export interface ChangePaymentPasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface VerifyPaymentPasswordRequest {
  password: string;
}

export interface PaymentPasswordVerifyResult {
  isSuccess: boolean;
  errorMessage?: string | null;
  errorCode?: string;
  messageKey?: string;
  remainingAttempts?: number;
  isLocked?: boolean;
  lockedRemainingMinutes?: number;
  requiresPasscodeUpgrade?: boolean;
}

export interface PaymentPasswordSecurityLog {
  voId: LongId;
  voType: 'password_verify' | 'password_change' | 'password_set' | 'account_lock' | 'account_unlock' | 'payment_password' | string;
  /** 后端兼容展示字段；client 只按稳定 voType 本地化。 */
  voAction: string;
  voResult: 'success' | 'failed' | string;
  voIpAddress?: string | null;
  voUserAgent?: string | null;
  voCreatedAt: string;
}

function createStructuredError<T>(response: ParsedApiResponse<T>, fallbackMessage: string) {
  return createApiResponseError(
    response.messageKey ? { ...response, message: undefined } : response,
    fallbackMessage,
  );
}

function ensureRequiredResponse<T>(response: ParsedApiResponse<T>, fallbackMessage: string): T {
  if (!response.ok || response.data === undefined) {
    throw createStructuredError(response, fallbackMessage);
  }
  return response.data;
}

export async function getPaymentPasswordStatus(t: TFunction): Promise<PaymentPasswordStatus | null> {
  const response = await apiGet<PaymentPasswordStatus | null>(
    '/api/v1/PaymentPassword/GetStatus',
    { withAuth: true },
  );
  if (!response.ok) {
    throw createStructuredError(response, t('pit.api.securityStatusFailed'));
  }
  return response.data ?? null;
}

export async function setPaymentPassword(request: SetPaymentPasswordRequest, t: TFunction): Promise<boolean> {
  const response = await apiPost<boolean>('/api/v1/PaymentPassword/SetPassword', request, { withAuth: true });
  return ensureRequiredResponse(response, t('pit.api.passcodeSetFailed'));
}

export async function changePaymentPassword(request: ChangePaymentPasswordRequest, t: TFunction): Promise<boolean> {
  const response = await apiPost<boolean>('/api/v1/PaymentPassword/ChangePassword', request, { withAuth: true });
  return ensureRequiredResponse(response, t('pit.api.passcodeChangeFailed'));
}

export async function verifyPaymentPassword(
  request: VerifyPaymentPasswordRequest,
  t: TFunction,
): Promise<PaymentPasswordVerifyResult> {
  const response = await apiPost<PaymentPasswordVerifyResult>(
    '/api/v1/PaymentPassword/VerifyPassword',
    request,
    { withAuth: true },
  );
  return ensureRequiredResponse(response, t('pit.api.passcodeVerifyFailed'));
}

export async function checkPasswordStrength(password: string, t: TFunction): Promise<PasswordStrength> {
  const response = await apiPost<PasswordStrength>(
    '/api/v1/PaymentPassword/CheckStrength',
    password,
    {
      withAuth: true,
      headers: { 'Content-Type': 'application/json' },
    },
  );
  return ensureRequiredResponse(response, t('pit.api.passcodeStrengthFailed'));
}

export async function getSecurityLogs(
  pageIndex: number,
  pageSize: number,
  t: TFunction,
): Promise<PagedResponse<PaymentPasswordSecurityLog>> {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString(),
  });
  const response = await apiGet<PagedResponse<PaymentPasswordSecurityLog>>(
    `/api/v1/PaymentPassword/GetSecurityLogs?${params.toString()}`,
    { withAuth: true },
  );
  return ensureRequiredResponse(response, t('pit.api.securityLogsFailed'));
}

export async function getSecuritySuggestions(t: TFunction): Promise<string[]> {
  const response = await apiGet<string[]>('/api/v1/PaymentPassword/GetSecuritySuggestions', { withAuth: true });
  return ensureRequiredResponse(response, t('pit.api.securitySuggestionsFailed'));
}

export const paymentPasswordApi = {
  getPaymentPasswordStatus,
  setPaymentPassword,
  changePaymentPassword,
  verifyPaymentPassword,
  checkPasswordStrength,
  getSecurityLogs,
  getSecuritySuggestions,
};
