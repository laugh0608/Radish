import { useState } from 'react';
import { log } from '@/utils/logger';
import { useCoinBalance, useTransfer } from '../../hooks';
import { validateTransferAmount } from '../../utils';
import { TransferForm } from './TransferForm';
import { TransferConfirm } from './TransferConfirm';
import { TransferResult } from './TransferResult';
import { RecentTransfers } from './RecentTransfers';
import type { TransferFormData, TransferResult as TransferResultType } from '../../types';
import styles from './Transfer.module.css';

type TransferStep = 'form' | 'confirm' | 'result';

/**
 * 转账功能组件
 */
export const Transfer = () => {
  const [currentStep, setCurrentStep] = useState<TransferStep>('form');
  const [transferData, setTransferData] = useState<TransferFormData | null>(null);
  const [transferResult, setTransferResult] = useState<TransferResultType | null>(null);
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  const { balance, loading: balanceLoading, refetch: refetchBalance } = useCoinBalance();
  const { transfer, loading: transferLoading } = useTransfer();

  const handleFormSubmit = (formData: TransferFormData) => {
    log.debug('Transfer', '提交转账表单', formData);
    setTransferData(formData);
    setCurrentStep('confirm');
  };

  const handleConfirm = async () => {
    if (!transferData) return;

    try {
      log.debug('Transfer', '确认转账', transferData);
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
        message: error instanceof Error ? error.message : '转账失败，请稍后重试'
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
          <h2 className={styles.title}>转移萝卜</h2>
          <p className={styles.subtitle}>向其他用户转移您的萝卜</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={`切换到${displayMode === 'carrot' ? '白萝卜' : '胡萝卜'}显示`}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {displayMode === 'carrot' ? '胡萝卜' : '白萝卜'}
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
            <div className={styles.stepLabel}>填写信息</div>
          </div>
          <div className={styles.stepConnector}></div>
          <div className={`${styles.step} ${currentStep === 'confirm' ? styles.active : ''} ${
            currentStep === 'result' ? styles.completed : ''
          }`}>
            <div className={styles.stepNumber}>2</div>
            <div className={styles.stepLabel}>确认转移</div>
          </div>
          <div className={styles.stepConnector}></div>
          <div className={`${styles.step} ${currentStep === 'result' ? styles.active : ''}`}>
            <div className={styles.stepNumber}>3</div>
            <div className={styles.stepLabel}>转移结果</div>
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
            <RecentTransfers displayMode={displayMode} />
          </div>
        )}
      </div>
    </div>
  );
};