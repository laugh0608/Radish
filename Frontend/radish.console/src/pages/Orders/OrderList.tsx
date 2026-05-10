import { useState, useEffect } from 'react';
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
  getOrderStatusDisplay,
  getOrderStatusColor,
  getProductTypeDisplay,
} from '../../api/shopApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import type { Order, OrderStatus } from '../../api/types';
import { OrderDetail } from './OrderDetail';
import { log } from '../../utils/logger';
import './OrderList.css';

export const OrderList = () => {
  useDocumentTitle('订单管理');
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canRetryOrder = usePermission(CONSOLE_PERMISSIONS.ordersRetry);
  const canRemarkOrder = usePermission(CONSOLE_PERMISSIONS.ordersRemark);

  // 详情弹窗状态
  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<Order | undefined>();

  // 确认对话框状态
  const [confirmVisible, setConfirmVisible] = useState(false);
  const [retryOrder, setRetryOrder] = useState<Order | undefined>();
  const [savingRemark, setSavingRemark] = useState(false);

  // 草稿筛选条件
  const [draftUserId, setDraftUserId] = useState<number | undefined>();
  const [draftStatus, setDraftStatus] = useState<OrderStatus | undefined>();
  const [draftProductId, setDraftProductId] = useState<number | undefined>();
  const [draftOrderNo, setDraftOrderNo] = useState('');

  // 已应用筛选条件
  const [searchParams, setSearchParams] = useState<{
    userId?: number;
    status?: OrderStatus;
    productId?: number;
    orderNo: string;
  }>({
    userId: undefined,
    status: undefined,
    productId: undefined,
    orderNo: '',
  });

  // 加载订单列表
  const loadOrders = async () => {
    try {
      setLoading(true);
      const response = await adminGetOrders({
        userId: searchParams.userId,
        status: searchParams.status,
        productId: searchParams.productId,
        orderNo: searchParams.orderNo || undefined,
        pageIndex,
        pageSize,
      });

      setOrders(response.data);
      setTotal(response.dataCount);
    } catch (error) {
      log.error('OrderList', '加载订单列表失败:', error);
      message.error('加载订单列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 初始化和筛选条件变化时加载
  useEffect(() => {
    if (!canViewOrders) {
      return;
    }

    void loadOrders();
  }, [pageIndex, pageSize, searchParams, canViewOrders]);

  // 搜索
  const handleSearch = () => {
    setPageIndex(1);
    setSearchParams({
      userId: draftUserId,
      status: draftStatus,
      productId: draftProductId,
      orderNo: draftOrderNo.trim(),
    });
  };

  // 重置筛选
  const handleReset = () => {
    setDraftUserId(undefined);
    setDraftStatus(undefined);
    setDraftProductId(undefined);
    setDraftOrderNo('');
    setPageIndex(1);
    setSearchParams({
      userId: undefined,
      status: undefined,
      productId: undefined,
      orderNo: '',
    });
  };

  // 查看详情
  const handleViewDetail = (order: Order) => {
    setSelectedOrder(order);
    setDetailVisible(true);
  };

  // 重试失败订单
  const handleRetry = (order: Order) => {
    setRetryOrder(order);
    setConfirmVisible(true);
  };

  // 确认重试
  const handleConfirmRetry = async () => {
    if (!retryOrder) return;

    try {
      await retryGrantBenefit(retryOrder.voId);
      message.success('重试成功');
      loadOrders();
    } catch (error) {
      log.error('OrderList', '重试失败:', error);
      message.error('重试失败');
    } finally {
      setConfirmVisible(false);
      setRetryOrder(undefined);
    }
  };

  const handleSaveRemark = async (remark: string) => {
    if (!selectedOrder) {
      return;
    }

    try {
      setSavingRemark(true);
      await adminRemarkOrder(selectedOrder.voId, remark);
      const normalizedRemark = remark.trim();
      const nextRemark = normalizedRemark || null;

      setSelectedOrder((current) => current
        ? {
            ...current,
            voAdminRemark: nextRemark,
          }
        : current);
      setOrders((current) => current.map((item) => (
        item.voId === selectedOrder.voId
          ? {
              ...item,
              voAdminRemark: nextRemark,
            }
          : item
      )));
      message.success('订单备注已保存');
    } catch (error) {
      log.error('OrderList', '保存订单备注失败:', error);
      message.error(error instanceof Error ? error.message : '保存订单备注失败');
    } finally {
      setSavingRemark(false);
    }
  };

  // 表格列定义
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
      width: 150,
      fixed: 'right',
      render: (_: unknown, record: Order) => (
        <Space size="small">
          <Button
            variant="ghost"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleViewDetail(record)}
          >
            详情
          </Button>
          {canRetryOrder && record.voStatus === 'Failed' && (
            <Button
              variant="ghost"
              size="small"
              icon={<SyncOutlined />}
              onClick={() => handleRetry(record)}
            >
              重试
            </Button>
          )}
        </Space>
      ),
    },
  ];

  // 如果正在加载且没有数据，显示骨架屏
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
          current: pageIndex,
          pageSize: pageSize,
          total: total,
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total) => `共 ${total} 条`,
          onChange: (page, size) => {
            setPageIndex(page);
            setPageSize(size);
          },
        }}
        scroll={{ x: 1400 }}
      />

      <OrderDetail
        visible={detailVisible}
        order={selectedOrder}
        canRemark={canRemarkOrder}
        savingRemark={savingRemark}
        onClose={() => {
          setDetailVisible(false);
          setSelectedOrder(undefined);
        }}
        onRetry={() => {
          if (selectedOrder) {
            handleRetry(selectedOrder);
          }
        }}
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
