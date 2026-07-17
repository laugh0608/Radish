import { AntSelect as Select, Button, DatePicker, Table } from '@radish/ui';
import type { RefObject } from 'react';
import type { Dayjs } from 'dayjs';
import type { ExpTransactionVo } from '@/api/experienceAdminApi';
import { createTransactionColumns } from './experienceAdminColumns';
import { EXPERIENCE_TRANSACTION_TYPES } from './experienceAdminHelpers';
import { useTranslation } from 'react-i18next';

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
  const { t, i18n } = useTranslation();
  const transactionColumns = createTransactionColumns(t, i18n.resolvedLanguage);
  const transactionTypeOptions = EXPERIENCE_TRANSACTION_TYPES.map((value) => ({
    value,
    label: t(`experience.transactionType.${value}`),
  }));

  return (
    <section className="admin-feature-card" ref={transactionSectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>{t('experience.transactions.title')}</h3>
          <p className="admin-feature-subtle">{t('experience.transactions.description')}</p>
        </div>
        <div className="experience-transaction-filters">
          <Select
            value={transactionTypeFilter}
            className="experience-filter-control"
            options={transactionTypeOptions}
            allowClear
            placeholder={t('experience.transactions.typePlaceholder')}
            onChange={(value) => {
              onTransactionTypeFilterChange(typeof value === 'string' && value.length > 0 ? value : undefined);
            }}
            disabled={!loadedUserId}
          />
          <DatePicker
            value={transactionStartDate}
            allowClear
            placeholder={t('experience.transactions.startDate')}
            className="experience-filter-control"
            disabled={!loadedUserId}
            onChange={onTransactionStartDateChange}
          />
          <DatePicker
            value={transactionEndDate}
            allowClear
            placeholder={t('experience.transactions.endDate')}
            className="experience-filter-control"
            disabled={!loadedUserId}
            onChange={onTransactionEndDateChange}
          />
          <Button disabled={!loadedUserId} onClick={onClearTransactionFilters}>
            {t('experience.actions.clearFilters')}
          </Button>
        </div>
      </div>

      {loadedUserId ? (
        <>
          {transactionReviewHint && (
            <div className="admin-feature-banner experience-section-gap-sm">
              {transactionReviewHint}
            </div>
          )}
          <Table<ExpTransactionVo>
            rowKey="voId"
            columns={transactionColumns}
            dataSource={transactions}
            loading={loadingTransactions}
            scroll={{ x: 1120 }}
            className="experience-table-spaced"
            pagination={{
              current: transactionPageIndex,
              pageSize: transactionPageSize,
              total: transactionTotal,
              showSizeChanger: true,
              showQuickJumper: true,
              showTotal: (total) => t('experience.transactions.total', { count: total }),
              onChange: onPageChange,
            }}
            locale={{
              emptyText: loadingTransactions ? t('experience.transactions.loading') : t('experience.transactions.empty'),
            }}
          />
        </>
      ) : (
        <div className="experience-empty-hint">
          {t('experience.transactions.queryFirst')}
        </div>
      )}
    </section>
  );
};
