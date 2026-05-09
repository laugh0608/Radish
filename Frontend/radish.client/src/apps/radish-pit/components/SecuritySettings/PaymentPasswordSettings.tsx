import { useState } from 'react';
import { log } from '@/utils/logger';
import { paymentPasswordApi } from '@/api/paymentPassword';
import { PasscodeInput } from '@radish/ui';
import {
  getPaymentPasscodeStrength,
  getPaymentPasscodeValidationMessage,
  isPaymentPasscodeFormatValid,
  isPaymentPasscodeValid,
  isSequentialPaymentPasscode
} from '@/utils/paymentPasscode';
import { toast } from '@radish/ui/toast';
import type { SecurityStatus } from '../../types';
import styles from './PaymentPasswordSettings.module.css';

interface PaymentPasswordSettingsProps {
  status: SecurityStatus | null;
  onUpdate: () => void;
}

type PasswordAction = 'set' | 'change' | 'none';

interface PasswordFormData {
  currentPassword?: string;
  newPassword: string;
  confirmPassword: string;
}

/**
 * 支付密码设置组件
 */
export const PaymentPasswordSettings = ({ status, onUpdate }: PaymentPasswordSettingsProps) => {
  const [action, setAction] = useState<PasswordAction>('none');
  const [formData, setFormData] = useState<PasswordFormData>({
    newPassword: '',
    confirmPassword: ''
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [passwordStrength, setPasswordStrength] = useState(0);
  const requiresPasscodeUpgrade = Boolean(status?.requiresPasscodeUpgrade);
  const hasUsablePasscode = Boolean(status?.hasPaymentPassword && !requiresPasscodeUpgrade);

  const handleInputChange = (field: keyof PasswordFormData, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));

    // 清除对应字段的错误
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }

    // 实时检查口令强度
    if (field === 'newPassword') {
      const strength = getPaymentPasscodeStrength(value);
      setPasswordStrength(strength);
    }
  };

  const getPasswordStrengthText = (strength: number): string => {
    switch (strength) {
      case 0: return '未完成';
      case 1: return '无效';
      case 2: return '较弱';
      case 3: return '一般';
      case 4: return '稳妥';
      case 5: return '较强';
      default: return '未知';
    }
  };

  const getPasswordStrengthColor = (strength: number): string => {
    if (strength <= 1) return 'critical';
    if (strength <= 2) return 'low';
    if (strength <= 3) return 'medium';
    return 'high';
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    // 验证当前口令（修改口令时）
    if (action === 'change') {
      const currentPasswordError = getPaymentPasscodeValidationMessage(formData.currentPassword ?? '', {
        requiredMessage: '请输入当前支付口令',
        allowRepeatedDigits: true,
      });
      if (currentPasswordError) {
        newErrors.currentPassword = currentPasswordError;
      }
    }

    // 验证新口令
    const newPasswordError = getPaymentPasscodeValidationMessage(formData.newPassword);
    if (newPasswordError) {
      newErrors.newPassword = newPasswordError;
    }

    // 验证确认口令
    if (!formData.confirmPassword.trim()) {
      newErrors.confirmPassword = '请确认新支付口令';
    } else if (formData.newPassword !== formData.confirmPassword) {
      newErrors.confirmPassword = '两次输入的支付口令不一致';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      log.debug('PaymentPasswordSettings', '表单验证失败', errors);
      return;
    }

    try {
      setLoading(true);

      if (action === 'set') {
        log.debug('PaymentPasswordSettings', '设置支付口令');
        await paymentPasswordApi.setPaymentPassword({
          newPassword: formData.newPassword,
          confirmPassword: formData.confirmPassword
        });
      } else if (action === 'change') {
        log.debug('PaymentPasswordSettings', '修改支付口令');
        await paymentPasswordApi.changePaymentPassword({
          currentPassword: formData.currentPassword!,
          newPassword: formData.newPassword,
          confirmPassword: formData.confirmPassword
        });
      }

      // 重置表单
      setAction('none');
      setFormData({ newPassword: '', confirmPassword: '' });
      setPasswordStrength(0);

      // 通知父组件更新状态
      onUpdate();

      toast.success(
        action === 'set'
          ? requiresPasscodeUpgrade
            ? '支付口令重置成功'
            : '支付口令设置成功'
          : '支付口令修改成功'
      );
    } catch (error) {
      log.error('支付口令操作失败:', error);
      toast.error(error instanceof Error ? error.message : '操作失败，请稍后重试');
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    setAction('none');
    setFormData({ newPassword: '', confirmPassword: '' });
    setErrors({});
    setPasswordStrength(0);
  };

  if (action === 'none') {
    return (
      <div className={styles.container}>
        {/* 当前状态 */}
        <div className={styles.statusCard}>
          <div className={styles.statusHeader}>
            <h3 className={styles.statusTitle}>
              <span className={styles.statusIcon}>🔑</span>
              支付口令状态
            </h3>
          </div>

          <div className={styles.statusContent}>
            <div className={`${styles.statusIndicator} ${
              requiresPasscodeUpgrade ? styles.legacy : status?.hasPaymentPassword ? styles.set : styles.notSet
            }`}>
              <div className={styles.indicatorIcon}>
                {requiresPasscodeUpgrade ? '♻️' : status?.hasPaymentPassword ? '✅' : '❌'}
              </div>
              <div className={styles.indicatorText}>
                <div className={styles.indicatorTitle}>
                  {requiresPasscodeUpgrade ? '旧支付口令已废弃' : status?.hasPaymentPassword ? '已设置支付口令' : '未设置支付口令'}
                </div>
                <div className={styles.indicatorDescription}>
                  {requiresPasscodeUpgrade
                    ? '旧支付口令不再支持商城购买和萝卜转移，请直接重置为新的 6 位数字支付口令'
                    : status?.hasPaymentPassword
                    ? '您的转移和购买操作会在支付口令验证后执行'
                    : '设置支付口令可以保护您的萝卜资产'}
                </div>
              </div>
            </div>

            {requiresPasscodeUpgrade && (
              <div className={styles.upgradeNotice}>
                <div className={styles.upgradeTitle}>需要立即重置</div>
                <div className={styles.upgradeText}>
                  旧支付口令已经废弃，不能再用于商城购买、萝卜转移或口令修改。请直接设置新的 6 位数字支付口令。
                </div>
              </div>
            )}

            {status?.hasPaymentPassword && (
              <div className={styles.passwordInfo}>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>口令强度</div>
                  <div className={styles.infoValue}>
                    <span className={`${styles.strengthBadge} ${styles[getPasswordStrengthColor(status.strengthLevel ?? 0)]}`}>
                      {status.strengthLevelDisplay || '未知'}
                    </span>
                  </div>
                </div>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>最后修改</div>
                  <div className={styles.infoValue}>
                    {status.lastPasswordChangeTimeDisplay || '未知'}
                  </div>
                </div>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>失败尝试</div>
                  <div className={styles.infoValue}>
                    {status.failedAttempts} 次
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className={styles.statusActions}>
            {hasUsablePasscode ? (
              <button
                className={styles.primaryButton}
                onClick={() => setAction('change')}
              >
                修改支付口令
              </button>
            ) : (
              <button
                className={styles.primaryButton}
                onClick={() => setAction('set')}
              >
                {requiresPasscodeUpgrade ? '重置为新口令' : '设置支付口令'}
              </button>
            )}
          </div>
        </div>

        {/* 安全提示 */}
        <div className={styles.securityTips}>
          <h4 className={styles.tipsTitle}>
            <span className={styles.tipsIcon}>💡</span>
            安全提示
          </h4>
          <ul className={styles.tipsList}>
            <li>支付口令用于保护您的萝卜转移和商城消费</li>
            <li>支付口令必须为 6 位数字，且不能为 6 个相同数字</li>
            <li>建议避免使用连续数字组合，例如 123456 或 654321</li>
            <li>支付口令应与登录密码不同，并定期更换</li>
            <li>不要在公共场所输入支付口令</li>
          </ul>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.formCard}>
        <div className={styles.formHeader}>
          <h3 className={styles.formTitle}>
            <span className={styles.formIcon}>🔑</span>
            {action === 'set' ? '设置支付口令' : '修改支付口令'}
          </h3>
          <p className={styles.formSubtitle}>
            {action === 'set'
              ? requiresPasscodeUpgrade
                ? '旧支付口令已废弃，请直接设置新的 6 位数字支付口令'
                : '设置 6 位数字支付口令以保护您的萝卜转移和购买操作'
              : '输入当前支付口令，并设置新的 6 位数字支付口令'}
          </p>
        </div>

        {requiresPasscodeUpgrade && action === 'set' && (
          <div className={styles.upgradeNotice}>
            <div className={styles.upgradeTitle}>旧口令不再可用</div>
            <div className={styles.upgradeText}>
              本次提交会直接将您的旧支付口令重置为新的 6 位数字支付口令，无需再输入旧口令。
            </div>
          </div>
        )}

        <form className={styles.form} onSubmit={handleSubmit}>
          {/* 当前口令（修改时需要） */}
          {action === 'change' && (
            <div className={styles.formGroup}>
              <label className={styles.label}>
                当前支付口令 <span className={styles.required}>*</span>
              </label>
              <PasscodeInput
                id="payment-passcode-current"
                value={formData.currentPassword || ''}
                onChange={(value) => handleInputChange('currentPassword', value)}
                autoComplete="current-password"
                error={errors.currentPassword}
                helperText="请输入当前 6 位支付口令"
                disabled={loading}
              />
            </div>
          )}

          {/* 新口令 */}
          <div className={styles.formGroup}>
              <label className={styles.label}>
                新支付口令 <span className={styles.required}>*</span>
              </label>
            <PasscodeInput
              id="payment-passcode-new"
              value={formData.newPassword}
              onChange={(value) => handleInputChange('newPassword', value)}
              autoComplete="new-password"
              error={errors.newPassword}
              helperText="请输入新的 6 位数字支付口令"
              disabled={loading}
              autoFocus={action === 'set'}
            />
            {formData.newPassword && (
              <div className={styles.passwordStrength}>
                <div className={styles.strengthLabel}>口令强度：</div>
                <div className={`${styles.strengthValue} ${styles[getPasswordStrengthColor(passwordStrength)]}`}>
                  {getPasswordStrengthText(passwordStrength)}
                </div>
                <div className={styles.strengthBar}>
                  <div
                    className={`${styles.strengthProgress} ${styles[getPasswordStrengthColor(passwordStrength)]}`}
                    style={{ width: `${(passwordStrength / 5) * 100}%` }}
                  ></div>
                </div>
              </div>
            )}
          </div>

          {/* 确认口令 */}
          <div className={styles.formGroup}>
              <label className={styles.label}>
                确认新支付口令 <span className={styles.required}>*</span>
              </label>
            <PasscodeInput
              id="payment-passcode-confirm"
              value={formData.confirmPassword}
              onChange={(value) => handleInputChange('confirmPassword', value)}
              autoComplete="new-password"
              error={errors.confirmPassword}
              helperText="请再次输入新的 6 位数字支付口令"
              disabled={loading}
            />
          </div>

          {/* 口令要求 */}
          <div className={styles.passwordRequirements}>
            <h5 className={styles.requirementsTitle}>口令要求：</h5>
            <ul className={styles.requirementsList}>
              <li className={isPaymentPasscodeFormatValid(formData.newPassword) ? styles.met : ''}>
                必须为 6 位数字
              </li>
              <li className={isPaymentPasscodeValid(formData.newPassword) ? styles.met : ''}>
                不能为 6 个相同数字
              </li>
              <li className={isPaymentPasscodeFormatValid(formData.newPassword) && !isSequentialPaymentPasscode(formData.newPassword) ? styles.met : ''}>
                避免使用连续数字组合
              </li>
            </ul>
          </div>

          {/* 操作按钮 */}
          <div className={styles.formActions}>
            <button
              type="button"
              className={styles.cancelButton}
              onClick={handleCancel}
              disabled={loading}
            >
              取消
            </button>
            <button
              type="submit"
              className={styles.submitButton}
              disabled={
                loading ||
                !isPaymentPasscodeValid(formData.newPassword) ||
                formData.newPassword !== formData.confirmPassword ||
                (action === 'change' && Boolean(getPaymentPasscodeValidationMessage(formData.currentPassword ?? '', {
                  requiredMessage: '请输入当前支付口令',
                  allowRepeatedDigits: true,
                })))
              }
            >
              {loading ? (
                <>
                  <div className={styles.buttonSpinner}></div>
                  处理中...
                </>
              ) : (
                action === 'set'
                  ? requiresPasscodeUpgrade ? '重置口令' : '设置口令'
                  : '修改口令'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
