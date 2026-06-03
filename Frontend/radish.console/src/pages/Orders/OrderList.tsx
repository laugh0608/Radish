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
import type { Order, OrderStatus } from '../../api/types';
import { OrderDetail } from './OrderDetail';
import { log } from '../../utils/logger';
import '../adminFeature.css';
import './OrderList.css';

const DEFAULT_PAGE_INDEX = 1;
const DEFAULT_PAGE_SIZE = 20;

function parsePositiveIntQuery(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function parseLongIdQuery(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return /^\d+$/u.test(trimmed) ? trimmed : undefined;
}

function parseOrderStatusQuery(value: string | null): OrderStatus | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 && parsed <= 5
    ? parsed as OrderStatus
    : undefined;
}

function parseBooleanQuery(value: string | null): boolean {
  return value === '1' || value === 'true';
}

function buildOrderSearchParams(params: {
  orderId?: string;
  userId?: string;
  status?: OrderStatus;
  productId?: string;
  orderNo?: string;
  pageIndex?: number;
  pageSize?: number;
  openDetail?: boolean;
}): URLSearchParams {
  const searchParams = new URLSearchParams();
  const normalizedOrderNo = params.orderNo?.trim() ?? '';

  if (params.orderId !== undefined) {
    searchParams.set('orderId', params.orderId.toString());
  }

  if (params.userId !== undefined) {
    searchParams.set('userId', params.userId.toString());
  }

  if (params.status !== undefined) {
    searchParams.set('status', params.status.toString());
  }

  if (params.productId !== undefined) {
    searchParams.set('productId', params.productId.toString());
  }

  if (normalizedOrderNo) {
    searchParams.set('orderNo', normalizedOrderNo);
  }

  if ((params.pageIndex ?? DEFAULT_PAGE_INDEX) !== DEFAULT_PAGE_INDEX) {
    searchParams.set('pageIndex', String(params.pageIndex));
  }

  if ((params.pageSize ?? DEFAULT_PAGE_SIZE) !== DEFAULT_PAGE_SIZE) {
    searchParams.set('pageSize', String(params.pageSize));
  }

  if (params.openDetail) {
    searchParams.set('openDetail', '1');
  }

  return searchParams;
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
  const queryPageIndex = parsePositiveIntQuery(urlSearchParams.get('pageIndex')) ?? DEFAULT_PAGE_INDEX;
  const queryPageSize = parsePositiveIntQuery(urlSearchParams.get('pageSize')) ?? DEFAULT_PAGE_SIZE;
  const queryOpenDetail = parseBooleanQuery(urlSearchParams.get('openDetail'));
  const returnTo = urlSearchParams.get('returnTo');
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
  const failedOrders = orders.filter((order) => order.voStatus === 'Failed').length;
  const completedOrders = orders.filter((order) => order.voStatus === 'Completed').length;
  const pageTotalPrice = orders.reduce((sum, order) => sum + order.voTotalPrice, 0);

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
    setUrlSearchParams(buildOrderSearchParams(params), { replace });
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
      pageIndex: DEFAULT_PAGE_INDEX,
      pageSize: queryPageSize,
    });
  };

  const handleReset = () => {
    syncSearchParams({
      pageIndex: DEFAULT_PAGE_INDEX,
      pageSize: queryPageSize,
    });
  };

  const handleViewDetail = (order: Order) => {
    setSelectedOrderId(order.voId);
    setSelectedOrderPreview(order);
    setDetailVisible(true);
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
      render: (price: number) => (
        <span className="order-list-price">{price} 胡萝卜</span>
      ),
    },
    {
      title: '总价',
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: number) => (
        <span className="order-list-price order-list-price--total">
          {price} 胡萝卜
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
          {canRetryOrder && record.voStatus === 'Failed' ? (
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
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <FileTextOutlined /> 订单管理
            </h2>
            <p className="admin-feature-subtle">查看商城订单、定位用户与商品，并处理发放失败重试和管理员备注。</p>
          </div>
          <Space wrap>
            {returnTo?.startsWith('/') ? (
              <Button onClick={() => navigate(returnTo)}>
                返回来源
              </Button>
            ) : null}
            <Tag>{canRemarkOrder ? '可备注' : '只读'}</Tag>
          </Space>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="订单列表指标">
        <div className="admin-feature-metric">
          当前结果
          <strong>{total}</strong>
        </div>
        <div className="admin-feature-metric">
          本页订单
          <strong>{orders.length}</strong>
        </div>
        <div className="admin-feature-metric">
          本页完成
          <strong>{completedOrders}</strong>
        </div>
        <div className="admin-feature-metric">
          发放失败
          <strong>{failedOrders}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label="订单筛选">
            <div className="admin-table-toolbar__title">
              <span>筛选订单</span>
              <Tag>{activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}</Tag>
            </div>
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
          </section>

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
          <h3>订单摘要</h3>
          <p className="admin-feature-subtle">用于核对当前 URL 查询条件、分页规模和失败订单处理入口。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部订单'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分页规模</span>
              <span className="admin-table-summary__value">{queryPageSize} 条 / 页</span>
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
        onRetry={() => {
          if (selectedOrderPreview) {
            handleRetry(selectedOrderPreview);
          }
        }}
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
