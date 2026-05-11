import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
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
} from '@radish/ui';
import {
  adminGetOrders,
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
  userId?: number;
  status?: OrderStatus;
  productId?: number;
  orderNo?: string;
  pageIndex?: number;
  pageSize?: number;
  openDetail?: boolean;
}): URLSearchParams {
  const searchParams = new URLSearchParams();
  const normalizedOrderNo = params.orderNo?.trim() ?? '';

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
  const [urlSearchParams, setUrlSearchParams] = useSearchParams();
  const queryUserId = parsePositiveIntQuery(urlSearchParams.get('userId'));
  const queryStatus = parseOrderStatusQuery(urlSearchParams.get('status'));
  const queryProductId = parsePositiveIntQuery(urlSearchParams.get('productId'));
  const queryOrderNo = (urlSearchParams.get('orderNo') ?? '').trim();
  const queryPageIndex = parsePositiveIntQuery(urlSearchParams.get('pageIndex')) ?? DEFAULT_PAGE_INDEX;
  const queryPageSize = parsePositiveIntQuery(urlSearchParams.get('pageSize')) ?? DEFAULT_PAGE_SIZE;
  const queryOpenDetail = parseBooleanQuery(urlSearchParams.get('openDetail'));
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canRetryOrder = usePermission(CONSOLE_PERMISSIONS.ordersRetry);
  const canRemarkOrder = usePermission(CONSOLE_PERMISSIONS.ordersRemark);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);

  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<number | undefined>();
  const [selectedOrderPreview, setSelectedOrderPreview] = useState<Order | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);

  const [confirmVisible, setConfirmVisible] = useState(false);
  const [retryOrder, setRetryOrder] = useState<Order | undefined>();
  const [savingRemark, setSavingRemark] = useState(false);

  const [draftUserId, setDraftUserId] = useState<number | undefined>(queryUserId);
  const [draftStatus, setDraftStatus] = useState<OrderStatus | undefined>(queryStatus);
  const [draftProductId, setDraftProductId] = useState<number | undefined>(queryProductId);
  const [draftOrderNo, setDraftOrderNo] = useState(queryOrderNo);

  const syncSearchParams = (params: {
    userId?: number;
    status?: OrderStatus;
    productId?: number;
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
        ? response.data.find((item) => item.voId === current.voId) ?? current
        : current);

      if (queryOpenDetail) {
        const targetOrder = queryOrderNo
          ? response.data.find((item) => item.voOrderNo === queryOrderNo)
          : response.data.length === 1 ? response.data[0] : undefined;

        if (targetOrder) {
          setSelectedOrderId(targetOrder.voId);
          setSelectedOrderPreview(targetOrder);
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
    navigate(`/users/${order.voUserId}`);
  };

  const handleViewProduct = (order: Order) => {
    navigate(`/products?productId=${order.voProductId}&openDetail=1`);
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
      if (selectedOrderId === retryOrder.voId) {
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
        item.voId === selectedOrderId
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
        <div>
          <div>{record.voUserName || '未知'}</div>
          <div style={{ fontSize: '12px', color: '#999' }}>ID: {record.voUserId}</div>
        </div>
      ),
    },
    {
      title: '商品',
      key: 'product',
      width: 200,
      render: (_: unknown, record: Order) => (
        <div>
          <div>{record.voProductName}</div>
          <div style={{ fontSize: '12px', color: '#999' }}>
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
        <span style={{ color: '#ff4d4f' }}>{price} 胡萝卜</span>
      ),
    },
    {
      title: '总价',
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: number) => (
        <span style={{ fontWeight: 'bold', color: '#ff4d4f' }}>
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
    <div className="order-list-page">
      <div className="page-header">
        <h2>订单管理</h2>
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Input
            placeholder="用户 ID"
            style={{ width: 120 }}
            type="number"
            value={draftUserId}
            onChange={(e) => setDraftUserId(e.target.value ? Number(e.target.value) : undefined)}
            onPressEnter={handleSearch}
          />

          <Select
            placeholder="订单状态"
            style={{ width: 120 }}
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
            placeholder="商品 ID"
            style={{ width: 120 }}
            type="number"
            value={draftProductId}
            onChange={(e) => setDraftProductId(e.target.value ? Number(e.target.value) : undefined)}
            onPressEnter={handleSearch}
          />

          <Input
            placeholder="订单号"
            style={{ width: 200 }}
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
        </Space>
      </div>

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
