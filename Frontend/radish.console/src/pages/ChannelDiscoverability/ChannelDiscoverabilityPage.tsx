import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  Button,
  ClockCircleOutlined,
  EyeOutlined,
  ReloadOutlined,
  SearchOutlined,
  Space,
  Table,
  Tag,
  formatLocalizedDateTime,
  message,
  type TableColumnsType,
} from '@radish/ui';
import {
  getChannelDiscoverabilityHistory,
  getChannelDiscoverabilityPage,
  updateChannelDiscoverVisibility,
  type ChannelDiscoverabilityVo,
  type ChannelDiscoverVisibility,
  type ChannelDiscoverVisibilityEventVo,
} from '@/api/channelDiscoverabilityApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './ChannelDiscoverabilityPage.css';

type VisibilityFilter = 'all' | 'hidden' | 'summary';
type EnabledFilter = 'all' | 'enabled' | 'disabled';

function toVisibilityFilter(value: VisibilityFilter): ChannelDiscoverVisibility | undefined {
  if (value === 'summary') {
    return 1;
  }
  if (value === 'hidden') {
    return 0;
  }
  return undefined;
}

export const ChannelDiscoverabilityPage = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('channelDiscoverability.documentTitle'));
  const canView = usePermission(CONSOLE_PERMISSIONS.channelDiscoverabilityView);
  const canManage = usePermission(CONSOLE_PERMISSIONS.channelDiscoverabilityManage);

  const [items, setItems] = useState<ChannelDiscoverabilityVo[]>([]);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [visibilityFilter, setVisibilityFilter] = useState<VisibilityFilter>('all');
  const [enabledFilter, setEnabledFilter] = useState<EnabledFilter>('all');
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [mutationTarget, setMutationTarget] = useState<ChannelDiscoverabilityVo>();
  const [mutationVisibility, setMutationVisibility] = useState<ChannelDiscoverVisibility>(0);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [historyTarget, setHistoryTarget] = useState<ChannelDiscoverabilityVo>();
  const [history, setHistory] = useState<ChannelDiscoverVisibilityEventVo[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const initialLoadStarted = useRef(false);

  const loadPage = useCallback(async (targetPage: number, targetPageSize: number) => {
    try {
      setLoading(true);
      const result = await getChannelDiscoverabilityPage({
        pageIndex: targetPage,
        pageSize: targetPageSize,
        keyword: keyword.trim() || undefined,
        discoverVisibility: toVisibilityFilter(visibilityFilter),
        isEnabled: enabledFilter === 'all' ? undefined : enabledFilter === 'enabled',
        includeDeleted,
      });
      setItems(result.data);
      setTotal(result.dataCount);
      setPageIndex(result.page);
      setPageSize(result.pageSize);
    } catch (error) {
      log.error('ChannelDiscoverabilityPage', '加载频道公开摘要列表失败:', error);
      message.error(error instanceof Error ? error.message : t('channelDiscoverability.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [enabledFilter, includeDeleted, keyword, t, visibilityFilter]);

  useEffect(() => {
    if (!canView) {
      initialLoadStarted.current = false;
      return;
    }

    if (initialLoadStarted.current) {
      return;
    }

    initialLoadStarted.current = true;
    void loadPage(1, pageSize);
  }, [canView, loadPage, pageSize]);

  const summaryCount = items.filter((item) => item.voDiscoverVisibility === 1).length;
  const eligibleCount = items.filter((item) => item.voCanEnableSummary).length;
  const inactiveCount = items.filter((item) => !item.voIsEnabled || item.voIsDeleted).length;
  const activeFilterCount = [
    keyword.trim(),
    visibilityFilter !== 'all',
    enabledFilter !== 'all',
    includeDeleted,
  ].filter(Boolean).length;

  const visibilityLabel = (visibility: ChannelDiscoverVisibility) => t(
    visibility === 1
      ? 'channelDiscoverability.visibility.summary'
      : 'channelDiscoverability.visibility.hidden',
  );

  const openMutation = (record: ChannelDiscoverabilityVo) => {
    setMutationTarget(record);
    setMutationVisibility(record.voDiscoverVisibility === 1 ? 0 : 1);
    setReason('');
  };

  const submitMutation = async () => {
    if (!mutationTarget || !reason.trim()) {
      message.error(t('channelDiscoverability.feedback.reasonRequired'));
      return;
    }

    try {
      setSaving(true);
      const result = await updateChannelDiscoverVisibility(mutationTarget.voChannelId, {
        discoverVisibility: mutationVisibility,
        expectedVersion: mutationTarget.voDiscoverVisibilityVersion,
        reason: reason.trim(),
      });
      message.success(t(result.voChanged
        ? 'channelDiscoverability.feedback.updated'
        : 'channelDiscoverability.feedback.unchanged'));
      setMutationTarget(undefined);
      setReason('');
      await loadPage(pageIndex, pageSize);
    } catch (error) {
      log.error('ChannelDiscoverabilityPage', '更新频道公开摘要资格失败:', error);
      message.error(error instanceof Error ? error.message : t('channelDiscoverability.feedback.updateFailed'));
    } finally {
      setSaving(false);
    }
  };

  const openHistory = async (record: ChannelDiscoverabilityVo) => {
    setHistoryTarget(record);
    setHistory([]);
    setHistoryLoading(true);
    try {
      setHistory(await getChannelDiscoverabilityHistory(record.voChannelId));
    } catch (error) {
      log.error('ChannelDiscoverabilityPage', '加载频道公开摘要历史失败:', error);
      message.error(error instanceof Error ? error.message : t('channelDiscoverability.feedback.historyFailed'));
    } finally {
      setHistoryLoading(false);
    }
  };

  const columns: TableColumnsType<ChannelDiscoverabilityVo> = [
    {
      title: t('channelDiscoverability.table.channel'),
      key: 'channel',
      width: 260,
      render: (_, record) => (
        <div className="channel-discoverability-name">
          <span className="channel-discoverability-name__icon">{record.voIconEmoji || '＃'}</span>
          <span className="channel-discoverability-name__copy">
            <strong>{record.voName}</strong>
            <span>/{record.voSlug}</span>
          </span>
        </div>
      ),
    },
    {
      title: t('channelDiscoverability.table.visibility'),
      key: 'visibility',
      width: 130,
      render: (_, record) => (
        <Tag color={record.voDiscoverVisibility === 1 ? 'processing' : 'default'}>
          {visibilityLabel(record.voDiscoverVisibility)}
        </Tag>
      ),
    },
    {
      title: t('channelDiscoverability.table.eligibility'),
      key: 'eligibility',
      width: 210,
      render: (_, record) => record.voCanEnableSummary
        ? <Tag color="success">{t('channelDiscoverability.eligibility.ready')}</Tag>
        : (
            <Space size={[4, 4]} wrap>
              {record.voEligibilityIssues.map((issue) => (
                <Tag key={issue} color="warning">
                  {t(`channelDiscoverability.eligibility.${issue}`, { defaultValue: issue })}
                </Tag>
              ))}
            </Space>
          ),
    },
    {
      title: t('channelDiscoverability.table.lifecycle'),
      key: 'lifecycle',
      width: 120,
      render: (_, record) => record.voIsDeleted
        ? <Tag>{t('channelDiscoverability.lifecycle.deleted')}</Tag>
        : (
            <Tag color={record.voIsEnabled ? 'success' : 'error'}>
              {t(record.voIsEnabled
                ? 'channelDiscoverability.lifecycle.enabled'
                : 'channelDiscoverability.lifecycle.disabled')}
            </Tag>
          ),
    },
    {
      title: t('channelDiscoverability.table.lastActivity'),
      dataIndex: 'voLastMessageTime',
      key: 'voLastMessageTime',
      width: 180,
      render: (value?: string | null) => value ? formatLocalizedDateTime(value, language) : '-',
    },
    {
      title: t('channelDiscoverability.table.modifiedBy'),
      key: 'modifiedBy',
      width: 190,
      render: (_, record) => (
        <span>
          {record.voModifyBy || '-'}
          <br />
          {record.voModifyTime ? formatLocalizedDateTime(record.voModifyTime, language) : '-'}
        </span>
      ),
    },
    {
      title: t('channelDiscoverability.table.actions'),
      key: 'actions',
      width: 240,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small" wrap>
          <Button
            variant="ghost"
            size="small"
            icon={<ClockCircleOutlined />}
            onClick={() => { void openHistory(record); }}
          >
            {t('channelDiscoverability.actions.history')}
          </Button>
          {canManage ? (
            <Button
              variant={record.voDiscoverVisibility === 1 ? 'danger' : 'primary'}
              size="small"
              icon={<EyeOutlined />}
              disabled={record.voDiscoverVisibility === 0 && !record.voCanEnableSummary}
              onClick={() => openMutation(record)}
            >
              {t(record.voDiscoverVisibility === 1
                ? 'channelDiscoverability.actions.hide'
                : 'channelDiscoverability.actions.expose')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  const historyColumns: TableColumnsType<ChannelDiscoverVisibilityEventVo> = [
    {
      title: t('channelDiscoverability.history.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatLocalizedDateTime(value, language),
    },
    {
      title: t('channelDiscoverability.history.change'),
      key: 'change',
      width: 190,
      render: (_, record) => `${visibilityLabel(record.voFromVisibility)} → ${visibilityLabel(record.voToVisibility)}`,
    },
    {
      title: t('channelDiscoverability.history.reason'),
      dataIndex: 'voReason',
      key: 'voReason',
      ellipsis: true,
    },
    {
      title: t('channelDiscoverability.history.operator'),
      dataIndex: 'voActorName',
      key: 'voActorName',
      width: 150,
    },
    {
      title: t('channelDiscoverability.history.version'),
      dataIndex: 'voResultVersion',
      key: 'voResultVersion',
      width: 100,
      render: (value: number) => `v${value}`,
    },
  ];

  return (
    <div className="admin-feature-page channel-discoverability-page">
      <ConsolePageHeader
        eyebrow={t('channelDiscoverability.page.eyebrow')}
        title={t('channelDiscoverability.page.title')}
        description={t('channelDiscoverability.page.description')}
        icon={<EyeOutlined />}
        status={(
          <ConsoleStatusChip tone={canManage ? 'warning' : 'neutral'}>
            {t(canManage
              ? 'channelDiscoverability.page.writable'
              : 'channelDiscoverability.page.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <Button icon={<ReloadOutlined />} onClick={() => { void loadPage(pageIndex, pageSize); }}>
            {t('channelDiscoverability.actions.refresh')}
          </Button>
        )}
      />

      <ConsoleMetricGrid label={t('channelDiscoverability.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.total')} value={formatConsoleNumber(total, language)} description={t('channelDiscoverability.metrics.totalDescription')} tone="info" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.summary')} value={formatConsoleNumber(summaryCount, language)} description={t('channelDiscoverability.metrics.summaryDescription')} tone="warning" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.eligible')} value={formatConsoleNumber(eligibleCount, language)} description={t('channelDiscoverability.metrics.eligibleDescription')} tone="success" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.inactive')} value={formatConsoleNumber(inactiveCount, language)} description={t('channelDiscoverability.metrics.inactiveDescription')} />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('channelDiscoverability.filter.title')}
            description={t('channelDiscoverability.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {t('channelDiscoverability.filter.count', { count: activeFilterCount })}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                prefix={<SearchOutlined />}
                placeholder={t('channelDiscoverability.filter.keyword')}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
                onPressEnter={() => { void loadPage(1, pageSize); }}
              />
              <Select<VisibilityFilter>
                value={visibilityFilter}
                onChange={setVisibilityFilter}
                options={[
                  { value: 'all', label: t('channelDiscoverability.filter.allVisibility') },
                  { value: 'summary', label: t('channelDiscoverability.visibility.summary') },
                  { value: 'hidden', label: t('channelDiscoverability.visibility.hidden') },
                ]}
              />
              <Select<EnabledFilter>
                value={enabledFilter}
                onChange={setEnabledFilter}
                options={[
                  { value: 'all', label: t('channelDiscoverability.filter.allLifecycle') },
                  { value: 'enabled', label: t('channelDiscoverability.lifecycle.enabled') },
                  { value: 'disabled', label: t('channelDiscoverability.lifecycle.disabled') },
                ]}
              />
              <Select<'yes' | 'no'>
                value={includeDeleted ? 'yes' : 'no'}
                onChange={(value) => setIncludeDeleted(value === 'yes')}
                options={[
                  { value: 'no', label: t('channelDiscoverability.filter.hideDeleted') },
                  { value: 'yes', label: t('channelDiscoverability.filter.showDeleted') },
                ]}
              />
              <Button variant="primary" icon={<SearchOutlined />} onClick={() => { void loadPage(1, pageSize); }}>
                {t('channelDiscoverability.actions.search')}
              </Button>
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table<ChannelDiscoverabilityVo>
              rowKey="voChannelId"
              columns={columns}
              dataSource={items}
              loading={loading}
              scroll={{ x: 1350 }}
              pagination={{
                current: pageIndex,
                pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                onChange: (page, size) => { void loadPage(page, size); },
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('channelDiscoverability.boundary.title')}</h3>
          <p className="admin-feature-subtle">{t('channelDiscoverability.boundary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('channelDiscoverability.boundary.exposed')}</span>
              <span className="admin-table-summary__value">{t('channelDiscoverability.boundary.exposedValue')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('channelDiscoverability.boundary.excluded')}</span>
              <span className="admin-table-summary__value">{t('channelDiscoverability.boundary.excludedValue')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('channelDiscoverability.boundary.tenant')}</span>
              <span className="admin-table-summary__value">{t('channelDiscoverability.boundary.tenantValue')}</span>
            </div>
          </div>
        </aside>
      </div>

      <Modal
        title={t(mutationVisibility === 1
          ? 'channelDiscoverability.mutation.exposeTitle'
          : 'channelDiscoverability.mutation.hideTitle')}
        open={Boolean(mutationTarget)}
        onCancel={() => setMutationTarget(undefined)}
        onOk={() => { void submitMutation(); }}
        confirmLoading={saving}
        okText={t('channelDiscoverability.mutation.confirm')}
        cancelText={t('channelDiscoverability.mutation.cancel')}
        okButtonProps={{ danger: mutationVisibility === 0, disabled: !reason.trim() }}
        destroyOnHidden
      >
        <p className="channel-discoverability-modal-note">
          {mutationTarget
            ? t(mutationVisibility === 1
                ? 'channelDiscoverability.mutation.exposeDescription'
                : 'channelDiscoverability.mutation.hideDescription',
              { name: mutationTarget.voName })
            : null}
        </p>
        <div className="channel-discoverability-modal-field">
          <label htmlFor="channel-discoverability-reason">{t('channelDiscoverability.mutation.reason')}</label>
          <Input.TextArea
            id="channel-discoverability-reason"
            rows={4}
            maxLength={500}
            showCount
            value={reason}
            placeholder={t('channelDiscoverability.mutation.reasonPlaceholder')}
            onChange={(event) => setReason(event.target.value)}
          />
        </div>
      </Modal>

      <Modal
        title={t('channelDiscoverability.history.title')}
        open={Boolean(historyTarget)}
        onCancel={() => setHistoryTarget(undefined)}
        footer={null}
        width={920}
        destroyOnHidden
      >
        <div className="channel-discoverability-history-summary">
          <strong>{historyTarget?.voName}</strong>
          <span>/{historyTarget?.voSlug}</span>
        </div>
        <Table<ChannelDiscoverVisibilityEventVo>
          rowKey="voId"
          columns={historyColumns}
          dataSource={history}
          loading={historyLoading}
          pagination={false}
          scroll={{ x: 850 }}
        />
      </Modal>
    </div>
  );
};
