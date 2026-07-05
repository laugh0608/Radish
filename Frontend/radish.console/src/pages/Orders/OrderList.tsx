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
} from '@radish/ui';
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
  getOrderStatusColor,
  getProductTypeDisplay,
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

function isOrderStatus(record: Order, statusName: string, statusValue: number, displayKeyword?: string): boolean {
  const rawStatus = String(record.voStatus ?? '');
  if (rawStatus === statusName || rawStatus === String(statusValue)) {
    return true;
  }

  return displayKeyword ? record.voStatusDisplay?.includes(displayKeyword) === true : false;
}

function isFailedOrder(record: Order): boolean {
  return isOrderStatus(record, 'Failed', 5, '失败');
}

function isCompletedOrder(record: Order): boolean {
  return isOrderStatus(record, 'Completed', 2, '完成');
}

function isPendingOperationOrder(record: Order): boolean {
  return isOrderStatus(record, 'Pending', 0, '待')
    || isOrderStatus(record, 'Paid', 1, '已支付')
    || isFailedOrder(record);
}

export const OrderList = () => {
  useDocumentTitle('订单管理');
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
      message.error('加载订单列表失败');
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
      message.success('重试成功');
      await loadOrders();
      if (String(selectedOrderId) === String(retryOrder.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('OrderList', '重试失败:', error);
      message.error(error instanceof Error ? error.message : '重试失败');
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
      message.success('订单备注已保存');
      setDetailReloadToken((current) => current + 1);
    } catch (error) {
      log.error('OrderList', '保存订单备注失败:', error);
      message.error(error instanceof Error ? error.message : '保存订单备注失败');
    } finally {
      setSavingRemark(false);
    }
  };

  const columns: TableColumnsType<Order> = [
    {
      title: '订单号',
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
      fixed: 'left',
    },
    {
      title: '用户',
      key: 'user',
      width: 150,
      render: (_: unknown, record: Order) => (
        <div className="order-list-entity">
          <div className="order-list-entity__title">{record.voUserName || '未知'}</div>
          <div className="order-list-entity__meta">ID: {record.voUserId}</div>
        </div>
      ),
    },
    {
      title: '商品',
      key: 'product',
      width: 200,
      render: (_: unknown, record: Order) => (
        <div className="order-list-entity">
          <div className="order-list-entity__title">{record.voProductName}</div>
          <div className="order-list-entity__meta">
            {getProductTypeDisplay(record.voProductType)} | ID: {record.voProductId}
          </div>
        </div>
      ),
    },
    {
      title: '数量',
      dataIndex: 'voQuantity',
      key: 'voQuantity',
      width: 80,
      align: 'center',
    },
    {
      title: '单价',
      dataIndex: 'voUnitPrice',
      key: 'voUnitPrice',
      width: 120,
      render: (price: unknown) => (
        <span className="order-list-price">{normalizeOrderPrice(price)} 胡萝卜</span>
      ),
    },
    {
      title: '总价',
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: unknown) => (
        <span className="order-list-price order-list-price--total">
          {normalizeOrderPrice(price)} 胡萝卜
        </span>
      ),
    },
    {
      title: '状态',
      key: 'status',
      width: 120,
      render: (_: unknown, record: Order) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>
          {record.voStatusDisplay}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => new Date(time).toLocaleString('zh-CN'),
    },
    {
      title: '操作',
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
              查看用户
            </Button>
          ) : null}
          {canViewProducts ? (
            <Button
              variant="ghost"
              size="small"
              onClick={() => handleViewProduct(record)}
            >
              查看商品
            </Button>
          ) : null}
          <Button
            variant="ghost"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleViewDetail(record)}
          >
            详情
          </Button>
          {canRetryOrder && isFailedOrder(record) ? (
            <Button
              variant="ghost"
              size="small"
              icon={<SyncOutlined />}
              onClick={() => handleRetry(record)}
            >
              重试
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
        eyebrow="COMMERCE OPERATIONS"
        title="订单管理"
        description="查看商城订单、定位用户与商品，并处理发放失败重试和管理员备注。"
        icon={<FileTextOutlined />}
        status={(
          <ConsoleStatusChip tone={canRemarkOrder ? 'success' : 'neutral'}>
            {canRemarkOrder ? '可备注' : '只读'}
          </ConsoleStatusChip>
        )}
        actions={returnTo?.startsWith('/') ? (
          <Button onClick={() => navigate(returnTo)}>
            返回来源
          </Button>
        ) : undefined}
      />

      <ConsoleMetricGrid label="订单列表指标">
        <ConsoleMetricCard label="当前结果" value={total} description="当前筛选后的订单数量" tone="info" />
        <ConsoleMetricCard label="本页订单" value={orders.length} description="当前页可见订单" />
        <ConsoleMetricCard label="本页完成" value={completedOrders} description="本页已完成订单" tone="success" />
        <ConsoleMetricCard
          label="发放失败"
          value={failedOrders}
          description="本页需关注的失败订单"
          tone={failedOrders > 0 ? 'danger' : 'neutral'}
        />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label="订单运营任务流">
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>订单范围</strong>
          <p>{total} 条筛选结果，当前页 {orders.length} 条订单。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>失败优先</strong>
          <p>{failedOrders} 条发放失败，{pendingOperationOrders} 条处于待处理链路。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>交易金额</strong>
          <p>本页合计 {pageTotalPrice} 胡萝卜，用于核对订单摘要。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>留痕回流</strong>
          <p>{selectedOrderContext ? `当前聚焦 ${selectedOrderContext.voOrderNo}` : '选择订单后可回看用户、商品和资产流水。'}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选订单"
            description="按用户、订单状态、商品或订单号定位交易记录。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="order-list-filter-input order-list-filter-input--id"
                placeholder="用户 ID"
                type="number"
                value={draftUserId}
                onChange={(e) => setDraftUserId(e.target.value ? e.target.value.trim() : undefined)}
                onPressEnter={handleSearch}
              />

              <Select
                className="order-list-filter-select"
                placeholder="订单状态"
                allowClear
                value={draftStatus}
                onChange={setDraftStatus}
              >
                <Select.Option value={0}>待支付</Select.Option>
                <Select.Option value={1}>已支付</Select.Option>
                <Select.Option value={2}>已完成</Select.Option>
                <Select.Option value={3}>已取消</Select.Option>
                <Select.Option value={4}>已退款</Select.Option>
                <Select.Option value={5}>发放失败</Select.Option>
              </Select>

              <Input
                className="order-list-filter-input order-list-filter-input--id"
                placeholder="商品 ID"
                type="number"
                value={draftProductId}
                onChange={(e) => setDraftProductId(e.target.value ? e.target.value.trim() : undefined)}
                onPressEnter={handleSearch}
              />

              <Input
                className="order-list-filter-input"
                placeholder="订单号"
                value={draftOrderNo}
                onChange={(e) => setDraftOrderNo(e.target.value)}
                onPressEnter={handleSearch}
                suffix={<SearchOutlined />}
              />

              <Button variant="primary" icon={<SearchOutlined />} onClick={handleSearch}>
                搜索
              </Button>

              <Button icon={<ReloadOutlined />} onClick={handleReset}>
                重置
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
                showTotal: (itemTotal) => `共 ${itemTotal} 条`,
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
          <h3>订单运营摘要</h3>
          <p className="admin-feature-subtle">核对当前筛选、失败重试、管理员备注和用户 / 商品 / 资产回流。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部订单'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">待处理链路</span>
              <span className="admin-table-summary__value">{pendingOperationOrders} 条订单</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">本页成交额</span>
              <span className="admin-table-summary__value">{pageTotalPrice} 胡萝卜</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">失败重试</span>
              <span className="admin-table-summary__value">
                {canRetryOrder ? '可重试发放失败订单' : '无重试权限'}
              </span>
            </div>
          </div>

          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">当前订单</span>
              <span className="admin-table-summary__value">{selectedOrderContext?.voOrderNo ?? '未选择订单'}</span>
            </div>
            {selectedOrderContext ? (
              <>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">商品 / 用户</span>
                  <span className="admin-table-summary__value">
                    {selectedOrderContext.voProductName} · {selectedOrderContext.voUserName || `#${selectedOrderContext.voUserId}`}
                  </span>
                </div>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">状态 / 金额</span>
                  <span className="admin-table-summary__value">
                    {selectedOrderContext.voStatusDisplay} · {normalizeOrderPrice(selectedOrderContext.voTotalPrice)} 胡萝卜
                  </span>
                </div>
                {selectedOrderContext.voAdminRemark || selectedOrderContext.voFailReason ? (
                  <div className="admin-feature-inline-context">
                    <strong>{selectedOrderContext.voFailReason ? '失败原因' : '管理员备注'}</strong>
                    <span>{selectedOrderContext.voFailReason || selectedOrderContext.voAdminRemark}</span>
                  </div>
                ) : null}
                <div className="admin-feature-rail__actions">
                  <Button size="small" onClick={() => handleViewDetail(selectedOrderContext)}>
                    订单详情
                  </Button>
                  {canViewUsers ? (
                    <Button size="small" onClick={() => handleViewUser(selectedOrderContext)}>
                      查看用户
                    </Button>
                  ) : null}
                  {canViewProducts ? (
                    <Button size="small" onClick={() => handleViewProduct(selectedOrderContext)}>
                      查看商品
                    </Button>
                  ) : null}
                  {canViewCoins ? (
                    <Button size="small" onClick={() => handleViewCoinTransaction(selectedOrderContext)}>
                      资产流水
                    </Button>
                  ) : null}
                  {canRetryOrder && isFailedOrder(selectedOrderContext) ? (
                    <Button size="small" onClick={() => handleRetry(selectedOrderContext)}>
                      重试发放
                    </Button>
                  ) : null}
                </div>
              </>
            ) : (
              <p className="admin-feature-rail__empty">当前页暂无订单，调整筛选条件后会形成订单运营摘要。</p>
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
        title="确认重试"
        message={`确定要重新发放订单"${retryOrder?.voOrderNo}"的权益吗？`}
        onConfirm={handleConfirmRetry}
        onCancel={() => {
          setConfirmVisible(false);
          setRetryOrder(undefined);
        }}
      />
    </div>
  );
};
