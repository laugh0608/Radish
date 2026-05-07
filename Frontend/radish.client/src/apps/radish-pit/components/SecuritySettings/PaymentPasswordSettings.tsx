import { useState } from 'react';
import { log } from '@/utils/logger';
import { paymentPasswordApi } from '@/api/paymentPassword';
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

  const handleInputChange = (field: keyof PasswordFormData, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));

    // 清除对应字段的错误
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }

    // 实时检查密码强度
    if (field === 'newPassword') {
      const strength = checkPasswordStrength(value);
      setPasswordStrength(strength);
    }
  };

  const checkPasswordStrength = (password: string): number => {
    if (!password) return 0;

    let score = 0;

    // 长度检查
    if (password.length >= 6) score += 1;
    if (password.length >= 8) score += 1;

    // 字符类型检查
    if (/[a-z]/.test(password)) score += 1; // 小写字母
    if (/[A-Z]/.test(password)) score += 1; // 大写字母
    if (/\d/.test(password)) score += 1;    // 数字
    if (/[!@#$%^&*(),.?":{}|<>]/.test(password)) score += 1; // 特殊字符

    return Math.min(5, score);
  };

  const getPasswordStrengthText = (strength: number): string => {
    switch (strength) {
      case 0: return '无效';
      case 1: return '很弱';
      case 2: return '弱';
      case 3: return '中等';
      case 4: return '强';
      case 5: return '很强';
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

    // 验证当前密码（修改密码时）
    if (action === 'change' && !formData.currentPassword?.trim()) {
      newErrors.currentPassword = '请输入当前支付密码';
    }

    // 验证新密码
    if (!formData.newPassword.trim()) {
      newErrors.newPassword = '请输入新密码';
    } else if (formData.newPassword.length < 6) {
      newErrors.newPassword = '密码长度不能少于6位';
    } else if (formData.newPassword.length > 20) {
      newErrors.newPassword = '密码长度不能超过20位';
    } else if (!/\d/.test(formData.newPassword)) {
      newErrors.newPassword = '密码必须包含至少一个数字';
    }

    // 验证确认密码
    if (!formData.confirmPassword.trim()) {
      newErrors.confirmPassword = '请确认新密码';
    } else if (formData.newPassword !== formData.confirmPassword) {
      newErrors.confirmPassword = '两次输入的密码不一致';
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
        log.debug('PaymentPasswordSettings', '设置支付密码');
        await paymentPasswordApi.setPaymentPassword({
          newPassword: formData.newPassword,
          confirmPassword: formData.confirmPassword
        });
      } else if (action === 'change') {
        log.debug('PaymentPasswordSettings', '修改支付密码');
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

      toast.success(action === 'set' ? '支付密码设置成功' : '支付密码修改成功');
    } catch (error) {
      log.error('支付密码操作失败:', error);
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
              支付密码状态
            </h3>
          </div>

          <div className={styles.statusContent}>
            <div className={`${styles.statusIndicator} ${
              status?.hasPaymentPassword ? styles.set : styles.notSet
            }`}>
              <div className={styles.indicatorIcon}>
                {status?.hasPaymentPassword ? '✅' : '❌'}
              </div>
              <div className={styles.indicatorText}>
                <div className={styles.indicatorTitle}>
                  {status?.hasPaymentPassword ? '已设置支付密码' : '未设置支付密码'}
                </div>
                <div className={styles.indicatorDescription}>
                  {status?.hasPaymentPassword
                    ? '您的转移操作受到支付密码保护'
                    : '设置支付密码可以保护您的萝卜安全'}
                </div>
              </div>
            </div>

            {status?.hasPaymentPassword && (
              <div className={styles.passwordInfo}>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>密码强度</div>
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
            {status?.hasPaymentPassword ? (
              <button
                className={styles.primaryButton}
                onClick={() => setAction('change')}
              >
                修改支付密码
              </button>
            ) : (
              <button
                className={styles.primaryButton}
                onClick={() => setAction('set')}
              >
                设置支付密码
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
            <li>支付密码用于保护您的萝卜转移操作</li>
            <li>支付密码应与登录密码不同，增强安全性</li>
            <li>建议使用包含数字、字母和特殊字符的复杂密码</li>
            <li>定期更换支付密码，保持账户安全</li>
            <li>不要在公共场所输入支付密码</li>
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
            {action === 'set' ? '设置支付密码' : '修改支付密码'}
          </h3>
          <p className={styles.formSubtitle}>
            {action === 'set'
              ? '设置支付密码以保护您的萝卜转移操作'
              : '输入当前密码和新密码来修改支付密码'}
          </p>
        </div>

        <form className={styles.form} onSubmit={handleSubmit}>
          {/* 当前密码（修改时需要） */}
          {action === 'change' && (
            <div className={styles.formGroup}>
              <label className={styles.label}>
                当前支付密码 <span className={styles.required}>*</span>
              </label>
              <input
                type="password"
                className={`${styles.input} ${errors.currentPassword ? styles.error : ''}`}
                placeholder="请输入当前支付密码"
                value={formData.currentPassword || ''}
                onChange={(e) => handleInputChange('currentPassword', e.target.value)}
                maxLength={20}
              />
              {errors.currentPassword && (
                <div className={styles.errorMessage}>{errors.currentPassword}</div>
              )}
            </div>
          )}

          {/* 新密码 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>
              新支付密码 <span className={styles.required}>*</span>
            </label>
            <input
              type="password"
              className={`${styles.input} ${errors.newPassword ? styles.error : ''}`}
              placeholder="请输入新支付密码"
              value={formData.newPassword}
              onChange={(e) => handleInputChange('newPassword', e.target.value)}
              maxLength={20}
            />
            {formData.newPassword && (
              <div className={styles.passwordStrength}>
                <div className={styles.strengthLabel}>密码强度：</div>
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
            {errors.newPassword && (
              <div className={styles.errorMessage}>{errors.newPassword}</div>
            )}
          </div>

          {/* 确认密码 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>
              确认新密码 <span className={styles.required}>*</span>
            </label>
            <input
              type="password"
              className={`${styles.input} ${errors.confirmPassword ? styles.error : ''}`}
              placeholder="请再次输入新支付密码"
              value={formData.confirmPassword}
              onChange={(e) => handleInputChange('confirmPassword', e.target.value)}
              maxLength={20}
            />
            {errors.confirmPassword && (
              <div className={styles.errorMessage}>{errors.confirmPassword}</div>
            )}
          </div>

          {/* 密码要求 */}
          <div className={styles.passwordRequirements}>
            <h5 className={styles.requirementsTitle}>密码要求：</h5>
            <ul className={styles.requirementsList}>
              <li className={formData.newPassword.length >= 6 ? styles.met : ''}>
                长度至少6位
              </li>
              <li className={formData.newPassword.length <= 20 ? styles.met : ''}>
                长度不超过20位
              </li>
              <li className={/\d/.test(formData.newPassword) ? styles.met : ''}>
                包含至少一个数字
              </li>
              <li className={/[a-zA-Z]/.test(formData.newPassword) ? styles.met : ''}>
                包含至少一个字母
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
              disabled={loading || passwordStrength < 2}
            >
              {loading ? (
                <>
                  <div className={styles.buttonSpinner}></div>
                  处理中...
                </>
              ) : (
                action === 'set' ? '设置密码' : '修改密码'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
