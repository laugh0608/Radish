/**
 * 验证邮箱格式
 * @param email 邮箱地址
 * @returns 是否为有效邮箱
 */
export function isEmail(email: string): boolean {
  const regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return regex.test(email);
}

/**
 * 验证中国大陆手机号
 * @param phone 手机号
 * @returns 是否为有效手机号
 */
export function isPhone(phone: string): boolean {
  const regex = /^1[3-9]\d{9}$/;
  return regex.test(phone);
}

/**
 * 验证 URL 格式
 * @param url URL 地址
 * @returns 是否为有效 URL
 */
export function isUrl(url: string): boolean {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}

/**
 * 验证身份证号（简单验证）
 * @param idCard 身份证号
 * @returns 是否为有效身份证号
 */
export function isIdCard(idCard: string): boolean {
  const regex = /(^\d{15}$)|(^\d{18}$)|(^\d{17}(\d|X|x)$)/;
  return regex.test(idCard);
}

/**
 * 验证密码强度
 * @param password 密码
 * @returns 密码强度等级 (0: 弱, 1: 中, 2: 强)
 */
export function getPasswordStrength(password: string): number {
  if (password.length < 6) return 0;

  let strength = 0;

  // 包含小写字母
  if (/[a-z]/.test(password)) strength++;

  // 包含大写字母
  if (/[A-Z]/.test(password)) strength++;

  // 包含数字
  if (/\d/.test(password)) strength++;

  // 包含特殊字符
  if (/[^a-zA-Z0-9]/.test(password)) strength++;

  // 长度大于 12
  if (password.length >= 12) strength++;

  if (strength <= 2) return 0; // 弱
  if (strength <= 3) return 1; // 中
  return 2; // 强
}
