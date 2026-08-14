import { useEffect, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router';
import { ApiResponseError } from '@radish/http';
import {
  AntInput as Input,
  AntSelect as Select,
  BottomSheet,
  Button,
  ConfirmDialog,
  EyeOutlined,
  LeftOutlined,
  ReloadOutlined,
  SearchOutlined,
  Table,
  Tag,
  formatLocalizedDateTime,
  formatLocalizedNumber,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { adminGetOrders, adminRemarkOrder, retryGrantBenefit } from '../../api/shopApi';
import type { Order, OrderStatus } from '../../api/types';
import { ConsolePageHeader, ConsoleStatusChip } from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { getLocalizedApiErrorMessage } from '@/utils/apiErrorMessage';
import { log } from '../../utils/logger';
import { OrderDetail, type OrderActionFeedback } from './OrderDetail';
import {
  getOrderProductTypeLabel,
  getOrderStatusColor,
  getOrderStatusLabel,
  summarizeOrderPage,
} from './orderPresentation';
import {
  DEFAULT_ORDER_PAGE_INDEX,
  DEFAULT_ORDER_PAGE_SIZE,
  buildOrderDetailSearchParams,
  buildOrderSearchParams,
  normalizeConsoleReturnTo,
  parseBooleanQuery,
  parseLongIdQuery,
  parseOrderStatusQuery,
  parsePositiveIntQuery,
} from './orderListUrlState';
import '../adminFeature.css';
import './OrderList.css';

interface OrderFilterControlsProps {
  userId?: string;
  status?: OrderStatus;
  productId?: string;
  orderNo: string;
  onUserIdChange: (value?: string) => void;
  onStatusChange: (value?: OrderStatus) => void;
  onProductIdChange: (value?: string) => void;
  onOrderNoChange: (value: string) => void;
  onSearch: () => void;
  onReset: () => void;
}

function normalizeOrderPrice(value: unknown): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string') {
    const parsed = Number(value.trim());
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function isConflictError(error: unknown): boolean {
  return error instanceof ApiResponseError
    && (error.httpStatus ?? error.statusCode ?? 0) === 409;
}

function OrderFilterControls({
  userId,
  status,
  productId,
  orderNo,
  onUserIdChange,
  onStatusChange,
  onProductIdChange,
  onOrderNoChange,
  onSearch,
  onReset,
}: OrderFilterControlsProps) {
  const { t } = useTranslation();

  return (
    <div className="order-filter-controls">
      <label className="order-filter-field">
        <span>{t('orders.list.filter.userId')}</span>
        <Input
          aria-label={t('orders.list.filter.userId')}
          inputMode="numeric"
          pattern="[0-9]*"
          placeholder={t('orders.list.filter.longIdPlaceholder')}
          type="text"
          value={userId}
          onChange={(event) => onUserIdChange(event.target.value || undefined)}
          onPressEnter={onSearch}
        />
      </label>

      <label className="order-filter-field">
        <span>{t('orders.list.filter.status')}</span>
        <Select
          aria-label={t('orders.list.filter.status')}
          placeholder={t('orders.list.filter.status')}
          allowClear
          value={status}
          onChange={onStatusChange}
        >
          <Select.Option value={0}>{t('orders.status.pending')}</Select.Option>
          <Select.Option value={1}>{t('orders.status.paid')}</Select.Option>
          <Select.Option value={2}>{t('orders.status.completed')}</Select.Option>
          <Select.Option value={3}>{t('orders.status.cancelled')}</Select.Option>
          <Select.Option value={4}>{t('orders.status.refunded')}</Select.Option>
          <Select.Option value={5}>{t('orders.status.failed')}</Select.Option>
        </Select>
      </label>

      <label className="order-filter-field">
        <span>{t('orders.list.filter.productId')}</span>
        <Input
          aria-label={t('orders.list.filter.productId')}
          inputMode="numeric"
          pattern="[0-9]*"
          placeholder={t('orders.list.filter.longIdPlaceholder')}
          type="text"
          value={productId}
          onChange={(event) => onProductIdChange(event.target.value || undefined)}
          onPressEnter={onSearch}
        />
      </label>

      <label className="order-filter-field order-filter-field--order-no">
        <span>{t('orders.list.filter.orderNo')}</span>
        <Input
          aria-label={t('orders.list.filter.orderNo')}
          placeholder={t('orders.list.filter.orderNoPlaceholder')}
          value={orderNo}
          onChange={(event) => onOrderNoChange(event.target.value)}
          onPressEnter={onSearch}
          suffix={<SearchOutlined />}
        />
      </label>

      <div className="order-filter-actions">
        <Button variant="primary" icon={<SearchOutlined />} onClick={onSearch}>
          {t('orders.list.search')}
        </Button>
        <Button icon={<ReloadOutlined />} onClick={onReset}>
          {t('orders.list.reset')}
        </Button>
      </div>
    </div>
  );
}

export const OrderList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('console.route.orders'));
  const navigate = useNavigate();
  const location = useLocation();
  const [urlSearchParams, setUrlSearchParams] = useSearchParams();
  const queryUserId = parseLongIdQuery(urlSearchParams.get('userId'));
  const queryOrderId = parseLongIdQuery(urlSearchParams.get('orderId'));
  const queryStatus = parseOrderStatusQuery(urlSearchParams.get('status'));
  const queryProductId = parseLongIdQuery(urlSearchParams.get('productId'));
  const queryOrderNo = (urlSearchParams.get('orderNo') ?? '').trim();
  const queryPageIndex = parsePositiveIntQuery(urlSearchParams.get('pageIndex')) ?? DEFAULT_ORDER_PAGE_INDEX;
  const queryPageSize = parsePositiveIntQuery(urlSearchParams.get('pageSize')) ?? DEFAULT_ORDER_PAGE_SIZE;
  const queryOpenDetail = parseBooleanQuery(urlSearchParams.get('openDetail'));
  const returnTo = normalizeConsoleReturnTo(urlSearchParams.get('returnTo'));

  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canRetryOrder = usePermission(CONSOLE_PERMISSIONS.ordersRetry);
  const canRemarkOrder = usePermission(CONSOLE_PERMISSIONS.ordersRemark);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);

  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [listError, setListError] = useState<string | null>(null);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | undefined>();
  const [selectedOrderPreview, setSelectedOrderPreview] = useState<Order | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);
  const [actionFeedback, setActionFeedback] = useState<OrderActionFeedback | null>(null);
  const [confirmVisible, setConfirmVisible] = useState(false);
  const [retryOrder, setRetryOrder] = useState<Order | undefined>();
  const [savingRemark, setSavingRemark] = useState(false);

  const [draftUserId, setDraftUserId] = useState<string | undefined>(queryUserId);
  const [draftStatus, setDraftStatus] = useState<OrderStatus | undefined>(queryStatus);
  const [draftProductId, setDraftProductId] = useState<string | undefined>(queryProductId);
  const [draftOrderNo, setDraftOrderNo] = useState(queryOrderNo);

  const activeFilterCount = [
    queryUserId,
    queryOrderId,
    queryStatus !== undefined ? 'status' : undefined,
    queryProductId,
    queryOrderNo,
  ].filter(Boolean).length;
  const pageSummary = summarizeOrderPage(orders);
  const hasWriteCapability = canRetryOrder || canRemarkOrder;
  const hasPreviousPage = queryPageIndex > DEFAULT_ORDER_PAGE_INDEX;
  const hasNextPage = queryPageIndex * queryPageSize < total;

  const syncSearchParams = (params: {
    orderId?: string;
    userId?: string;
    status?: OrderStatus;
    productId?: string;
    orderNo?: string;
    pageIndex?: number;
    pageSize?: number;
    openDetail?: boolean;
  }, replace: boolean = false) => {
    setUrlSearchParams(buildOrderSearchParams({ ...params, returnTo }), { replace });
  };

  useEffect(() => {
    setDraftUserId(queryUserId);
    setDraftStatus(queryStatus);
    setDraftProductId(queryProductId);
    setDraftOrderNo(queryOrderNo);
  }, [queryOrderNo, queryProductId, queryStatus, queryUserId]);

  const loadOrders = async () => {
    try {
      setLoading(true);
      const response = await adminGetOrders({
        userId: queryUserId,
        status: queryStatus,
        productId: queryProductId,
        orderNo: queryOrderNo || undefined,
        pageIndex: queryPageIndex,
        pageSize: queryPageSize,
      });

      setOrders(response.data);
      setTotal(response.dataCount);
      setListError(null);
      setSelectedOrderPreview((current) => current
        ? response.data.find((item) => String(item.voId) === String(current.voId)) ?? current
        : current);
    } catch (error) {
      log.error('OrderList', '加载订单列表失败:', error);
      setListError(getLocalizedApiErrorMessage(error, t, 'orders.list.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canViewOrders) {
      return;
    }

    void loadOrders();
    // Order list reads are scoped only by URL filters and pagination.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    queryUserId,
    queryStatus,
    queryProductId,
    queryOrderNo,
    queryPageIndex,
    queryPageSize,
    canViewOrders,
  ]);

  useEffect(() => {
    if (!queryOpenDetail) {
      setDetailVisible(false);
      setSelectedOrderId(undefined);
      setSelectedOrderPreview(undefined);
      setActionFeedback(null);
      return;
    }

    const targetOrder = queryOrderId
      ? orders.find((item) => String(item.voId) === queryOrderId)
      : queryOrderNo
      ? orders.find((item) => item.voOrderNo === queryOrderNo)
      : orders.length === 1 ? orders[0] : undefined;

    if (targetOrder) {
      setSelectedOrderId(String(targetOrder.voId));
      setSelectedOrderPreview(targetOrder);
      setDetailVisible(true);
      return;
    }

    if (queryOrderId) {
      setSelectedOrderId(queryOrderId);
      setSelectedOrderPreview((current) => (
        String(current?.voId ?? '') === queryOrderId ? current : undefined
      ));
      setDetailVisible(true);
    }
  }, [orders, queryOpenDetail, queryOrderId, queryOrderNo]);

  const handleSearch = (): boolean => {
    const normalizedUserId = parseLongIdQuery(draftUserId?.trim() || null);
    const normalizedProductId = parseLongIdQuery(draftProductId?.trim() || null);
    if ((draftUserId?.trim() && !normalizedUserId) || (draftProductId?.trim() && !normalizedProductId)) {
      message.warning(t('orders.list.filter.invalidLongId'));
      return false;
    }

    syncSearchParams({
      userId: normalizedUserId,
      status: draftStatus,
      productId: normalizedProductId,
      orderNo: draftOrderNo,
      pageIndex: DEFAULT_ORDER_PAGE_INDEX,
      pageSize: queryPageSize,
    });
    return true;
  };

  const handleReset = () => {
    syncSearchParams({
      pageIndex: DEFAULT_ORDER_PAGE_INDEX,
      pageSize: queryPageSize,
    });
    setFilterSheetOpen(false);
  };

  const handleViewDetail = (order: Order) => {
    setSelectedOrderId(String(order.voId));
    setSelectedOrderPreview(order);
    setActionFeedback(null);
    setDetailVisible(true);
    setUrlSearchParams(
      buildOrderDetailSearchParams({
        orderId: String(order.voId),
        userId: queryUserId,
        status: queryStatus,
        productId: queryProductId,
        orderNo: queryOrderNo,
        pageIndex: queryPageIndex,
        pageSize: queryPageSize,
        returnTo,
      }),
    );
  };

  const handleViewUser = (order: Order) => {
    const sourcePath = `${location.pathname}${location.search}`;
    navigate(`/users/${encodeURIComponent(String(order.voUserId))}?returnTo=${encodeURIComponent(sourcePath)}`);
  };

  const handleViewProduct = (order: Order) => {
    const sourcePath = `${location.pathname}${location.search}`;
    navigate(
      `/products?productId=${encodeURIComponent(String(order.voProductId))}&openDetail=1&returnTo=${encodeURIComponent(sourcePath)}`,
    );
  };

  const handleViewCoinTransaction = (order: Order) => {
    const sourcePath = `${location.pathname}${location.search}`;
    const searchParams = new URLSearchParams({
      userId: String(order.voUserId),
      transactionType: 'CONSUME',
      businessType: 'Order',
      businessId: String(order.voId),
      returnTo: sourcePath,
    });

    navigate(`/coins?${searchParams.toString()}`);
  };

  const handleRetry = (order: Order) => {
    if (!canRetryOrder || order.voCanRetryFulfillment !== true) {
      return;
    }

    setRetryOrder(order);
    setConfirmVisible(true);
  };

  const handleCloseDetail = () => {
    setDetailVisible(false);
    setSelectedOrderId(undefined);
    setSelectedOrderPreview(undefined);
    setActionFeedback(null);

    if (queryOpenDetail) {
      syncSearchParams({
        userId: queryUserId,
        status: queryStatus,
        productId: queryProductId,
        orderNo: queryOrderNo,
        pageIndex: queryPageIndex,
        pageSize: queryPageSize,
      }, true);
    }
  };

  const handleConfirmRetry = async () => {
    if (!retryOrder || !canRetryOrder || retryOrder.voCanRetryFulfillment !== true) {
      return;
    }

    const targetOrder = retryOrder;
    setConfirmVisible(false);
    setRetryOrder(undefined);

    try {
      await retryGrantBenefit(targetOrder.voId);
      setActionFeedback({ tone: 'success', message: t('orders.list.retrySuccess') });
      message.success(t('orders.list.retrySuccess'));
      await loadOrders();
      if (String(selectedOrderId) === String(targetOrder.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('OrderList', '重试发放失败:', error);
      const errorMessage = isConflictError(error)
        ? t('orders.retry.conflict')
        : getLocalizedApiErrorMessage(error, t, 'orders.list.retryFailed');
      setActionFeedback({ tone: 'danger', message: errorMessage });
      message.error(errorMessage);
    }
  };

  const handleSaveRemark = async (order: Order, remark: string): Promise<boolean> => {
    if (!canRemarkOrder || String(order.voId) !== String(selectedOrderId)) {
      return false;
    }

    try {
      setSavingRemark(true);
      await adminRemarkOrder(order.voId, remark);
      const normalizedRemark = remark.trim();
      const nextRemark = normalizedRemark || null;
      const updatedOrder = { ...order, voAdminRemark: nextRemark };

      setSelectedOrderPreview(updatedOrder);
      setOrders((current) => current.map((item) => (
        String(item.voId) === String(order.voId) ? { ...item, voAdminRemark: nextRemark } : item
      )));
      setActionFeedback({ tone: 'success', message: t('orders.list.remarkSaved') });
      message.success(t('orders.list.remarkSaved'));
      setDetailReloadToken((current) => current + 1);
      return true;
    } catch (error) {
      log.error('OrderList', '保存订单备注失败:', error);
      const errorMessage = isConflictError(error)
        ? t('orders.detail.remarkConflict')
        : getLocalizedApiErrorMessage(error, t, 'orders.list.remarkFailed');
      setActionFeedback({ tone: 'danger', message: errorMessage });
      message.error(errorMessage);
      return false;
    } finally {
      setSavingRemark(false);
    }
  };

  const handleOrderLoaded = (order: Order) => {
    setSelectedOrderPreview(order);
    setOrders((current) => current.map((item) => (
      String(item.voId) === String(order.voId) ? order : item
    )));
  };

  const changePage = (pageIndex: number, pageSize: number = queryPageSize) => {
    syncSearchParams({
      userId: queryUserId,
      status: queryStatus,
      productId: queryProductId,
      orderNo: queryOrderNo,
      pageIndex,
      pageSize,
    });
  };

  const columns: TableColumnsType<Order> = [
    {
      title: t('orders.column.orderNo'),
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 170,
      fixed: 'left',
      ellipsis: true,
    },
    {
      title: t('orders.column.createdAt'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 154,
      render: (time: string) => formatLocalizedDateTime(time, language),
    },
    {
      title: t('orders.column.user'),
      dataIndex: 'voUserName',
      key: 'user',
      width: 130,
      ellipsis: true,
      render: (_: unknown, record: Order) => record.voUserName || `#${record.voUserId}`,
    },
    {
      title: t('orders.column.product'),
      dataIndex: 'voProductName',
      key: 'product',
      width: 190,
      ellipsis: true,
      render: (_: unknown, record: Order) => (
        <span title={`${record.voProductName} × ${record.voQuantity}`}>
          {record.voProductName} × {record.voQuantity}
        </span>
      ),
    },
    {
      title: t('orders.column.totalPrice'),
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 116,
      align: 'right',
      render: (price: unknown) => (
        <span className="order-list-price order-list-price--total">
          {formatLocalizedNumber(normalizeOrderPrice(price), language)} {t('console.unit.carrot')}
        </span>
      ),
    },
    {
      title: t('orders.column.status'),
      key: 'status',
      width: 112,
      render: (_: unknown, record: Order) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>{getOrderStatusLabel(record, t)}</Tag>
      ),
    },
    {
      title: t('orders.column.action'),
      key: 'action',
      width: 98,
      fixed: 'right',
      align: 'right',
      render: (_: unknown, record: Order) => (
        <Button
          variant="ghost"
          size="small"
          icon={<EyeOutlined />}
          onClick={(event) => {
            event.stopPropagation();
            handleViewDetail(record);
          }}
        >
          {t('orders.action.detail')}
        </Button>
      ),
    },
  ];

  const filterControls = (
    <OrderFilterControls
      userId={draftUserId}
      status={draftStatus}
      productId={draftProductId}
      orderNo={draftOrderNo}
      onUserIdChange={setDraftUserId}
      onStatusChange={setDraftStatus}
      onProductIdChange={setDraftProductId}
      onOrderNoChange={setDraftOrderNo}
      onSearch={handleSearch}
      onReset={handleReset}
    />
  );

  return (
    <div className="admin-feature-page order-list-page">
      <ConsolePageHeader
        title={t('orders.list.title')}
        status={(
          <ConsoleStatusChip tone={hasWriteCapability ? 'success' : 'neutral'}>
            {t(hasWriteCapability ? 'orders.list.operator' : 'orders.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={returnTo?.startsWith('/') ? (
          <Button icon={<LeftOutlined />} onClick={() => navigate(returnTo)}>
            {t('orders.common.backToSource')}
          </Button>
        ) : undefined}
      />

      <section className="order-metric-strip" aria-label={t('orders.list.metrics.label')}>
        <div><span>{t('orders.list.metrics.results')}</span><strong>{formatLocalizedNumber(total, language)}</strong></div>
        <div><span>{t('orders.list.metrics.failed')}</span><strong>{pageSummary.failedCount}</strong></div>
        <div><span>{t('orders.list.metrics.retryable')}</span><strong>{pageSummary.retryableCount}</strong></div>
        <div>
          <span>{t('orders.list.metrics.amount')}</span>
          <strong>{formatLocalizedNumber(pageSummary.totalAmount, language)} <small>{t('console.unit.carrot')}</small></strong>
        </div>
      </section>

      {listError ? (
        <div className={`order-inline-state ${orders.length > 0 ? 'order-inline-state--warning' : 'order-inline-state--danger'}`} role="status">
          <div>
            <strong>{t(orders.length > 0 ? 'orders.list.staleTitle' : 'orders.list.errorTitle')}</strong>
            <span>{orders.length > 0 ? t('orders.list.staleDescription') : listError}</span>
          </div>
          <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadOrders()}>
            {t('orders.list.refresh')}
          </Button>
        </div>
      ) : null}

      <section className="order-list-surface">
        <div className="order-desktop-filter-bar">
          <div className="order-list-surface__heading">
            <div>
              <h2>{t('orders.list.toolbar.title')}</h2>
              <p>{t('orders.list.resultSummary', { total, visible: orders.length })}</p>
            </div>
            <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
              {activeFilterCount > 0
                ? t('orders.list.filterCount', { count: activeFilterCount })
                : t('orders.list.noFilters')}
            </ConsoleStatusChip>
          </div>
          {filterControls}
        </div>

        <div className="order-mobile-toolbar">
          <div>
            <strong>{t('orders.list.mobileResults', { count: total })}</strong>
            <span>{activeFilterCount > 0
              ? t('orders.list.filterCount', { count: activeFilterCount })
              : t('orders.list.noFilters')}</span>
          </div>
          <div className="order-mobile-toolbar__actions">
            <Button size="small" onClick={() => setFilterSheetOpen(true)}>
              {t('orders.list.filterAction')}
            </Button>
            <Button
              size="small"
              icon={<ReloadOutlined />}
              aria-label={t('orders.list.refresh')}
              disabled={loading}
              onClick={() => void loadOrders()}
            >
              {t('orders.list.refresh')}
            </Button>
          </div>
        </div>

        <div className={detailVisible ? 'order-list-workspace order-list-workspace--detail' : 'order-list-workspace'}>
          <main className="order-list-results">
            <div className="order-desktop-table">
              <Table
                columns={columns}
                dataSource={orders}
                rowKey="voId"
                loading={loading}
                locale={{
                  emptyText: (
                    <div className="order-empty-state">
                      <strong>{t('orders.list.emptyTitle')}</strong>
                      <span>{t('orders.list.emptyDescription')}</span>
                      {activeFilterCount > 0 ? <Button size="small" onClick={handleReset}>{t('orders.list.resetFilters')}</Button> : null}
                    </div>
                  ),
                }}
                pagination={{
                  current: queryPageIndex,
                  pageSize: queryPageSize,
                  total,
                  showSizeChanger: true,
                  showQuickJumper: true,
                  showTotal: (itemTotal) => t('orders.list.pagination', { count: itemTotal }),
                  onChange: changePage,
                }}
                rowClassName={(record) => (
                  String(record.voId) === String(selectedOrderId) ? 'order-table-row--selected' : ''
                )}
                onRow={(record) => ({
                  tabIndex: 0,
                  'aria-label': t('orders.list.openOrder', { orderNo: record.voOrderNo }),
                  onClick: () => handleViewDetail(record),
                  onKeyDown: (event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault();
                      handleViewDetail(record);
                    }
                  },
                })}
                scroll={{ x: 970 }}
              />
            </div>

            <div className="order-mobile-list" aria-busy={loading}>
              {orders.length > 0 ? orders.map((order) => (
                <button
                  key={order.voId}
                  className="order-mobile-row"
                  type="button"
                  onClick={() => handleViewDetail(order)}
                  aria-label={t('orders.list.openOrder', { orderNo: order.voOrderNo })}
                >
                  <span className="order-mobile-row__object">
                    <strong>{order.voOrderNo}</strong>
                    <span>{formatLocalizedDateTime(order.voCreateTime, language)}</span>
                    <span>{order.voUserName || `#${order.voUserId}`}</span>
                  </span>
                  <span className="order-mobile-row__evidence">
                    <strong>{order.voProductName}</strong>
                    <span>
                      {getOrderProductTypeLabel(order.voProductType, t)} · {formatLocalizedNumber(normalizeOrderPrice(order.voUnitPrice), language)} {t('console.unit.carrot')}
                    </span>
                    <span title={order.voCoinTransactionId ?? undefined}>
                      {order.voCoinTransactionId
                        ? t('orders.list.paymentEvidence', { transactionId: order.voCoinTransactionId })
                        : t(order.voPaidTime ? 'orders.list.paidEvidence' : 'orders.list.noPaymentEvidence')}
                    </span>
                  </span>
                  <span className="order-mobile-row__result">
                    <strong>{formatLocalizedNumber(normalizeOrderPrice(order.voTotalPrice), language)}</strong>
                    <Tag color={getOrderStatusColor(order.voStatus)}>{getOrderStatusLabel(order, t)}</Tag>
                    <span>{t('orders.action.detail')} ›</span>
                  </span>
                </button>
              )) : !loading ? (
                <div className="order-empty-state">
                  <strong>{t('orders.list.emptyTitle')}</strong>
                  <span>{t('orders.list.emptyDescription')}</span>
                  {activeFilterCount > 0 ? <Button size="small" onClick={handleReset}>{t('orders.list.resetFilters')}</Button> : null}
                </div>
              ) : (
                <div className="order-mobile-loading">{t('orders.list.loading')}</div>
              )}

              {orders.length > 0 ? (
                <nav className="order-mobile-pagination" aria-label={t('orders.list.paginationLabel')}>
                  <Button size="small" disabled={!hasPreviousPage || loading} onClick={() => changePage(queryPageIndex - 1)}>
                    {t('orders.list.previousPage')}
                  </Button>
                  <span>{t('orders.list.currentPage', { page: queryPageIndex })}</span>
                  <Button size="small" disabled={!hasNextPage || loading} onClick={() => changePage(queryPageIndex + 1)}>
                    {t('orders.list.nextPage')}
                  </Button>
                </nav>
              ) : null}
            </div>
          </main>

          <OrderDetail
            visible={detailVisible}
            orderId={selectedOrderId}
            fallbackOrder={selectedOrderPreview}
            reloadToken={detailReloadToken}
            canRemark={canRemarkOrder}
            savingRemark={savingRemark}
            feedback={actionFeedback}
            onClose={handleCloseDetail}
            onReload={() => {
              setActionFeedback(null);
              setDetailReloadToken((current) => current + 1);
            }}
            onOrderLoaded={handleOrderLoaded}
            onRetry={canRetryOrder ? handleRetry : undefined}
            onViewUser={canViewUsers ? handleViewUser : undefined}
            onViewProduct={canViewProducts ? handleViewProduct : undefined}
            onViewCoinTransaction={canViewCoins ? handleViewCoinTransaction : undefined}
            onSaveRemark={handleSaveRemark}
          />
        </div>
      </section>

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('orders.list.closeFilters')}
        title={t('orders.list.filterAction')}
        height="auto"
        className="order-filter-sheet"
        footer={(
          <Button
            variant="primary"
            onClick={() => {
              if (handleSearch()) {
                setFilterSheetOpen(false);
              }
            }}
          >
            {t('orders.list.applyFilters')}
          </Button>
        )}
      >
        {filterControls}
      </BottomSheet>

      <ConfirmDialog
        isOpen={confirmVisible}
        title={t('orders.retry.title')}
        message={t('orders.retry.message', {
          orderNo: retryOrder?.voOrderNo ?? '-',
          stage: retryOrder ? getOrderStatusLabel(retryOrder, t) : '-',
        })}
        confirmText={t('orders.action.retryFulfillment')}
        cancelText={t('orders.detail.close')}
        onConfirm={handleConfirmRetry}
        onCancel={() => {
          setConfirmVisible(false);
          setRetryOrder(undefined);
        }}
      />
    </div>
  );
};
