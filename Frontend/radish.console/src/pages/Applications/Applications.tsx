import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntModal as Modal,
  BottomSheet,
  Button,
  Popconfirm,
  Space,
  Table,
  Tag,
  formatLocalizedDateTime,
  message,
  type TableColumnsType,
} from '@radish/ui';
import {
  AppstoreOutlined,
  DeleteOutlined,
  EditOutlined,
  KeyOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@radish/ui';
import { clientApi } from '@/api/clients';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import type { ClientSecretResult, OidcClient } from '@/types/oidc';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { log } from '@/utils/logger';
import { ApplicationForm } from './ApplicationForm';
import {
  ApplicationSecretResult,
  type ApplicationSecretDisclosure,
} from './ApplicationSecretResult';
import {
  DEFAULT_APPLICATION_LIST_QUERY,
  parseApplicationListQuery,
  serializeApplicationListQuery,
  type ApplicationListQuery,
} from './applicationListUrlState';
import '../adminFeature.css';
import './Applications.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

export const Applications = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('applications.documentTitle'));
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseApplicationListQuery(searchParams), [searchParams]);
  const [keywordDraft, setKeywordDraft] = useState(query.keyword);
  const [clients, setClients] = useState<OidcClient[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [formApplication, setFormApplication] = useState<OidcClient>();
  const [busyAction, setBusyAction] = useState<string>();
  const [secretDisclosure, setSecretDisclosure] = useState<ApplicationSecretDisclosure>();
  const requestSequence = useRef(0);
  const snapshotQueryKey = useRef<string | undefined>(undefined);
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);
  const canCreateApplication = usePermission(CONSOLE_PERMISSIONS.applicationsCreate);
  const canEditApplication = usePermission(CONSOLE_PERMISSIONS.applicationsEdit);
  const canDeleteApplication = usePermission(CONSOLE_PERMISSIONS.applicationsDelete);
  const canResetApplicationSecret = usePermission(CONSOLE_PERMISSIONS.applicationsResetSecret);
  const actionsAreAuthoritative = readState === 'ready';
  const pageCount = Math.max(1, Math.ceil(total / query.pageSize));
  const activeClientCount = clients.filter((client) => client.status !== 'Disabled').length;
  const confidentialClientCount = clients.filter((client) => client.clientType === 'confidential').length;

  useEffect(() => {
    setKeywordDraft(query.keyword);
  }, [query.keyword]);

  const updateQuery = useCallback((nextQuery: ApplicationListQuery) => {
    setSearchParams(serializeApplicationListQuery(nextQuery), { replace: true });
  }, [setSearchParams]);

  const loadClients = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    const queryKey = serializeApplicationListQuery(query).toString();
    const hasCurrentSnapshot = snapshotQueryKey.current === queryKey;
    requestSequence.current = requestId;

    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setClients([]);
        setTotal(0);
      }

      const result = await clientApi.getClients({
        page: query.page,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
      });
      if (requestSequence.current !== requestId) return;
      if (!result.ok || !result.data) {
        throw new Error(result.message || t('applications.feedback.loadFailed'));
      }

      const responsePageCount = Math.max(1, result.data.pageCount);
      if (result.data.data.length === 0 && result.data.dataCount > 0 && query.page > responsePageCount) {
        updateQuery({ ...query, page: responsePageCount });
        return;
      }

      setClients(result.data.data);
      setTotal(result.data.dataCount);
      snapshotQueryKey.current = queryKey;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) return;
      log.error('Applications', '加载应用列表失败', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(error instanceof Error ? error.message : t('applications.feedback.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) setLoading(false);
    }
  }, [query, t, updateQuery]);

  useEffect(() => {
    if (canViewApplications) void loadClients();
  }, [canViewApplications, loadClients]);

  const handleApplyFilters = () => {
    updateQuery({ ...query, page: 1, keyword: keywordDraft.trim().slice(0, 100) });
    setFilterSheetOpen(false);
  };

  const handleResetFilters = () => {
    setKeywordDraft('');
    updateQuery({ ...DEFAULT_APPLICATION_LIST_QUERY, pageSize: query.pageSize });
    setFilterSheetOpen(false);
  };

  const handlePageChange = (page: number, pageSize = query.pageSize) => {
    updateQuery({ ...query, page, pageSize });
  };

  const handleCreate = () => {
    if (!canCreateApplication || !actionsAreAuthoritative) {
      message.error(t(!canCreateApplication
        ? 'applications.feedback.permissionDenied'
        : 'applications.feedback.authorityUnavailable'));
      return;
    }

    setFormMode('create');
    setFormApplication(undefined);
    setFormOpen(true);
  };

  const handleEdit = async (application: OidcClient) => {
    if (!canEditApplication || !actionsAreAuthoritative) {
      message.error(t(!canEditApplication
        ? 'applications.feedback.permissionDenied'
        : 'applications.feedback.authorityUnavailable'));
      return;
    }

    const actionKey = `edit:${application.id}`;
    try {
      setBusyAction(actionKey);
      const result = await clientApi.getClient(application.id);
      if (!result.ok || !result.data) {
        throw new Error(result.message || t('applications.feedback.detailUnavailable'));
      }
      setFormMode('edit');
      setFormApplication(result.data);
      setFormOpen(true);
    } catch (error) {
      log.error('Applications', '加载应用详情失败', error);
      message.error(error instanceof Error ? error.message : t('applications.feedback.detailUnavailable'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleDelete = async (application: OidcClient) => {
    if (!canDeleteApplication || !actionsAreAuthoritative) {
      message.error(t(!canDeleteApplication
        ? 'applications.feedback.permissionDenied'
        : 'applications.feedback.authorityUnavailable'));
      return;
    }

    const actionKey = `delete:${application.id}`;
    try {
      setBusyAction(actionKey);
      const result = await clientApi.deleteClient(application.id);
      if (!result.ok) {
        throw new Error(result.message || t('applications.feedback.deleteFailed'));
      }
      message.success(t('applications.feedback.deleted'));
      await loadClients();
    } catch (error) {
      log.error('Applications', '删除应用失败', error);
      message.error(error instanceof Error ? error.message : t('applications.feedback.deleteFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const rotateSecret = async (application: OidcClient) => {
    if (!canResetApplicationSecret || !actionsAreAuthoritative) {
      message.error(t(!canResetApplicationSecret
        ? 'applications.feedback.permissionDenied'
        : 'applications.feedback.authorityUnavailable'));
      return;
    }
    if (application.clientType !== 'confidential') {
      message.error(t('applications.feedback.publicSecretUnsupported'));
      return;
    }

    const actionKey = `secret:${application.id}`;
    try {
      setBusyAction(actionKey);
      const result = await clientApi.resetClientSecret(application.id);
      if (!result.ok || !result.data?.clientSecret) {
        throw new Error(result.message || t('applications.feedback.resetFailed'));
      }
      setSecretDisclosure({ operation: 'rotate', result: result.data });
      message.success(t('applications.feedback.resetSucceeded'));
    } catch (error) {
      log.error('Applications', '轮换客户端密钥失败', error);
      message.error(error instanceof Error ? error.message : t('applications.feedback.resetFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleResetSecret = (application: OidcClient) => {
    if (!canResetApplicationSecret || !actionsAreAuthoritative) {
      message.error(t(!canResetApplicationSecret
        ? 'applications.feedback.permissionDenied'
        : 'applications.feedback.authorityUnavailable'));
      return;
    }
    if (application.clientType !== 'confidential') {
      message.error(t('applications.feedback.publicSecretUnsupported'));
      return;
    }

    Modal.confirm({
      title: t('applications.secret.rotateConfirmTitle'),
      content: t('applications.secret.rotateConfirmDescription', { clientId: application.clientId }),
      okText: t('applications.secret.rotateConfirm'),
      cancelText: t('applications.form.cancel'),
      okButtonProps: { danger: true },
      onOk: () => rotateSecret(application),
    });
  };

  const handleFormSuccess = (secretResult?: ClientSecretResult) => {
    setFormOpen(false);
    setFormApplication(undefined);
    if (secretResult?.clientSecret) {
      setSecretDisclosure({ operation: 'create', result: secretResult });
    }
    void loadClients();
  };

  const renderClientIdentity = (application: OidcClient) => (
    <div className="applications-client-identity">
      <strong>{application.displayName || application.clientId}</strong>
      <span>{application.clientId}</span>
    </div>
  );

  const renderActions = (application: OidcClient) => {
    const editing = busyAction === `edit:${application.id}`;
    const rotating = busyAction === `secret:${application.id}`;
    const deleting = busyAction === `delete:${application.id}`;
    const anyBusy = busyAction !== undefined;
    return (
      <Space size="small" wrap>
        {canEditApplication ? (
          <Button
            variant="ghost"
            size="small"
            icon={<EditOutlined />}
            disabled={!actionsAreAuthoritative || anyBusy}
            onClick={() => void handleEdit(application)}
          >
            {t(editing ? 'applications.action.loadingDetail' : 'applications.action.edit')}
          </Button>
        ) : null}
        {canResetApplicationSecret ? (
          <Button
            variant="ghost"
            size="small"
            icon={<KeyOutlined />}
            disabled={!actionsAreAuthoritative || anyBusy || application.clientType !== 'confidential'}
            title={application.clientType === 'public' ? t('applications.feedback.publicSecretUnsupported') : undefined}
            onClick={() => handleResetSecret(application)}
          >
            {t(rotating ? 'applications.action.rotatingSecret' : 'applications.action.resetSecret')}
          </Button>
        ) : null}
        {canDeleteApplication ? (
          <Popconfirm
            title={t('applications.delete.confirm')}
            description={t('applications.delete.description', { clientId: application.clientId })}
            onConfirm={() => handleDelete(application)}
            okText={t('applications.delete.ok')}
            cancelText={t('applications.delete.cancel')}
            disabled={!actionsAreAuthoritative || anyBusy}
          >
            <Button
              variant="danger"
              size="small"
              icon={<DeleteOutlined />}
              disabled={!actionsAreAuthoritative || anyBusy}
            >
              {t(deleting ? 'applications.action.deleting' : 'applications.action.delete')}
            </Button>
          </Popconfirm>
        ) : null}
      </Space>
    );
  };

  const columns: TableColumnsType<OidcClient> = [
    {
      title: t('applications.column.application'),
      key: 'application',
      width: 260,
      render: (_, application) => renderClientIdentity(application),
    },
    {
      title: t('applications.column.clientType'),
      dataIndex: 'clientType',
      key: 'clientType',
      width: 130,
      render: (value: OidcClient['clientType']) => (
        <Tag color={value === 'confidential' ? 'blue' : 'default'}>
          {t(`applications.clientType.${value}`)}
        </Tag>
      ),
    },
    {
      title: t('applications.column.grantTypes'),
      dataIndex: 'grantTypes',
      key: 'grantTypes',
      width: 240,
      render: (values: string[]) => values.join(', ') || '-',
    },
    {
      title: t('applications.column.type'),
      dataIndex: 'type',
      key: 'type',
      width: 110,
      render: (value: OidcClient['type']) => (
        <Tag color={value === 'Internal' ? 'blue' : 'green'}>
          {t(value === 'Internal' ? 'applications.type.internal' : 'applications.type.thirdParty')}
        </Tag>
      ),
    },
    {
      title: t('applications.column.status'),
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (value: OidcClient['status']) => (
        <Tag color={value !== 'Disabled' ? 'success' : 'default'}>
          {t(value !== 'Disabled' ? 'applications.status.enabled' : 'applications.status.disabled')}
        </Tag>
      ),
    },
    {
      title: t('applications.column.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (value?: string) => value ? formatLocalizedDateTime(value, language) : '-',
    },
    {
      title: t('applications.column.actions'),
      key: 'actions',
      width: 300,
      fixed: 'right',
      render: (_, application) => renderActions(application),
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls applications-filter-controls">
      <Input
        allowClear
        maxLength={100}
        prefix={<SearchOutlined />}
        placeholder={t('applications.filter.keyword')}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={handleApplyFilters}
      />
      <div className="console-resource-filter-controls__actions">
        <Button onClick={handleResetFilters}>{t('applications.filter.reset')}</Button>
        <Button variant="primary" onClick={handleApplyFilters}>{t('applications.filter.search')}</Button>
      </div>
    </div>
  );

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert">
      <div>
        <strong>{t(readState === 'stale'
          ? 'applications.list.staleTitle'
          : 'applications.list.unavailableTitle')}</strong>
        <span>{t(readState === 'stale'
          ? 'applications.list.staleDescription'
          : 'applications.list.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadClients()}>{t('applications.action.retry')}</Button>
    </div>
  ) : null;

  return (
    <div className="admin-feature-page applications-page">
      <ConsolePageHeader
        eyebrow={t('applications.eyebrow')}
        title={t('applications.title')}
        description={t('applications.description')}
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canViewApplications ? 'success' : 'danger'}>
            {t(canViewApplications ? 'applications.status.viewable' : 'applications.status.noPermission')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} disabled={!canViewApplications} onClick={() => void loadClients()}>
              {t('applications.action.refresh')}
            </Button>
            {canCreateApplication ? (
              <Button
                variant="primary"
                icon={<PlusOutlined />}
                disabled={!actionsAreAuthoritative}
                onClick={handleCreate}
              >
                {t('applications.action.create')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('applications.metrics.label')}>
        <ConsoleMetricCard
          label={t('applications.metrics.results')}
          value={formatConsoleNumber(total, language)}
          description={t('applications.metrics.resultsDescription')}
          tone="info"
        />
        <ConsoleMetricCard
          label={t('applications.metrics.page')}
          value={formatConsoleNumber(clients.length, language)}
          description={t('applications.metrics.pageDescription')}
        />
        <ConsoleMetricCard
          label={t('applications.metrics.enabled')}
          value={formatConsoleNumber(activeClientCount, language)}
          description={t('applications.metrics.currentPageDescription')}
          tone="success"
        />
        <ConsoleMetricCard
          label={t('applications.metrics.confidential')}
          value={formatConsoleNumber(confidentialClientCount, language)}
          description={t('applications.metrics.currentPageDescription')}
          tone="warning"
        />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={(
          <ConsoleToolbar
            title={t('applications.list.title')}
            description={t('applications.list.description')}
            meta={(
              <ConsoleStatusChip tone={query.keyword ? 'info' : 'neutral'}>
                {t(query.keyword ? 'applications.filter.active' : 'applications.filter.none')}
              </ConsoleStatusChip>
            )}
          >
            {filterControls}
          </ConsoleToolbar>
        )}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy">
              <strong>{t('applications.list.total', { count: total })}</strong>
              <span>{t(query.keyword ? 'applications.filter.active' : 'applications.filter.none')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>
                {t('applications.filter.mobile')}
              </Button>
              <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadClients()}>
                {t('applications.action.retry')}
              </Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {readNotice}
            <Table<OidcClient>
              columns={columns}
              dataSource={clients}
              rowKey="id"
              loading={loading}
              scroll={{ x: 1420 }}
              pagination={{
                current: query.page,
                pageSize: query.pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('applications.list.total', { count }),
                onChange: handlePageChange,
              }}
            />
          </section>
        )}
        mobileList={(
          <>
            {readNotice}
            {loading && clients.length === 0 ? (
              <div className="console-resource-mobile-loading">{t('applications.list.mobileLoading')}</div>
            ) : null}
            {readState === 'ready' && clients.length === 0 ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('applications.list.emptyTitle')}</strong>
                <span>{t('applications.list.emptyDescription')}</span>
              </div>
            ) : null}
            {clients.map((application) => (
              <article className="console-resource-mobile-card applications-mobile-card" key={application.id}>
                <div className="console-resource-mobile-card__header">
                  {renderClientIdentity(application)}
                  <Tag color={application.clientType === 'confidential' ? 'blue' : 'default'}>
                    {t(`applications.clientType.${application.clientType}`)}
                  </Tag>
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('applications.column.grantTypes')}</span>
                    <strong>{application.grantTypes.join(', ') || '-'}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('applications.column.scopes')}</span>
                    <strong>{application.scopes.join(', ') || '-'}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('applications.column.status')}</span>
                    <strong>{t(application.status !== 'Disabled'
                      ? 'applications.status.enabled'
                      : 'applications.status.disabled')}</strong>
                  </div>
                </div>
                {application.description ? (
                  <p className="console-resource-mobile-card__description">{application.description}</p>
                ) : null}
                <div className="console-resource-mobile-card__footer">
                  <Tag color={application.type === 'Internal' ? 'blue' : 'green'}>
                    {t(application.type === 'Internal'
                      ? 'applications.type.internal'
                      : 'applications.type.thirdParty')}
                  </Tag>
                  {renderActions(application)}
                </div>
              </article>
            ))}
            {clients.length > 0 ? (
              <div className="console-resource-mobile-pagination">
                <Button
                  size="small"
                  disabled={query.page <= 1 || loading}
                  onClick={() => handlePageChange(query.page - 1)}
                >
                  {t('applications.list.previous')}
                </Button>
                <span>{t('applications.list.page', { current: query.page, total: pageCount })}</span>
                <Button
                  size="small"
                  disabled={query.page >= pageCount || loading}
                  onClick={() => handlePageChange(query.page + 1)}
                >
                  {t('applications.list.next')}
                </Button>
              </div>
            ) : null}
          </>
        )}
        context={(
          <>
            <h3>{t('applications.summary.title')}</h3>
            <p className="admin-feature-subtle">{t('applications.summary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('applications.summary.scopeLabel')}</span>
                <span className="admin-table-summary__value">
                  {t('applications.summary.scopeValue', { page: query.page, pageSize: query.pageSize })}
                </span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('applications.summary.writeAuthority')}</span>
                <span className="admin-table-summary__value">
                  {t(actionsAreAuthoritative
                    ? 'applications.summary.authorityReady'
                    : 'applications.summary.authorityFrozen')}
                </span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('applications.summary.secretPermission')}</span>
                <span className="admin-table-summary__value">
                  {t(canResetApplicationSecret
                    ? 'applications.summary.secretAllowed'
                    : 'applications.summary.secretDenied')}
                </span>
              </div>
            </div>
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('applications.filter.close')}
        title={t('applications.filter.title')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>

      <ApplicationForm
        visible={formOpen}
        mode={formMode}
        application={formApplication}
        canSubmit={formMode === 'create' ? canCreateApplication : canEditApplication}
        onCancel={() => {
          setFormOpen(false);
          setFormApplication(undefined);
        }}
        onSuccess={handleFormSuccess}
      />

      <ApplicationSecretResult
        disclosure={secretDisclosure}
        onClose={() => setSecretDisclosure(undefined)}
      />
    </div>
  );
};
