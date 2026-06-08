/**
 * 支付密码相关的 API 调用
 */

import { apiGet, apiPost, configureApiClient, type PagedResponse } from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';
import type { LongId } from './user';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 支付密码状态（后端ViewModel使用Vo前缀）
 */
export interface PaymentPasswordStatus {
  voId: number;
  voUserId: LongId;
  voFailedAttempts: number;
  voIsLocked: boolean;
  voLockedUntil?: string;
  voLockedRemainingMinutes: number;
  voLastUsedTime?: string;
  voLastUsedTimeDisplay: string;
  voLastModifiedTime?: string;
  voLastModifiedTimeDisplay: string;
  voStrengthLevel: number;
  voStrengthLevelDisplay: string;
  voPasscodeVersion?: number;
  voIsLegacyPasscode: boolean;
  voRequiresPasscodeUpgrade: boolean;
  voIsEnabled: boolean;
  voHasPaymentPassword: boolean;
  voSecurityStatus: string;
  voSecuritySuggestions: string[];
  voCreatedAt: string;
  voCreatedAtDisplay: string;
}

/**
 * 密码强度信息
 */
export interface PasswordStrength {
  voLevel: number;
  voDisplay: string;
  voIsValid: boolean;
}

/**
 * 设置支付密码请求
 */
export interface SetPaymentPasswordRequest {
  newPassword: string;
  confirmPassword: string;
}

/**
 * 修改支付密码请求
 */
export interface ChangePaymentPasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

/**
 * 验证支付密码请求
 */
export interface VerifyPaymentPasswordRequest {
  password: string;
}

/**
 * 支付密码验证结果
 */
export interface PaymentPasswordVerifyResult {
  isSuccess: boolean;
  errorMessage: string;
  errorCode?: string;
  remainingAttempts?: number;
  isLocked?: boolean;
  lockedRemainingMinutes?: number;
  requiresPasscodeUpgrade?: boolean;
}

/**
 * 支付密码安全日志
 */
export interface PaymentPasswordSecurityLog {
  voId: number;
  voType: 'password_verify' | 'password_change' | 'password_set' | 'account_lock' | 'account_unlock' | 'payment_password';
  voAction: string;
  voResult: 'success' | 'failed';
  voIpAddress?: string;
  voUserAgent?: string;
  voCreatedAt: string;
}

/**
 * 获取支付密码状态
 * @param t i18n 翻译函数（可选）
 * @returns 支付密码状态（如果未设置则返回null）
 */
export async function getPaymentPasswordStatus(t?: TFunction) {
  void t;
  const response = await apiGet<PaymentPasswordStatus | null>(
    '/api/v1/PaymentPassword/GetStatus',
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '获取支付口令状态失败');
  }

  return response.data;
}

/**
 * 设置支付密码
 * @param request 设置请求
 * @param t i18n 翻译函数（可选）
 * @returns 是否成功
 */
export async function setPaymentPassword(request: SetPaymentPasswordRequest, t?: TFunction) {
  void t;
  const response = await apiPost<boolean>(
    '/api/v1/PaymentPassword/SetPassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '设置支付口令失败');
  }

  return response.data ?? false;
}

/**
 * 修改支付密码
 * @param request 修改请求
 * @param t i18n 翻译函数（可选）
 * @returns 是否成功
 */
export async function changePaymentPassword(request: ChangePaymentPasswordRequest, t?: TFunction) {
  void t;
  const response = await apiPost<boolean>(
    '/api/v1/PaymentPassword/ChangePassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '修改支付口令失败');
  }

  return response.data ?? false;
}

/**
 * 验证支付密码
 * @param request 验证请求
 * @param t i18n 翻译函数（可选）
 * @returns 验证结果
 */
export async function verifyPaymentPassword(request: VerifyPaymentPasswordRequest, t?: TFunction) {
  void t;
  const response = await apiPost<PaymentPasswordVerifyResult>(
    '/api/v1/PaymentPassword/VerifyPassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '验证支付口令失败');
  }

  return response.data;
}

/**
 * 检查密码强度
 * @param password 密码
 * @param t i18n 翻译函数（可选）
 * @returns 密码强度信息
 */
export async function checkPasswordStrength(password: string, t?: TFunction) {
  void t;
  const response = await apiPost<PasswordStrength>(
    '/api/v1/PaymentPassword/CheckStrength',
    password,
    {
      withAuth: true,
      headers: {
        'Content-Type': 'application/json',
      },
    }
  );

  if (!response.ok) {
    throw new Error(response.message || '检查密码强度失败');
  }

  return response.data;
}

/**
 * 获取当前用户支付密码安全日志
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量
 * @param t i18n 翻译函数（可选）
 * @returns 分页安全日志
 */
export async function getSecurityLogs(
  pageIndex: number = 1,
  pageSize: number = 20,
  t?: TFunction
): Promise<PagedResponse<PaymentPasswordSecurityLog>> {
  void t;
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString(),
  });

  const response = await apiGet<PagedResponse<PaymentPasswordSecurityLog>>(
    `/api/v1/PaymentPassword/GetSecurityLogs?${params.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取安全日志失败');
  }

  return response.data;
}

/**
 * 获取安全建议
 * @param t i18n 翻译函数（可选）
 * @returns 安全建议列表
 */
export async function getSecuritySuggestions(t?: TFunction) {
  void t;
  const response = await apiGet<string[]>(
    '/api/v1/PaymentPassword/GetSecuritySuggestions',
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '获取安全建议失败');
  }

  return response.data;
}

/**
 * 支付密码 API 对象（用于统一导入）
 */
export const paymentPasswordApi = {
  getPaymentPasswordStatus,
  setPaymentPassword,
  changePaymentPassword,
  verifyPaymentPassword,
  checkPasswordStrength,
  getSecuritySuggestions,
  getSecurityLogs,
};
