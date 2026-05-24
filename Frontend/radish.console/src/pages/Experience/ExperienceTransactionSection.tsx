import { AntSelect as Select, Button, DatePicker, Table } from '@radish/ui';
import type { RefObject } from 'react';
import type { Dayjs } from 'dayjs';
import type { ExpTransactionVo } from '@/api/experienceAdminApi';
import { createTransactionColumns } from './experienceAdminColumns';
import { EXPERIENCE_TRANSACTION_TYPE_OPTIONS } from './experienceAdminHelpers';

type ExperienceTransactionSectionProps = {
  transactionSectionRef: RefObject<HTMLElement | null>;
  loadedUserId: string | null;
  transactions: ExpTransactionVo[];
  loadingTransactions: boolean;
  transactionTotal: number;
  transactionPageIndex: number;
  transactionPageSize: number;
  transactionTypeFilter?: string;
  transactionStartDate: Dayjs | null;
  transactionEndDate: Dayjs | null;
  transactionReviewHint: string | null;
  onTransactionTypeFilterChange: (value: string | undefined) => void;
  onTransactionStartDateChange: (value: Dayjs | null) => void;
  onTransactionEndDateChange: (value: Dayjs | null) => void;
  onClearTransactionFilters: () => void;
  onPageChange: (page: number, pageSize: number) => void;
};

export const ExperienceTransactionSection = ({
  transactionSectionRef,
  loadedUserId,
  transactions,
  loadingTransactions,
  transactionTotal,
  transactionPageIndex,
  transactionPageSize,
  transactionTypeFilter,
  transactionStartDate,
  transactionEndDate,
  transactionReviewHint,
  onTransactionTypeFilterChange,
  onTransactionStartDateChange,
  onTransactionEndDateChange,
  onClearTransactionFilters,
  onPageChange,
}: ExperienceTransactionSectionProps) => {
  const transactionColumns = createTransactionColumns();

  return (
    <section className="admin-feature-card" ref={transactionSectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>经验流水</h3>
          <p className="admin-feature-subtle">回看该用户最近的经验变动、管理员操作痕迹与升级轨迹，并支持按异常日期 / 类型快速复核。</p>
        </div>
        <div className="experience-transaction-filters">
          <Select
            value={transactionTypeFilter}
            style={{ width: '100%' }}
            options={EXPERIENCE_TRANSACTION_TYPE_OPTIONS}
            allowClear
            placeholder="筛选经验类型"
            onChange={(value) => {
              onTransactionTypeFilterChange(typeof value === 'string' && value.length > 0 ? value : undefined);
            }}
            disabled={!loadedUserId}
          />
          <DatePicker
            value={transactionStartDate}
            allowClear
            placeholder="开始日期"
            style={{ width: '100%' }}
            disabled={!loadedUserId}
            onChange={onTransactionStartDateChange}
          />
          <DatePicker
            value={transactionEndDate}
            allowClear
            placeholder="结束日期"
            style={{ width: '100%' }}
            disabled={!loadedUserId}
            onChange={onTransactionEndDateChange}
          />
          <Button disabled={!loadedUserId} onClick={onClearTransactionFilters}>
            清空筛选
          </Button>
        </div>
      </div>

      {loadedUserId ? (
        <>
          {transactionReviewHint && (
            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
              {transactionReviewHint}
            </div>
          )}
          <Table<ExpTransactionVo>
            rowKey="voId"
            columns={transactionColumns}
            dataSource={transactions}
            loading={loadingTransactions}
            scroll={{ x: 1120 }}
            style={{ marginTop: 20 }}
            pagination={{
              current: transactionPageIndex,
              pageSize: transactionPageSize,
              total: transactionTotal,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => `共 ${total} 条`,
              onChange: onPageChange,
            }}
            locale={{
              emptyText: loadingTransactions ? '经验流水加载中...' : '该用户暂无经验流水记录',
            }}
          />
        </>
      ) : (
        <div style={{ marginTop: 20, color: '#8c8c8c' }}>
          请先查询用户经验，再查看经验流水。
        </div>
      )}
    </section>
  );
};
