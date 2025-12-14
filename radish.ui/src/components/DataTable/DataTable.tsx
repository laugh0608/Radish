import React from 'react';
import { Table, Empty } from 'antd';
import type { TableProps as AntTableProps, TableColumnsType } from 'antd';
import type { PagedResponse } from '../api/types';

/**
 * DataTable 组件属性
 */
export interface DataTableProps<T> extends Omit<AntTableProps<T>, 'dataSource' | 'pagination' | 'columns'> {
  /** 列配置 */
  columns: TableColumnsType<T>;
  /** 数据源（支持普通数组或分页数据） */
  dataSource?: T[] | PagedResponse<T>;
  /** 是否加载中 */
  loading?: boolean;
  /** 空状态描述 */
  emptyText?: string;
  /** 是否显示分页 */
  showPagination?: boolean;
  /** 当前页码（受控模式） */
  currentPage?: number;
  /** 每页数量（受控模式） */
  pageSize?: number;
  /** 分页变化回调 */
  onPageChange?: (page: number, pageSize: number) => void;
  /** 行的唯一键字段名 */
  rowKey?: string | ((record: T) => string);
}

/**
 * 判断是否为分页数据
 */
function isPagedResponse<T>(data: any): data is PagedResponse<T> {
  return (
    data &&
    typeof data === 'object' &&
    'data' in data &&
    'page' in data &&
    'pageSize' in data &&
    'dataCount' in data &&
    Array.isArray(data.data)
  );
}

/**
 * 统一的数据表格组件
 *
 * @example
 * ```tsx
 * // 普通数组
 * <DataTable
 *   columns={columns}
 *   dataSource={users}
 *   loading={loading}
 * />
 *
 * // 分页数据
 * <DataTable
 *   columns={columns}
 *   dataSource={pagedData}
 *   onPageChange={(page, pageSize) => loadData(page, pageSize)}
 * />
 * ```
 */
export function DataTable<T extends Record<string, any>>({
  columns,
  dataSource,
  loading = false,
  emptyText = '暂无数据',
  showPagination = true,
  currentPage,
  pageSize: propPageSize,
  onPageChange,
  rowKey = 'id',
  ...restProps
}: DataTableProps<T>) {
  // 解析数据源
  const data = isPagedResponse<T>(dataSource) ? dataSource.data : dataSource || [];
  const isPaged = isPagedResponse<T>(dataSource);

  // 分页配置
  const paginationConfig = showPagination && isPaged
    ? {
        current: currentPage || dataSource.page,
        pageSize: propPageSize || dataSource.pageSize,
        total: dataSource.dataCount,
        showSizeChanger: true,
        showQuickJumper: true,
        showTotal: (total: number) => `共 ${total} 条`,
        pageSizeOptions: ['10', '20', '50', '100'],
        onChange: onPageChange,
      }
    : showPagination && !isPaged && data.length > 10
    ? {
        showSizeChanger: true,
        showQuickJumper: true,
        showTotal: (total: number) => `共 ${total} 条`,
        pageSizeOptions: ['10', '20', '50', '100'],
      }
    : false;

  return (
    <Table<T>
      columns={columns}
      dataSource={data}
      loading={loading}
      pagination={paginationConfig}
      rowKey={rowKey}
      locale={{
        emptyText: <Empty description={emptyText} />,
      }}
      {...restProps}
    />
  );
}
