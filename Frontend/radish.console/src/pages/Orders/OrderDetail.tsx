import { useEffect, useMemo, useState } from 'react';
import { Modal, Descriptions, Tag, Button, Space, AntInput as Input, message, formatLocalizedDateTime, formatLocalizedNumber } from '@radish/ui';
import { SyncOutlined } from '@radish/ui';
import { adminGetOrder } from '../../api/shopApi';
import type { Order } from '../../api/types';
import { useTranslation } from 'react-i18next';
import {
  getOrderFailureStageLabel,
  getOrderDurationLabel,
  getOrderProductTypeLabel,
  getOrderStatusColor,
  getOrderStatusLabel,
} from './orderPresentation';

interface OrderDetailProps {
  visible: boolean;
  orderId?: string;
  fallbackOrder?: Order;
  reloadToken?: number;
  onClose: () => void;
  onRetry?: () => void;
  onViewUser?: (order: Order) => void;
  onViewProduct?: (order: Order) => void;
  onViewCoinTransaction?: (order: Order) => void;
  canRemark?: boolean;
  savingRemark?: boolean;
  onSaveRemark?: (remark: string) => void;
}

function formatDateTime(value: string | null | undefined, language: string): string {
  if (!value) {
    return '-';
  }

  return formatLocalizedDateTime(value, language);
}

export const OrderDetail = ({
  visible,
  orderId,
  fallbackOrder,
  reloadToken = 0,
  onClose,
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
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setDetailOrder(fallbackOrder);
      setLoadError(null);
      return;
    }

    setDetailOrder(fallbackOrder);
  }, [fallbackOrder, visible]);

  useEffect(() => {
    if (!visible || !orderId) {
      return;
    }

    let cancelled = false;

    const loadOrder = async () => {
      try {
        setLoading(true);
        setLoadError(null);
        const result = await adminGetOrder(orderId);
        if (cancelled) {
          return;
        }

        setDetailOrder(result);
      } catch (error) {
        if (cancelled) {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : t('orders.detail.loadFailed');
        setLoadError(errorMessage);
        setDetailOrder((current) => current ?? fallbackOrder);
        message.error(errorMessage);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadOrder();

    return () => {
      cancelled = true;
    };
  }, [fallbackOrder, orderId, reloadToken, t, visible]);

  const currentOrder = detailOrder ?? fallbackOrder;

  useEffect(() => {
    if (!visible || !currentOrder) {
      return;
    }

    setAdminRemark(currentOrder.voAdminRemark ?? '');
  }, [currentOrder, visible]);

  const normalizedSavedRemark = useMemo(
    () => ((currentOrder?.voAdminRemark ?? '').trim()),
    [currentOrder?.voAdminRemark],
  );
  const normalizedEditingRemark = adminRemark.trim();
  const canSaveRemark = canRemark
    && !!onSaveRemark
    && normalizedEditingRemark !== normalizedSavedRemark
    && !savingRemark;

  return (
    <Modal
      title={t('orders.detail.title')}
      isOpen={visible}
      onClose={onClose}
      closeLabel={t('orders.detail.close')}
      size="large"
      footer={
        <Space wrap>
          {currentOrder && onViewUser ? (
            <Button onClick={() => onViewUser(currentOrder)}>
              {t('orders.action.viewUser')}
            </Button>
          ) : null}
          {currentOrder && onViewProduct ? (
            <Button onClick={() => onViewProduct(currentOrder)}>
              {t('orders.action.viewProduct')}
            </Button>
          ) : null}
          {currentOrder?.voCoinTransactionId && onViewCoinTransaction ? (
            <Button onClick={() => onViewCoinTransaction(currentOrder)}>
              {t('orders.detail.viewDebit')}
            </Button>
          ) : null}
          {currentOrder?.voCanRetryFulfillment === true && onRetry ? (
            <Button variant="primary" onClick={onRetry}>
              <SyncOutlined />
              {t('orders.detail.retry')}
            </Button>
          ) : null}
          {canRemark && onSaveRemark ? (
            <Button
              variant="primary"
              onClick={() => onSaveRemark(normalizedEditingRemark)}
              disabled={!canSaveRemark}
            >
              {t(savingRemark ? 'orders.detail.saving' : 'orders.detail.saveRemark')}
            </Button>
          ) : null}
          <Button onClick={onClose}>{t('orders.detail.close')}</Button>
        </Space>
      }
    >
      {!currentOrder ? (
        <div className="order-detail-empty">
          {loading ? t('orders.detail.loading') : loadError ?? t('orders.detail.notFound')}
        </div>
      ) : (
        <>
          <Descriptions bordered column={2}>
            <Descriptions.Item label={t('orders.detail.field.orderNo')} span={2}>
              {currentOrder.voOrderNo}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.status')}>
              <Tag color={getOrderStatusColor(currentOrder.voStatus)}>
                {getOrderStatusLabel(currentOrder, t)}
              </Tag>
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.failureStage')}>
              {getOrderFailureStageLabel(currentOrder.voFailureStage, t)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.createdAt')}>
              {formatDateTime(currentOrder.voCreateTime, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.paidAt')}>
              {formatDateTime(currentOrder.voPaidTime, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.completedAt')}>
              {formatDateTime(currentOrder.voCompletedTime, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.benefitExpiresAt')}>
              {formatDateTime(currentOrder.voBenefitExpiresAt, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.fixedExpiresAt')}>
              {formatDateTime(currentOrder.voFixedExpiresAt, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.cancelledAt')}>
              {formatDateTime(currentOrder.voCancelledTime, language)}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.cancelReason')} span={2}>
              {currentOrder.voCancelReason || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.failReason')} span={2}>
              {currentOrder.voFailReason ? (
                <span className="order-detail-danger">{currentOrder.voFailReason}</span>
              ) : '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.userId')}>
              {currentOrder.voUserId}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.user')}>
              {currentOrder.voUserName || t('orders.common.unknown')}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.productId')}>
              {currentOrder.voProductId}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.product')}>
              {currentOrder.voProductName}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.productType')}>
              <Tag color="blue">{getOrderProductTypeLabel(currentOrder.voProductType, t)}</Tag>
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.quantity')}>
              {currentOrder.voQuantity}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.unitPrice')}>
              <span className="order-detail-price">
                {formatLocalizedNumber(currentOrder.voUnitPrice, language)} {t('console.unit.carrot')}
              </span>
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.totalPrice')}>
              <span className="order-detail-price order-detail-price--total">
                {formatLocalizedNumber(currentOrder.voTotalPrice, language)} {t('console.unit.carrot')}
              </span>
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.coinTransactionId')} span={2}>
              {currentOrder.voCoinTransactionId || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.benefitId')}>
              {currentOrder.voGrantedBenefitId || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.inventoryId')}>
              {currentOrder.voGrantedInventoryId || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.duration')} span={2}>
              {getOrderDurationLabel(
                currentOrder,
                t,
                (value) => formatDateTime(value, language),
              )}
            </Descriptions.Item>

            <Descriptions.Item label={t('orders.detail.field.userRemark')} span={2}>
              {currentOrder.voUserRemark || '-'}
            </Descriptions.Item>
          </Descriptions>

          {loadError ? (
            <div className="order-detail-warning">
              {loadError}
            </div>
          ) : null}

          {canRemark ? (
            <div className="order-detail-remark">
              <div className="order-detail-remark__label">{t('orders.detail.adminRemark')}</div>
              <Input.TextArea
                rows={4}
                maxLength={500}
                showCount
                value={adminRemark}
                onChange={(event) => setAdminRemark(event.target.value)}
                placeholder={t('orders.detail.remarkPlaceholder')}
                disabled={savingRemark}
              />
            </div>
          ) : null}
        </>
      )}
    </Modal>
  );
};
