import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { isApiResponseNotFoundError } from '@radish/http';
import {
  AntInput as Input,
  Button,
  LeftOutlined,
  ReloadOutlined,
  SyncOutlined,
  Tag,
  formatLocalizedDateTime,
  formatLocalizedNumber,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { adminGetOrder } from '../../api/shopApi';
import type { Order } from '../../api/types';
import { log } from '../../utils/logger';
import {
  getOrderDurationLabel,
  getOrderFailureStageLabel,
  getOrderProductTypeLabel,
  getOrderStatusColor,
  getOrderStatusLabel,
} from './orderPresentation';

export interface OrderActionFeedback {
  tone: 'success' | 'danger';
  message: string;
}

interface OrderDetailProps {
  visible: boolean;
  orderId?: string;
  fallbackOrder?: Order;
  reloadToken?: number;
  feedback?: OrderActionFeedback | null;
  onClose: () => void;
  onReload?: () => void;
  onOrderLoaded?: (order: Order) => void;
  onRetry?: (order: Order) => void;
  onViewUser?: (order: Order) => void;
  onViewProduct?: (order: Order) => void;
  onViewCoinTransaction?: (order: Order) => void;
  canRemark?: boolean;
  savingRemark?: boolean;
  onSaveRemark?: (order: Order, remark: string) => Promise<boolean>;
}

type DetailReadState = 'loading' | 'ready' | 'stale' | 'unavailable';

function formatDateTime(value: string | null | undefined, language: string): string {
  if (!value) {
    return '-';
  }

  return formatLocalizedDateTime(value, language);
}

interface DetailFieldProps {
  label: string;
  value: ReactNode;
  wide?: boolean;
  danger?: boolean;
  mono?: boolean;
}

function DetailField({ label, value, wide = false, danger = false, mono = false }: DetailFieldProps) {
  return (
    <div className={wide ? 'order-detail-field order-detail-field--wide' : 'order-detail-field'}>
      <span>{label}</span>
      <strong className={[
        danger ? 'order-detail-field__value--danger' : '',
        mono ? 'order-detail-field__value--mono' : '',
      ].filter(Boolean).join(' ')}>{value}</strong>
    </div>
  );
}

export const OrderDetail = ({
  visible,
  orderId,
  fallbackOrder,
  reloadToken = 0,
  feedback,
  onClose,
  onReload,
  onOrderLoaded,
  onRetry,
  onViewUser,
  onViewProduct,
  onViewCoinTransaction,
  canRemark = false,
  savingRemark = false,
  onSaveRemark,
}: OrderDetailProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [detailOrder, setDetailOrder] = useState<Order | undefined>(fallbackOrder);
  const [adminRemark, setAdminRemark] = useState('');
  const [readState, setReadState] = useState<DetailReadState>('loading');
  const onOrderLoadedRef = useRef(onOrderLoaded);

  useEffect(() => {
    onOrderLoadedRef.current = onOrderLoaded;
  }, [onOrderLoaded]);

  useEffect(() => {
    if (!visible) {
      setDetailOrder(fallbackOrder);
      setReadState('loading');
      return;
    }

    setDetailOrder(fallbackOrder);
    setReadState('loading');
    // The authoritative detail read owns subsequent updates for this order ID.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderId, visible]);

  useEffect(() => {
    if (visible && readState !== 'ready' && fallbackOrder) {
      setDetailOrder(fallbackOrder);
    }
  }, [fallbackOrder, readState, visible]);

  useEffect(() => {
    if (!visible || !orderId) {
      return;
    }

    let cancelled = false;

    const loadOrder = async () => {
      try {
        setReadState('loading');
        const result = await adminGetOrder(orderId);
        if (cancelled) {
          return;
        }

        setDetailOrder(result);
        setReadState('ready');
        onOrderLoadedRef.current?.(result);
      } catch (error) {
        if (cancelled) {
          return;
        }

        log.error('OrderDetail', '加载订单详情失败:', error);
        setReadState(isApiResponseNotFoundError(error) ? 'unavailable' : 'stale');
        setDetailOrder((current) => current ?? fallbackOrder);
      }
    };

    void loadOrder();

    return () => {
      cancelled = true;
    };
    // onOrderLoaded is read through a ref so parent presentation updates do not replay the API request.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderId, reloadToken, visible]);

  const currentOrder = detailOrder ?? fallbackOrder;

  useEffect(() => {
    if (!visible || !currentOrder) {
      return;
    }

    setAdminRemark(currentOrder.voAdminRemark ?? '');
  }, [currentOrder, visible]);

  const normalizedSavedRemark = useMemo(
    () => (currentOrder?.voAdminRemark ?? '').trim(),
    [currentOrder?.voAdminRemark],
  );
  const normalizedEditingRemark = adminRemark.trim();
  const actionsAreAuthoritative = readState === 'ready';
  const canSaveRemark = actionsAreAuthoritative
    && canRemark
    && !!onSaveRemark
    && normalizedEditingRemark !== normalizedSavedRemark
    && !savingRemark;

  if (!visible) {
    return null;
  }

  return (
    <aside
      className="order-detail-task"
      role="dialog"
      aria-modal="false"
      aria-label={t('orders.detail.title')}
      data-console-fullscreen-task="orders"
    >
      <header className="order-detail-task__header">
        <Button variant="ghost" size="small" icon={<LeftOutlined />} onClick={onClose}>
          {t('orders.detail.back')}
        </Button>
        <div className="order-detail-task__heading">
          <span>{t('orders.detail.title')}</span>
          <strong>{currentOrder?.voOrderNo ?? t('orders.detail.notFound')}</strong>
        </div>
        <Button variant="ghost" size="small" onClick={onClose}>
          {t('orders.detail.close')}
        </Button>
      </header>

      {!currentOrder ? (
        <div className="order-detail-empty">
          <strong>{t(readState === 'unavailable' ? 'orders.detail.unavailableTitle' : 'orders.detail.loading')}</strong>
          <span>{t(readState === 'unavailable' ? 'orders.detail.unavailableDescription' : 'orders.detail.loadingDescription')}</span>
          <Button onClick={onClose}>{t('orders.detail.backToList')}</Button>
        </div>
      ) : (
        <div className="order-detail-task__scroll">
          <section className="order-detail-status-panel">
            <div className="order-detail-status-panel__main">
              <Tag color={getOrderStatusColor(currentOrder.voStatus)}>
                {getOrderStatusLabel(currentOrder, t)}
              </Tag>
              <span>{getOrderFailureStageLabel(currentOrder.voFailureStage, t)}</span>
              {readState === 'loading' ? <span>{t('orders.detail.verifying')}</span> : null}
            </div>
            <div className="order-detail-status-panel__actions">
              {onReload ? (
                <Button size="small" icon={<ReloadOutlined />} onClick={onReload}>
                  {t('orders.list.refresh')}
                </Button>
              ) : null}
              {actionsAreAuthoritative && currentOrder.voCanRetryFulfillment === true && onRetry ? (
                <Button variant="primary" size="small" icon={<SyncOutlined />} onClick={() => onRetry(currentOrder)}>
                  {t('orders.action.retryFulfillment')}
                </Button>
              ) : null}
            </div>
          </section>

          {readState === 'unavailable' || readState === 'stale' ? (
            <div className="order-detail-state order-detail-state--warning" role="status">
              <ReloadOutlined />
              <div>
                <strong>{t(readState === 'unavailable' ? 'orders.detail.unavailableTitle' : 'orders.detail.staleTitle')}</strong>
                <span>{t(readState === 'unavailable' ? 'orders.detail.unavailableDescription' : 'orders.detail.staleDescription')}</span>
              </div>
            </div>
          ) : null}

          {feedback ? (
            <div className={`order-detail-state order-detail-state--${feedback.tone}`} role="status" aria-live="polite">
              <div><strong>{feedback.message}</strong></div>
            </div>
          ) : null}

          <section className="order-detail-group">
            <div className="order-detail-group__header">
              <h3>{t('orders.detail.group.snapshot')}</h3>
              <span>{t('orders.detail.group.snapshotDescription')}</span>
            </div>
            <div className="order-detail-fields">
              <DetailField label={t('orders.detail.field.orderNo')} value={currentOrder.voOrderNo} wide mono />
              <DetailField label={t('orders.detail.field.createdAt')} value={formatDateTime(currentOrder.voCreateTime, language)} />
              <DetailField label={t('orders.detail.field.user')} value={currentOrder.voUserName || t('orders.common.unknown')} />
              <DetailField label={t('orders.detail.field.userId')} value={currentOrder.voUserId} mono />
              <DetailField label={t('orders.detail.field.product')} value={currentOrder.voProductName} />
              <DetailField label={t('orders.detail.field.productId')} value={currentOrder.voProductId} mono />
              <DetailField label={t('orders.detail.field.productType')} value={getOrderProductTypeLabel(currentOrder.voProductType, t)} />
              <DetailField label={t('orders.detail.field.quantity')} value={currentOrder.voQuantity} />
              <DetailField
                label={t('orders.detail.field.unitPrice')}
                value={`${formatLocalizedNumber(currentOrder.voUnitPrice, language)} ${t('console.unit.carrot')}`}
              />
              <DetailField
                label={t('orders.detail.field.totalPrice')}
                value={`${formatLocalizedNumber(currentOrder.voTotalPrice, language)} ${t('console.unit.carrot')}`}
                danger
              />
              <DetailField
                label={t('orders.detail.field.duration')}
                value={getOrderDurationLabel(currentOrder, t, (value) => formatDateTime(value, language))}
                wide
              />
              <DetailField label={t('orders.detail.field.userRemark')} value={currentOrder.voUserRemark || '-'} wide />
            </div>
            <div className="order-detail-links">
              {onViewUser ? <Button size="small" onClick={() => onViewUser(currentOrder)}>{t('orders.action.viewUser')}</Button> : null}
              {onViewProduct ? <Button size="small" onClick={() => onViewProduct(currentOrder)}>{t('orders.action.viewProduct')}</Button> : null}
            </div>
          </section>

          <section className="order-detail-group">
            <div className="order-detail-group__header">
              <h3>{t('orders.detail.group.payment')}</h3>
              <span>{t('orders.detail.group.paymentDescription')}</span>
            </div>
            <div className="order-detail-fields">
              <DetailField label={t('orders.detail.field.paidAt')} value={formatDateTime(currentOrder.voPaidTime, language)} />
              <DetailField label={t('orders.detail.field.coinTransactionId')} value={currentOrder.voCoinTransactionId || '-'} wide mono />
            </div>
            {currentOrder.voCoinTransactionId && onViewCoinTransaction ? (
              <div className="order-detail-links">
                <Button size="small" onClick={() => onViewCoinTransaction(currentOrder)}>{t('orders.detail.viewDebit')}</Button>
              </div>
            ) : null}
          </section>

          <section className="order-detail-group">
            <div className="order-detail-group__header">
              <h3>{t('orders.detail.group.fulfillment')}</h3>
              <span>{t('orders.detail.group.fulfillmentDescription')}</span>
            </div>
            <div className="order-detail-fields">
              <DetailField label={t('orders.detail.field.completedAt')} value={formatDateTime(currentOrder.voCompletedTime, language)} />
              <DetailField label={t('orders.detail.field.benefitExpiresAt')} value={formatDateTime(currentOrder.voBenefitExpiresAt, language)} />
              <DetailField label={t('orders.detail.field.fixedExpiresAt')} value={formatDateTime(currentOrder.voFixedExpiresAt, language)} />
              <DetailField label={t('orders.detail.field.cancelledAt')} value={formatDateTime(currentOrder.voCancelledTime, language)} />
              <DetailField label={t('orders.detail.field.benefitId')} value={currentOrder.voGrantedBenefitId || '-'} mono />
              <DetailField label={t('orders.detail.field.inventoryId')} value={currentOrder.voGrantedInventoryId || '-'} mono />
              <DetailField label={t('orders.detail.field.cancelReason')} value={currentOrder.voCancelReason || '-'} wide />
              <DetailField
                label={t('orders.detail.field.failReason')}
                value={currentOrder.voFailReason || '-'}
                wide
                danger={Boolean(currentOrder.voFailReason)}
              />
            </div>
          </section>

          {canRemark && onSaveRemark ? (
            <section className="order-detail-group order-detail-remark">
              <div className="order-detail-group__header">
                <h3>{t('orders.detail.adminRemark')}</h3>
                <span>{t('orders.detail.remarkDescription')}</span>
              </div>
              <Input.TextArea
                rows={4}
                maxLength={500}
                showCount
                value={adminRemark}
                onChange={(event) => setAdminRemark(event.target.value)}
                placeholder={t('orders.detail.remarkPlaceholder')}
                disabled={!actionsAreAuthoritative || savingRemark}
              />
              <div className="order-detail-remark__actions">
                <Button
                  variant="primary"
                  disabled={!canSaveRemark}
                  onClick={() => void onSaveRemark(currentOrder, normalizedEditingRemark)}
                >
                  {t(savingRemark ? 'orders.detail.saving' : 'orders.detail.saveRemark')}
                </Button>
              </div>
            </section>
          ) : null}
        </div>
      )}
    </aside>
  );
};
