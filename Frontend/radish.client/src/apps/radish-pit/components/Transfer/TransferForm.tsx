import { useState, useEffect, useMemo } from 'react';
import { log } from '@/utils/logger';
import { PasscodeInput } from '@radish/ui';
import { getPaymentPasscodeValidationMessage } from '@/utils/paymentPasscode';
import { formatCoinAmount, validateTransferAmount, debounce } from '../../utils';
import { searchUsersForMention, type UserMentionOption } from '@/api/user';
import { normalizePositiveLongIdKey } from '@/utils/longId';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { useTranslation } from 'react-i18next';
import type { CoinAmount } from '@/api/coin';
import type { TransferFormData } from '../../types';
import styles from './TransferForm.module.css';

interface TransferFormProps {
  balance: CoinAmount;
  displayMode: 'carrot' | 'white';
  loading: boolean;
  onSubmit: (data: TransferFormData) => void;
}

/**
 * 转账表单组件
 */
export const TransferForm = ({ balance, displayMode, loading, onSubmit }: TransferFormProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [formData, setFormData] = useState<Partial<TransferFormData>>({
    amount: 0,
    note: '',
    paymentPassword: ''
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [userSearchQuery, setUserSearchQuery] = useState('');
  const [userSearchResults, setUserSearchResults] = useState<UserMentionOption[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [showUserDropdown, setShowUserDropdown] = useState(false);
  const [amountValidation, setAmountValidation] = useState<{ isValid: boolean; message?: string }>({ isValid: true });

  const passcodeValidationMessages = {
    requiredMessage: t('pit.passcode.validation.required'),
    formatMessage: t('pit.passcode.validation.format'),
    repeatedDigitsMessage: t('pit.passcode.validation.repeatedDigits'),
  };

  // 防抖搜索用户
  const searchUsers = useMemo(
    () => debounce(async (query: string) => {
      if (!query.trim()) {
        setUserSearchResults([]);
        setShowUserDropdown(false);
        return;
      }

      try {
        setSearchLoading(true);
        const results = await searchUsersForMention(query, t, 10);

        // API 已经做了格式转换，直接使用
        setUserSearchResults(results);
        setShowUserDropdown(results.length > 0);
        log.debug('TransferForm', '用户搜索完成', { query, count: results.length });
      } catch (error) {
        log.error('TransferForm', '搜索用户失败:', error);
        setUserSearchResults([]);
      } finally {
        setSearchLoading(false);
      }
    }, 300),
    [t]
  );

  useEffect(() => {
    searchUsers(userSearchQuery);
  }, [userSearchQuery, searchUsers]);

  const handleInputChange = (field: keyof TransferFormData, value: number | string) => {
    setFormData(prev => ({ ...prev, [field]: value }));

    // 清除对应字段的错误
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }

    // 实时验证转账金额
    if (field === 'amount') {
      const validation = validateTransferAmount(Number(value), balance, t);
      setAmountValidation(validation);
    }
  };

  const handleUserSearch = (query: string) => {
    setUserSearchQuery(query);
    handleInputChange('recipientName', query);

    // 如果用户清空了搜索框，也清空选中的用户ID
    if (!query.trim()) {
      handleInputChange('recipientId', '');
    }
  };

  const handleUserSelect = (user: UserMentionOption) => {
    const userId = normalizePositiveLongIdKey(user.voId);
    const displayName = resolveVisibleUserDisplayName(user, t('common.unknownUser'));
    const displayHandle = resolveVisibleUserHandle(user, displayName) || displayName;

    if (!userId) {
      setErrors(prev => ({ ...prev, recipientId: t('pit.transfer.validation.recipientInvalid') }));
      return;
    }

    setFormData(prev => ({
      ...prev,
      recipientId: userId,
      recipientName: displayHandle
    }));
    setUserSearchQuery(displayHandle);
    setShowUserDropdown(false);

    // 清除用户相关错误
    setErrors(prev => ({ ...prev, recipientId: '', recipientName: '' }));
  };

  const validateForm = (): Record<string, string> => {
    const newErrors: Record<string, string> = {};
    const recipientId = normalizePositiveLongIdKey(formData.recipientId);

    // 验证接收方
    if (!recipientId) {
      newErrors.recipientId = t('pit.transfer.validation.recipientRequired');
    }

    if (!formData.recipientName?.trim()) {
      newErrors.recipientName = t('pit.transfer.validation.recipientQueryRequired');
    }

    // 验证金额
    if (!formData.amount || formData.amount <= 0) {
      newErrors.amount = t('pit.transfer.validation.amountRequired');
    } else {
      const validation = validateTransferAmount(formData.amount, balance, t);
      if (!validation.isValid) {
        newErrors.amount = validation.message || t('pit.transfer.validation.amountInvalid');
      }
    }

    // 验证支付口令
    const paymentPasscodeError = getPaymentPasscodeValidationMessage(
      formData.paymentPassword ?? '',
      passcodeValidationMessages,
    );
    if (paymentPasscodeError) {
      newErrors.paymentPassword = paymentPasscodeError;
    }

    setErrors(newErrors);
    return newErrors;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const validationErrors = validateForm();
    if (Object.keys(validationErrors).length > 0) {
      log.debug('TransferForm', '表单验证失败', validationErrors);
      return;
    }

    const transferData: TransferFormData = {
      recipientId: normalizePositiveLongIdKey(formData.recipientId)!,
      recipientName: formData.recipientName!,
      amount: formData.amount!,
      note: formData.note || '',
      paymentPassword: formData.paymentPassword!
    };

    log.debug('TransferForm', '提交转账表单', {
      recipientId: transferData.recipientId,
      amount: transferData.amount,
      hasNote: Boolean(transferData.note?.trim())
    });
    onSubmit(transferData);
  };

  const handleQuickAmount = (percentage: number) => {
    const quickAmount = Math.floor(Math.min(Number(balance), 100000) * percentage);
    handleInputChange('amount', quickAmount);
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.overview.loading')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>
            <span className={styles.cardIcon}>💸</span>
            {t('pit.transfer.form.title')}
          </h3>
          <div className={styles.balanceInfo}>
            {t('pit.transfer.form.available', {
              value: formatCoinAmount(balance, language, t, displayMode),
            })}
          </div>
        </div>

        <form className={styles.form} onSubmit={handleSubmit}>
          {/* 接收方选择 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>
              {t('pit.transfer.form.recipient')} <span className={styles.required}>*</span>
            </label>
            <div className={styles.userSearchContainer}>
              <input
                type="text"
                className={`${styles.input} ${errors.recipientName ? styles.error : ''}`}
                placeholder={t('pit.transfer.form.recipientPlaceholder')}
                value={userSearchQuery}
                onChange={(e) => handleUserSearch(e.target.value)}
                onFocus={() => userSearchResults.length > 0 && setShowUserDropdown(true)}
              />
              {searchLoading && (
                <div className={styles.searchLoading}>
                  <div className={styles.searchSpinner}></div>
                </div>
              )}

              {showUserDropdown && userSearchResults.length > 0 && (
                <div className={styles.userDropdown}>
                  {userSearchResults.map((user) => {
                    const displayName = resolveVisibleUserDisplayName(user, t('common.unknownUser'));
                    const displayHandle = resolveVisibleUserHandle(user, displayName);
                    return (
                      <div
                        key={user.voId}
                        className={styles.userOption}
                        onClick={() => handleUserSelect(user)}
                      >
                        <div className={styles.userAvatar}>
                          {user.voAvatar ? (
                            <img src={user.voAvatar} alt={displayName} />
                          ) : (
                            <span>{displayName.charAt(0)}</span>
                          )}
                        </div>
                        <div className={styles.userInfo}>
                          <div className={styles.userName}>{displayName}</div>
                          {displayHandle && (
                            <div className={styles.userHandle}>{displayHandle}</div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
            {errors.recipientName && (
              <div className={styles.errorMessage}>{errors.recipientName}</div>
            )}
          </div>

          {/* 转移金额 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>
              {t('pit.transfer.form.amount')} <span className={styles.required}>*</span>
            </label>
            <div className={styles.amountContainer}>
              <input
                type="number"
                className={`${styles.input} ${styles.amountInput} ${
                  errors.amount || !amountValidation.isValid ? styles.error : ''
                }`}
                placeholder={t('pit.transfer.form.amountPlaceholder')}
                value={formData.amount || ''}
                onChange={(e) => handleInputChange('amount', Number(e.target.value))}
                min="1"
                max={Math.min(Number(balance), 100000)}
              />
              <div className={styles.amountUnit}>
                {t('pit.currency.carrot')}
              </div>
            </div>

            {/* 快捷金额按钮 */}
            <div className={styles.quickAmounts}>
              <button
                type="button"
                className={styles.quickAmountButton}
                onClick={() => handleQuickAmount(0.25)}
              >
                25%
              </button>
              <button
                type="button"
                className={styles.quickAmountButton}
                onClick={() => handleQuickAmount(0.5)}
              >
                50%
              </button>
              <button
                type="button"
                className={styles.quickAmountButton}
                onClick={() => handleQuickAmount(0.75)}
              >
                75%
              </button>
              <button
                type="button"
                className={styles.quickAmountButton}
                onClick={() => handleQuickAmount(1)}
              >
                {t('pit.transfer.form.allAvailable')}
              </button>
            </div>

            {(errors.amount || !amountValidation.isValid) && (
              <div className={styles.errorMessage}>
                {errors.amount || amountValidation.message}
              </div>
            )}
          </div>

          {/* 转移备注 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>{t('pit.transfer.form.remark')}</label>
            <textarea
              className={styles.textarea}
              placeholder={t('pit.transfer.form.remarkPlaceholder')}
              value={formData.note || ''}
              onChange={(e) => handleInputChange('note', e.target.value)}
              maxLength={200}
              rows={3}
            />
            <div className={styles.charCount}>
              {(formData.note || '').length}/200
            </div>
          </div>

          {/* 支付口令 */}
          <div className={styles.formGroup}>
            <label className={styles.label}>
              {t('pit.transfer.form.passcode')} <span className={styles.required}>*</span>
            </label>
            <PasscodeInput
              id="transfer-payment-passcode"
              value={formData.paymentPassword || ''}
              onChange={(value) => handleInputChange('paymentPassword', value)}
              error={errors.paymentPassword}
              helperText={t('pit.transfer.form.passcodeHelper')}
              autoComplete="current-password"
            />
          </div>

          {/* 提交按钮 */}
          <div className={styles.formActions}>
            <button
              type="submit"
              className={styles.submitButton}
              disabled={!amountValidation.isValid || Object.keys(errors).length > 0}
            >
              {t('pit.common.next')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
