/**
 * 支付密码相关的 API 调用
 */

import { apiGet, apiPost, configureApiClient } from '@radish/ui';
import type { TFunction } from 'i18next';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

/**
 * 支付密码状态（后端ViewModel使用Vo前缀）
 */
export interface PaymentPasswordStatus {
  voId: number;
  voUserId: number;
  voFailedAttempts: number;
  voIsLocked: boolean;
  voLockedUntil?: string;
  voLastPasswordChangeTime?: string;
  voCreateTime: string;
  voUpdateTime?: string;
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
  password: string;
  confirmPassword: string;
}

/**
 * 修改支付密码请求
 */
export interface ChangePaymentPasswordRequest {
  oldPassword: string;
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
  remainingAttempts?: number;
  isLocked?: boolean;
  lockedUntil?: string;
}

/**
 * 获取支付密码状态
 * @param t i18n 翻译函数（可选）
 * @returns 支付密码状态（如果未设置则返回null）
 */
export async function getPaymentPasswordStatus(t?: TFunction) {
  const response = await apiGet<PaymentPasswordStatus | null>(
    '/api/v1/PaymentPassword/GetStatus',
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '获取支付密码状态失败');
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
  const response = await apiPost<boolean>(
    '/api/v1/PaymentPassword/SetPassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '设置支付密码失败');
  }

  return response.data;
}

/**
 * 修改支付密码
 * @param request 修改请求
 * @param t i18n 翻译函数（可选）
 * @returns 是否成功
 */
export async function changePaymentPassword(request: ChangePaymentPasswordRequest, t?: TFunction) {
  const response = await apiPost<boolean>(
    '/api/v1/PaymentPassword/ChangePassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '修改支付密码失败');
  }

  return response.data;
}

/**
 * 验证支付密码
 * @param request 验证请求
 * @param t i18n 翻译函数（可选）
 * @returns 验证结果
 */
export async function verifyPaymentPassword(request: VerifyPaymentPasswordRequest, t?: TFunction) {
  const response = await apiPost<PaymentPasswordVerifyResult>(
    '/api/v1/PaymentPassword/VerifyPassword',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '验证支付密码失败');
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
  const response = await apiPost<PasswordStrength>(
    '/api/v1/PaymentPassword/CheckStrength',
    JSON.stringify(password),
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
 * 获取安全建议
 * @param t i18n 翻译函数（可选）
 * @returns 安全建议列表
 */
export async function getSecuritySuggestions(t?: TFunction) {
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
};
