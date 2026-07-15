import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { useCoinBalance, useTransfer } from '../../hooks';
import { TransferForm } from './TransferForm';
import { TransferConfirm } from './TransferConfirm';
import { TransferResult } from './TransferResult';
import { RecentTransfers } from './RecentTransfers';
import type { TabType, TransferFormData, TransferResult as TransferResultType } from '../../types';
import styles from './Transfer.module.css';

type TransferStep = 'form' | 'confirm' | 'result';

const createTransferLogMeta = (transferData: TransferFormData) => ({
  recipientId: transferData.recipientId,
  amount: transferData.amount,
  hasNote: Boolean(transferData.note?.trim())
});

function buildTransferIdempotencyKey(): string {
  const randomPart = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `coin-transfer:${randomPart}`;
}

/**
 * 转账功能组件
 */
interface TransferProps {
  onNavigate: (tab: TabType) => void;
}

export const Transfer = ({ onNavigate }: TransferProps) => {
  const { t } = useTranslation();
  const [currentStep, setCurrentStep] = useState<TransferStep>('form');
  const [transferData, setTransferData] = useState<TransferFormData | null>(null);
  const [transferResult, setTransferResult] = useState<TransferResultType | null>(null);
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  const { balance, loading: balanceLoading, refetch: refetchBalance } = useCoinBalance();
  const { transfer, loading: transferLoading } = useTransfer();

  const handleFormSubmit = (formData: TransferFormData) => {
    log.debug('Transfer', '提交转账表单', createTransferLogMeta(formData));
    setTransferData({
      ...formData,
      idempotencyKey: buildTransferIdempotencyKey()
    });
    setCurrentStep('confirm');
  };

  const handleConfirm = async () => {
    if (!transferData) return;

    try {
      log.debug('Transfer', '确认转账', createTransferLogMeta(transferData));
      const result = await transfer(transferData);
      setTransferResult(result);
      setCurrentStep('result');

      // 如果转账成功，刷新余额
      if (result.success) {
        await refetchBalance();
      }
    } catch (error) {
      log.error('转账失败:', error);
      setTransferResult({
        success: false,
        message: error instanceof Error ? error.message : t('pit.api.transferFailed')
      });
      setCurrentStep('result');
    }
  };

  const handleCancel = () => {
    log.debug('Transfer', '取消转账');
    setCurrentStep('form');
    setTransferData(null);
  };

  const handleStartNew = () => {
    log.debug('Transfer', '开始新的转账');
    setCurrentStep('form');
    setTransferData(null);
    setTransferResult(null);
  };

  const toggleDisplayMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'white' : 'carrot');
  };

  const renderStepContent = () => {
    switch (currentStep) {
      case 'form':
        return (
          <TransferForm
            balance={balance}
            displayMode={displayMode}
            loading={balanceLoading}
            onSubmit={handleFormSubmit}
          />
        );
      case 'confirm':
        return (
          <TransferConfirm
            transferData={transferData!}
            displayMode={displayMode}
            loading={transferLoading}
            onConfirm={handleConfirm}
            onCancel={handleCancel}
          />
        );
      case 'result':
        return (
          <TransferResult
            result={transferResult!}
            displayMode={displayMode}
            onStartNew={handleStartNew}
            onResetPasscode={() => onNavigate('security')}
            onViewHistory={() => onNavigate('history')}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      {/* 页面标题和操作 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>{t('pit.transfer.title')}</h2>
          <p className={styles.subtitle}>{t('pit.transfer.description')}</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={t('pit.currency.switchTo', {
              mode: t(displayMode === 'carrot' ? 'pit.currency.white' : 'pit.currency.carrot'),
            })}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {t(displayMode === 'carrot' ? 'pit.currency.carrot' : 'pit.currency.white')}
          </button>
        </div>
      </div>

      {/* 步骤指示器 */}
      <div className={styles.stepIndicator}>
        <div className={styles.steps}>
          <div className={`${styles.step} ${currentStep === 'form' ? styles.active : ''} ${
            ['confirm', 'result'].includes(currentStep) ? styles.completed : ''
          }`}>
            <div className={styles.stepNumber}>1</div>
            <div className={styles.stepLabel}>{t('pit.transfer.step.form')}</div>
          </div>
          <div className={styles.stepConnector}></div>
          <div className={`${styles.step} ${currentStep === 'confirm' ? styles.active : ''} ${
            currentStep === 'result' ? styles.completed : ''
          }`}>
            <div className={styles.stepNumber}>2</div>
            <div className={styles.stepLabel}>{t('pit.transfer.step.confirm')}</div>
          </div>
          <div className={styles.stepConnector}></div>
          <div className={`${styles.step} ${currentStep === 'result' ? styles.active : ''}`}>
            <div className={styles.stepNumber}>3</div>
            <div className={styles.stepLabel}>{t('pit.transfer.step.result')}</div>
          </div>
        </div>
      </div>

      {/* 主要内容区域 */}
      <div className={styles.content}>
        <div className={styles.mainContent}>
          {renderStepContent()}
        </div>

        {/* 最近转账记录 */}
        {currentStep === 'form' && (
          <div className={styles.sideContent}>
            <RecentTransfers displayMode={displayMode} onViewAll={() => onNavigate('history')} />
          </div>
        )}
      </div>
    </div>
  );
};
