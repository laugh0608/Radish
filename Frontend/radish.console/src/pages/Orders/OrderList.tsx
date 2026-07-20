import { useEffect, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  TableSkeleton,
  Table,
  Button,
  AntInput as Input,
  AntSelect as Select,
  Space,
  Tag,
  message,
  ConfirmDialog,
  type TableColumnsType,
  formatLocalizedDateTime,
  formatLocalizedNumber,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import {
  EyeOutlined,
  ReloadOutlined,
  SearchOutlined,
  SyncOutlined,
  FileTextOutlined,
} from '@radish/ui';
import {
  adminGetOrders,
  adminGetOrder,
  adminRemarkOrder,
  retryGrantBenefit,
} from '../../api/shopApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import type { Order, OrderStatus } from '../../api/types';
import { OrderDetail } from './OrderDetail';
import {
  getOrderProductTypeLabel,
  getOrderStatusColor,
  getOrderStatusLabel,
  isOrderInStatus,
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
import { log } from '../../utils/logger';
import '../adminFeature.css';
import './OrderList.css';

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

function isFailedOrder(record: Order): boolean {
  return isOrderInStatus(record, 'Failed');
}

function isCompletedOrder(record: Order): boolean {
  return isOrderInStatus(record, 'Completed');
}

function canRetryOrderFulfillment(record: Order): boolean {
  return record.voCanRetryFulfillment === true;
}

function isPendingOperationOrder(record: Order): boolean {
  return isOrderInStatus(record, 'Pending')
    || isOrderInStatus(record, 'Paid')
    || isFailedOrder(record);
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
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canRetryOrder = usePermission(CONSOLE_PERMISSIONS.ordersRetry);
  const canRemarkOrder = usePermission(CONSOLE_PERMISSIONS.ordersRemark);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);

  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | undefined>();
  const [selectedOrderPreview, setSelectedOrderPreview] = useState<Order | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);

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
  const failedOrders = orders.filter(isFailedOrder).length;
  const completedOrders = orders.filter(isCompletedOrder).length;
  const pendingOperationOrders = orders.filter(isPendingOperationOrder).length;
  const pageTotalPrice = orders.reduce((sum, order) => sum + normalizeOrderPrice(order.voTotalPrice), 0);
  const selectedOrderContext = selectedOrderPreview ?? orders.find(isFailedOrder) ?? orders[0] ?? null;

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
  }, [queryUserId, queryStatus, queryProductId, queryOrderNo]);

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
      setSelectedOrderPreview((current) => current
        ? response.data.find((item) => String(item.voId) === String(current.voId)) ?? current
        : current);

      if (queryOpenDetail) {
        const targetOrder = queryOrderId
          ? response.data.find((item) => String(item.voId) === queryOrderId)
          : queryOrderNo
          ? response.data.find((item) => item.voOrderNo === queryOrderNo)
          : response.data.length === 1 ? response.data[0] : undefined;

        if (targetOrder) {
          setSelectedOrderId(targetOrder.voId);
          setSelectedOrderPreview(targetOrder);
          setDetailVisible(true);
        } else if (queryOrderId) {
          const detail = await adminGetOrder(queryOrderId);
          setSelectedOrderId(detail.voId);
          setSelectedOrderPreview(detail);
          setDetailVisible(true);
        }
      }
    } catch (error) {
      log.error('OrderList', '加载订单列表失败:', error);
      message.error(error instanceof Error ? error.message : t('orders.list.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canViewOrders) {
      return;
    }

    void loadOrders();
    // Order list loading is URL-state scoped; loadOrders also opens detail from the fetched page and should not replay for unrelated handler captures.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    queryUserId,
    queryOrderId,
    queryStatus,
    queryProductId,
    queryOrderNo,
    queryPageIndex,
    queryPageSize,
    queryOpenDetail,
    canViewOrders,
  ]);

  const handleSearch = () => {
    syncSearchParams({
      userId: draftUserId,
      status: draftStatus,
      productId: draftProductId,
      orderNo: draftOrderNo,
      pageIndex: DEFAULT_ORDER_PAGE_INDEX,
      pageSize: queryPageSize,
    });
  };

  const handleReset = () => {
    syncSearchParams({
      pageIndex: DEFAULT_ORDER_PAGE_INDEX,
      pageSize: queryPageSize,
    });
  };

  const handleViewDetail = (order: Order) => {
    setSelectedOrderId(order.voId);
    setSelectedOrderPreview(order);
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
    const returnTo = `${location.pathname}${location.search}`;
    navigate(`/users/${encodeURIComponent(String(order.voUserId))}?returnTo=${encodeURIComponent(returnTo)}`);
  };

  const handleViewProduct = (order: Order) => {
    const returnTo = `${location.pathname}${location.search}`;
    navigate(
      `/products?productId=${encodeURIComponent(String(order.voProductId))}&openDetail=1&returnTo=${encodeURIComponent(returnTo)}`,
    );
  };

  const handleViewCoinTransaction = (order: Order) => {
    const returnTo = `${location.pathname}${location.search}`;
    const searchParams = new URLSearchParams({
      userId: String(order.voUserId),
      transactionType: 'CONSUME',
      businessType: 'Order',
      businessId: String(order.voId),
      returnTo,
    });

    navigate(`/coins?${searchParams.toString()}`);
  };

  const handleRetry = (order: Order) => {
    setRetryOrder(order);
    setConfirmVisible(true);
  };

  const handleCloseDetail = () => {
    setDetailVisible(false);
    setSelectedOrderId(undefined);
    setSelectedOrderPreview(undefined);

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
    if (!retryOrder) {
      return;
    }

    try {
      await retryGrantBenefit(retryOrder.voId);
      message.success(t('orders.list.retrySuccess'));
      await loadOrders();
      if (String(selectedOrderId) === String(retryOrder.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('OrderList', '重试失败:', error);
      message.error(error instanceof Error ? error.message : t('orders.list.retryFailed'));
    } finally {
      setConfirmVisible(false);
      setRetryOrder(undefined);
    }
  };

  const handleSaveRemark = async (remark: string) => {
    if (!selectedOrderId) {
      return;
    }

    try {
      setSavingRemark(true);
      await adminRemarkOrder(selectedOrderId, remark);
      const normalizedRemark = remark.trim();
      const nextRemark = normalizedRemark || null;

      setSelectedOrderPreview((current) => current
        ? {
            ...current,
            voAdminRemark: nextRemark,
          }
        : current);
      setOrders((current) => current.map((item) => (
        String(item.voId) === String(selectedOrderId)
          ? {
              ...item,
              voAdminRemark: nextRemark,
            }
          : item
      )));
      message.success(t('orders.list.remarkSaved'));
      setDetailReloadToken((current) => current + 1);
    } catch (error) {
      log.error('OrderList', '保存订单备注失败:', error);
      message.error(error instanceof Error ? error.message : t('orders.list.remarkFailed'));
    } finally {
      setSavingRemark(false);
    }
  };

  const columns: TableColumnsType<Order> = [
    {
      title: t('orders.column.orderNo'),
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
      fixed: 'left',
    },
    {
      title: t('orders.column.user'),
      key: 'user',
      width: 150,
      render: (_: unknown, record: Order) => (
        <div className="order-list-entity">
          <div className="order-list-entity__title">{record.voUserName || t('orders.common.unknown')}</div>
          <div className="order-list-entity__meta">ID: {record.voUserId}</div>
        </div>
      ),
    },
    {
      title: t('orders.column.product'),
      key: 'product',
      width: 200,
      render: (_: unknown, record: Order) => (
        <div className="order-list-entity">
          <div className="order-list-entity__title">{record.voProductName}</div>
          <div className="order-list-entity__meta">
            {getOrderProductTypeLabel(record.voProductType, t)} | ID: {record.voProductId}
          </div>
        </div>
      ),
    },
    {
      title: t('orders.column.quantity'),
      dataIndex: 'voQuantity',
      key: 'voQuantity',
      width: 80,
      align: 'center',
    },
    {
      title: t('orders.column.unitPrice'),
      dataIndex: 'voUnitPrice',
      key: 'voUnitPrice',
      width: 120,
      render: (price: unknown) => (
        <span className="order-list-price">{formatLocalizedNumber(normalizeOrderPrice(price), language)} {t('console.unit.carrot')}</span>
      ),
    },
    {
      title: t('orders.column.totalPrice'),
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: unknown) => (
        <span className="order-list-price order-list-price--total">
          {formatLocalizedNumber(normalizeOrderPrice(price), language)} {t('console.unit.carrot')}
        </span>
      ),
    },
    {
      title: t('orders.column.status'),
      key: 'status',
      width: 120,
      render: (_: unknown, record: Order) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>
          {getOrderStatusLabel(record, t)}
        </Tag>
      ),
    },
    {
      title: t('orders.column.createdAt'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatLocalizedDateTime(time, language),
    },
    {
      title: t('orders.column.action'),
      key: 'action',
      width: 280,
      fixed: 'right',
      render: (_: unknown, record: Order) => (
        <Space size="small" wrap>
          {canViewUsers ? (
            <Button
              variant="ghost"
              size="small"
              onClick={() => handleViewUser(record)}
            >
              {t('orders.action.viewUser')}
            </Button>
          ) : null}
          {canViewProducts ? (
            <Button
              variant="ghost"
              size="small"
              onClick={() => handleViewProduct(record)}
            >
              {t('orders.action.viewProduct')}
            </Button>
          ) : null}
          <Button
            variant="ghost"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleViewDetail(record)}
          >
            {t('orders.action.detail')}
          </Button>
          {canRetryOrder && canRetryOrderFulfillment(record) ? (
            <Button
              variant="ghost"
              size="small"
              icon={<SyncOutlined />}
              onClick={() => handleRetry(record)}
            >
              {t('orders.action.retry')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  if (loading && orders.length === 0) {
    return <TableSkeleton rows={10} columns={6} showFilters={true} showActions={true} />;
  }

  return (
    <div className="admin-feature-page order-list-page">
      <ConsolePageHeader
        eyebrow={t('orders.list.eyebrow')}
        title={t('orders.list.title')}
        description={t('orders.list.description')}
        icon={<FileTextOutlined />}
        status={(
          <ConsoleStatusChip tone={canRemarkOrder ? 'success' : 'neutral'}>
            {t(canRemarkOrder ? 'orders.list.canRemark' : 'orders.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={returnTo?.startsWith('/') ? (
          <Button onClick={() => navigate(returnTo)}>
            {t('orders.common.backToSource')}
          </Button>
        ) : undefined}
      />

      <ConsoleMetricGrid label={t('orders.list.metrics.label')}>
        <ConsoleMetricCard label={t('orders.list.metrics.results')} value={total} description={t('orders.list.metrics.resultsDescription')} tone="info" />
        <ConsoleMetricCard label={t('orders.list.metrics.page')} value={orders.length} description={t('orders.list.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('orders.list.metrics.completed')} value={completedOrders} description={t('orders.list.metrics.completedDescription')} tone="success" />
        <ConsoleMetricCard
          label={t('orders.list.metrics.failed')}
          value={failedOrders}
          description={t('orders.list.metrics.failedDescription')}
          tone={failedOrders > 0 ? 'danger' : 'neutral'}
        />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('orders.list.flow.label')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('orders.list.flow.scopeTitle')}</strong>
          <p>{t('orders.list.flow.scope', { total, visible: orders.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('orders.list.flow.failedTitle')}</strong>
          <p>{t('orders.list.flow.failed', { failed: failedOrders, pending: pendingOperationOrders })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('orders.list.flow.amountTitle')}</strong>
          <p>{t('orders.list.flow.amount', { amount: formatLocalizedNumber(pageTotalPrice, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('orders.list.flow.traceTitle')}</strong>
          <p>{selectedOrderContext
            ? t('orders.list.flow.focused', { orderNo: selectedOrderContext.voOrderNo })
            : t('orders.list.flow.select')}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('orders.list.toolbar.title')}
            description={t('orders.list.toolbar.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('orders.list.filterCount', { count: activeFilterCount }) : t('orders.list.noFilters')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="order-list-filter-input order-list-filter-input--id"
                placeholder={t('orders.list.filter.userId')}
                type="number"
                value={draftUserId}
                onChange={(e) => setDraftUserId(e.target.value ? e.target.value.trim() : undefined)}
                onPressEnter={handleSearch}
              />

              <Select
                className="order-list-filter-select"
                placeholder={t('orders.list.filter.status')}
                allowClear
                value={draftStatus}
                onChange={setDraftStatus}
              >
                <Select.Option value={0}>{t('orders.status.pending')}</Select.Option>
                <Select.Option value={1}>{t('orders.status.paid')}</Select.Option>
                <Select.Option value={2}>{t('orders.status.completed')}</Select.Option>
                <Select.Option value={3}>{t('orders.status.cancelled')}</Select.Option>
                <Select.Option value={4}>{t('orders.status.refunded')}</Select.Option>
                <Select.Option value={5}>{t('orders.status.fulfillmentFailed')}</Select.Option>
              </Select>

              <Input
                className="order-list-filter-input order-list-filter-input--id"
                placeholder={t('orders.list.filter.productId')}
                type="number"
                value={draftProductId}
                onChange={(e) => setDraftProductId(e.target.value ? e.target.value.trim() : undefined)}
                onPressEnter={handleSearch}
              />

              <Input
                className="order-list-filter-input"
                placeholder={t('orders.list.filter.orderNo')}
                value={draftOrderNo}
                onChange={(e) => setDraftOrderNo(e.target.value)}
                onPressEnter={handleSearch}
                suffix={<SearchOutlined />}
              />

              <Button variant="primary" icon={<SearchOutlined />} onClick={handleSearch}>
                {t('orders.list.search')}
              </Button>

              <Button icon={<ReloadOutlined />} onClick={handleReset}>
                {t('orders.list.reset')}
              </Button>
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table
              columns={columns}
              dataSource={orders}
              rowKey="voId"
              loading={loading}
              pagination={{
                current: queryPageIndex,
                pageSize: queryPageSize,
                total: total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (itemTotal) => t('orders.list.pagination', { count: itemTotal }),
                onChange: (page, size) => {
                  syncSearchParams({
                    userId: queryUserId,
                    status: queryStatus,
                    productId: queryProductId,
                    orderNo: queryOrderNo,
                    pageIndex: page,
                    pageSize: size,
                  });
                },
              }}
              scroll={{ x: 1600 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('orders.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('orders.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('orders.summary.scope')}</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? t('orders.summary.filterCount', { count: activeFilterCount }) : t('orders.summary.all')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('orders.summary.pending')}</span>
              <span className="admin-table-summary__value">{t('orders.list.orderCount', { count: pendingOperationOrders })}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('orders.summary.pageAmount')}</span>
              <span className="admin-table-summary__value">{formatLocalizedNumber(pageTotalPrice, language)} {t('console.unit.carrot')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('orders.summary.retry')}</span>
              <span className="admin-table-summary__value">
                {t(canRetryOrder ? 'orders.summary.canRetry' : 'orders.summary.noRetry')}
              </span>
            </div>
          </div>

          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('orders.summary.current')}</span>
              <span className="admin-table-summary__value">{selectedOrderContext?.voOrderNo ?? t('orders.summary.notSelected')}</span>
            </div>
            {selectedOrderContext ? (
              <>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">{t('orders.summary.productUser')}</span>
                  <span className="admin-table-summary__value">
                    {selectedOrderContext.voProductName} · {selectedOrderContext.voUserName || `#${selectedOrderContext.voUserId}`}
                  </span>
                </div>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">{t('orders.summary.statusAmount')}</span>
                  <span className="admin-table-summary__value">
                    {getOrderStatusLabel(selectedOrderContext, t)} · {formatLocalizedNumber(normalizeOrderPrice(selectedOrderContext.voTotalPrice), language)} {t('console.unit.carrot')}
                  </span>
                </div>
                {selectedOrderContext.voAdminRemark || selectedOrderContext.voFailReason ? (
                  <div className="admin-feature-inline-context">
                    <strong>{t(selectedOrderContext.voFailReason ? 'orders.summary.failureReason' : 'orders.summary.adminRemark')}</strong>
                    <span>{selectedOrderContext.voFailReason || selectedOrderContext.voAdminRemark}</span>
                  </div>
                ) : null}
                <div className="admin-feature-rail__actions">
                  <Button size="small" onClick={() => handleViewDetail(selectedOrderContext)}>
                    {t('orders.detail.title')}
                  </Button>
                  {canViewUsers ? (
                    <Button size="small" onClick={() => handleViewUser(selectedOrderContext)}>
                      {t('orders.action.viewUser')}
                    </Button>
                  ) : null}
                  {canViewProducts ? (
                    <Button size="small" onClick={() => handleViewProduct(selectedOrderContext)}>
                      {t('orders.action.viewProduct')}
                    </Button>
                  ) : null}
                  {canViewCoins ? (
                    <Button size="small" onClick={() => handleViewCoinTransaction(selectedOrderContext)}>
                      {t('orders.action.coinTransaction')}
                    </Button>
                  ) : null}
                  {canRetryOrder && canRetryOrderFulfillment(selectedOrderContext) ? (
                    <Button size="small" onClick={() => handleRetry(selectedOrderContext)}>
                      {t('orders.action.retryFulfillment')}
                    </Button>
                  ) : null}
                </div>
              </>
            ) : (
              <p className="admin-feature-rail__empty">{t('orders.summary.empty')}</p>
            )}
          </div>
        </aside>
      </div>

      <OrderDetail
        visible={detailVisible}
        orderId={selectedOrderId}
        fallbackOrder={selectedOrderPreview}
        reloadToken={detailReloadToken}
        canRemark={canRemarkOrder}
        savingRemark={savingRemark}
        onClose={handleCloseDetail}
        onRetry={canRetryOrder ? () => {
          if (selectedOrderPreview) {
            handleRetry(selectedOrderPreview);
          }
        } : undefined}
        onViewUser={canViewUsers ? handleViewUser : undefined}
        onViewProduct={canViewProducts ? handleViewProduct : undefined}
        onViewCoinTransaction={canViewCoins ? handleViewCoinTransaction : undefined}
        onSaveRemark={handleSaveRemark}
      />

      <ConfirmDialog
        isOpen={confirmVisible}
        title={t('orders.retry.title')}
        message={t('orders.retry.message', { orderNo: retryOrder?.voOrderNo ?? '-' })}
        confirmText={t('orders.action.retry')}
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
