import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { formatCoinDateTime, formatCoinNumber, formatPasscodeStrength } from '../../utils';
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
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
        requiredMessage: t('pit.passcode.validation.currentRequired'),
        formatMessage: t('pit.passcode.validation.format'),
        repeatedDigitsMessage: t('pit.passcode.validation.repeatedDigits'),
        allowRepeatedDigits: true,
      });
      if (currentPasswordError) {
        newErrors.currentPassword = currentPasswordError;
      }
    }

    // 验证新口令
    const newPasswordError = getPaymentPasscodeValidationMessage(formData.newPassword, {
      requiredMessage: t('pit.passcode.validation.newRequired'),
      formatMessage: t('pit.passcode.validation.format'),
      repeatedDigitsMessage: t('pit.passcode.validation.repeatedDigits'),
    });
    if (newPasswordError) {
      newErrors.newPassword = newPasswordError;
    }

    // 验证确认口令
    if (!formData.confirmPassword.trim()) {
      newErrors.confirmPassword = t('pit.passcode.validation.confirmRequired');
    } else if (formData.newPassword !== formData.confirmPassword) {
      newErrors.confirmPassword = t('pit.passcode.validation.mismatch');
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
        }, t);
      } else if (action === 'change') {
        log.debug('PaymentPasswordSettings', '修改支付口令');
        await paymentPasswordApi.changePaymentPassword({
          currentPassword: formData.currentPassword!,
          newPassword: formData.newPassword,
          confirmPassword: formData.confirmPassword
        }, t);
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
            ? t('pit.security.passcode.resetSuccess')
            : t('pit.security.passcode.setSuccess')
          : t('pit.security.passcode.changeSuccess')
      );
    } catch (error) {
      log.error('支付口令操作失败:', error);
      toast.error(error instanceof Error ? error.message : t('pit.security.passcode.operationFailed'));
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
              {t('pit.security.passcode.statusTitle')}
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
                  {t(requiresPasscodeUpgrade
                    ? 'pit.security.passcode.legacy'
                    : status?.hasPaymentPassword
                      ? 'pit.security.passcode.configuredFull'
                      : 'pit.security.passcode.notConfiguredFull')}
                </div>
                <div className={styles.indicatorDescription}>
                  {t(requiresPasscodeUpgrade
                    ? 'pit.security.passcode.legacyDescription'
                    : status?.hasPaymentPassword
                      ? 'pit.security.passcode.verifiedOperations'
                      : 'pit.security.passcode.notConfiguredDescription')}
                </div>
              </div>
            </div>

            {requiresPasscodeUpgrade && (
              <div className={styles.upgradeNotice}>
                <div className={styles.upgradeTitle}>{t('pit.security.passcode.resetRequiredTitle')}</div>
                <div className={styles.upgradeText}>
                  {t('pit.security.passcode.resetRequiredDescription')}
                </div>
              </div>
            )}

            {status?.hasPaymentPassword && (
              <div className={styles.passwordInfo}>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>{t('pit.security.passcode.strength')}</div>
                  <div className={styles.infoValue}>
                    <span className={`${styles.strengthBadge} ${styles[getPasswordStrengthColor(status.strengthLevel ?? 0)]}`}>
                      {formatPasscodeStrength(status.strengthLevel, t)}
                    </span>
                  </div>
                </div>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>{t('pit.security.passcode.lastModified')}</div>
                  <div className={styles.infoValue}>
                    {status.lastPasswordChangeTime
                      ? formatCoinDateTime(status.lastPasswordChangeTime, displayTimeZone, language)
                      : t('pit.common.unknown')}
                  </div>
                </div>
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>{t('pit.security.passcode.failedAttempts')}</div>
                  <div className={styles.infoValue}>
                    {t('pit.security.attempts.count', {
                      count: status.failedAttempts,
                      value: formatCoinNumber(status.failedAttempts, language),
                    })}
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
                {t('pit.security.passcode.changeFull')}
              </button>
            ) : (
              <button
                className={styles.primaryButton}
                onClick={() => setAction('set')}
              >
                {t(requiresPasscodeUpgrade
                  ? 'pit.security.passcode.resetAsNew'
                  : 'pit.security.passcode.setFull')}
              </button>
            )}
          </div>
        </div>

        {/* 安全提示 */}
        <div className={styles.securityTips}>
          <h4 className={styles.tipsTitle}>
            <span className={styles.tipsIcon}>💡</span>
            {t('pit.security.passcode.tipsTitle')}
          </h4>
          <ul className={styles.tipsList}>
            <li>{t('pit.security.passcode.tipPurpose')}</li>
            <li>{t('pit.security.passcode.tipFormat')}</li>
            <li>{t('pit.security.passcode.tipSequence')}</li>
            <li>{t('pit.security.passcode.tipSeparate')}</li>
            <li>{t('pit.security.passcode.tipPublic')}</li>
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
            {t(action === 'set' ? 'pit.security.passcode.setFull' : 'pit.security.passcode.changeFull')}
          </h3>
          <p className={styles.formSubtitle}>
            {t(action === 'set'
              ? requiresPasscodeUpgrade
                ? 'pit.security.passcode.formResetDescription'
                : 'pit.security.passcode.formSetDescription'
              : 'pit.security.passcode.formChangeDescription')}
          </p>
        </div>

        {requiresPasscodeUpgrade && action === 'set' && (
          <div className={styles.upgradeNotice}>
            <div className={styles.upgradeTitle}>{t('pit.security.passcode.legacyUnavailable')}</div>
            <div className={styles.upgradeText}>
              {t('pit.security.passcode.legacyResetDescription')}
            </div>
          </div>
        )}

        <form className={styles.form} onSubmit={handleSubmit}>
          {/* 当前口令（修改时需要） */}
          {action === 'change' && (
            <div className={styles.formGroup}>
              <label className={styles.label}>
                {t('pit.security.passcode.current')} <span className={styles.required}>*</span>
              </label>
              <PasscodeInput
                id="payment-passcode-current"
                value={formData.currentPassword || ''}
                onChange={(value) => handleInputChange('currentPassword', value)}
                autoComplete="current-password"
                error={errors.currentPassword}
                helperText={t('pit.security.passcode.currentHelper')}
                disabled={loading}
              />
            </div>
          )}

          {/* 新口令 */}
          <div className={styles.formGroup}>
              <label className={styles.label}>
                {t('pit.security.passcode.new')} <span className={styles.required}>*</span>
              </label>
            <PasscodeInput
              id="payment-passcode-new"
              value={formData.newPassword}
              onChange={(value) => handleInputChange('newPassword', value)}
              autoComplete="new-password"
              error={errors.newPassword}
              helperText={t('pit.security.passcode.newHelper')}
              disabled={loading}
              autoFocus={action === 'set'}
            />
            {formData.newPassword && (
              <div className={styles.passwordStrength}>
                <div className={styles.strengthLabel}>{t('pit.security.passcode.strengthLabel')}</div>
                <div className={`${styles.strengthValue} ${styles[getPasswordStrengthColor(passwordStrength)]}`}>
                  {formatPasscodeStrength(passwordStrength, t)}
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
                {t('pit.security.passcode.confirm')} <span className={styles.required}>*</span>
              </label>
            <PasscodeInput
              id="payment-passcode-confirm"
              value={formData.confirmPassword}
              onChange={(value) => handleInputChange('confirmPassword', value)}
              autoComplete="new-password"
              error={errors.confirmPassword}
              helperText={t('pit.security.passcode.confirmHelper')}
              disabled={loading}
            />
          </div>

          {/* 口令要求 */}
          <div className={styles.passwordRequirements}>
            <h5 className={styles.requirementsTitle}>{t('pit.security.passcode.requirements')}</h5>
            <ul className={styles.requirementsList}>
              <li className={isPaymentPasscodeFormatValid(formData.newPassword) ? styles.met : ''}>
                {t('pit.security.passcode.requirementFormat')}
              </li>
              <li className={isPaymentPasscodeValid(formData.newPassword) ? styles.met : ''}>
                {t('pit.security.passcode.requirementRepeated')}
              </li>
              <li className={isPaymentPasscodeFormatValid(formData.newPassword) && !isSequentialPaymentPasscode(formData.newPassword) ? styles.met : ''}>
                {t('pit.security.passcode.requirementSequence')}
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
              {t('pit.common.cancel')}
            </button>
            <button
              type="submit"
              className={styles.submitButton}
              disabled={
                loading ||
                !isPaymentPasscodeValid(formData.newPassword) ||
                formData.newPassword !== formData.confirmPassword ||
                (action === 'change' && Boolean(getPaymentPasscodeValidationMessage(formData.currentPassword ?? '', {
                  requiredMessage: t('pit.passcode.validation.currentRequired'),
                  formatMessage: t('pit.passcode.validation.format'),
                  repeatedDigitsMessage: t('pit.passcode.validation.repeatedDigits'),
                  allowRepeatedDigits: true,
                })))
              }
            >
              {loading ? (
                <>
                  <div className={styles.buttonSpinner}></div>
                  {t('pit.common.processing')}
                </>
              ) : (
                action === 'set'
                  ? t(requiresPasscodeUpgrade
                    ? 'pit.security.passcode.resetShort'
                    : 'pit.security.passcode.setShort')
                  : t('pit.security.passcode.changeShort')
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
