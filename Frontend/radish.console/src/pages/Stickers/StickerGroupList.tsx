import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  BottomSheet,
  Table,
  Button,
  Space,
  Tag,
  Popconfirm,
  message,
  AntInput as Input,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  SearchOutlined,
  AppstoreOutlined,
} from '@radish/ui';
import {
  deleteStickerGroup,
  getAdminStickerGroups,
  updateStickerGroupStatus,
  type StickerGroupVo,
} from '@/api/stickerApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { usePermission } from '@/hooks/usePermission';
import { getAvatarUrl } from '@/config/env';
import { ROUTES } from '@/router/routes';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { StickerGroupForm } from './StickerGroupForm';
import '../adminFeature.css';
import './StickerResource.css';
import './StickerGroupList.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

const normalizeKeyword = (value: string | null) => value?.trim().slice(0, 100) ?? '';

export const StickerGroupList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('stickers.group.documentTitle'));

  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const appliedKeyword = normalizeKeyword(searchParams.get('keyword'));
  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [groups, setGroups] = useState<StickerGroupVo[]>([]);
  const [keywordDraft, setKeywordDraft] = useState(appliedKeyword);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [busyAction, setBusyAction] = useState<string>();
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingGroup, setEditingGroup] = useState<StickerGroupVo>();
  const requestSequence = useRef(0);
  const hasSnapshot = useRef(false);
  const canViewStickers = usePermission(CONSOLE_PERMISSIONS.stickersView);
  const canCreateSticker = usePermission(CONSOLE_PERMISSIONS.stickersCreate);
  const canEditSticker = usePermission(CONSOLE_PERMISSIONS.stickersEdit);
  const canDeleteStickerPermission = usePermission(CONSOLE_PERMISSIONS.stickersDelete);
  const canToggleSticker = usePermission(CONSOLE_PERMISSIONS.stickersToggle);
  const actionsAreAuthoritative = readState === 'ready';

  useEffect(() => {
    setKeywordDraft(appliedKeyword);
  }, [appliedKeyword]);

  const loadGroups = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    requestSequence.current = requestId;
    try {
      setLoading(true);
      setReadState('loading');
      const data = await getAdminStickerGroups();
      if (requestSequence.current !== requestId) return;
      setGroups(data);
      hasSnapshot.current = true;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) return;
      log.error('StickerGroupList', '加载表情包分组失败:', error);
      setReadState(hasSnapshot.current ? 'stale' : 'unavailable');
      message.error(error instanceof Error ? error.message : t('stickers.group.feedback.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (canViewStickers) void loadGroups();
  }, [canViewStickers, loadGroups]);

  const filteredGroups = useMemo(() => {
    const normalized = appliedKeyword.toLowerCase();
    if (!normalized) return groups;
    return groups.filter((item) =>
      item.voName.toLowerCase().includes(normalized)
      || item.voCode.toLowerCase().includes(normalized)
      || (item.voDescription || '').toLowerCase().includes(normalized));
  }, [appliedKeyword, groups]);

  const activeFilterCount = appliedKeyword ? 1 : 0;
  const enabledGroups = groups.filter((group) => group.voIsEnabled).length;
  const paidGroups = groups.filter((group) => group.voGroupType === 2).length;
  const totalStickers = groups.reduce((sum, group) => sum + group.voStickerCount, 0);

  const reportUnavailableAction = (hasPermission: boolean) => {
    message.error(t(!hasPermission
      ? 'stickers.common.permissionDenied'
      : 'stickers.common.authorityUnavailable'));
  };

  const handleApplyFilter = () => {
    const nextKeyword = keywordDraft.trim().slice(0, 100);
    const nextParams = new URLSearchParams();
    if (nextKeyword) nextParams.set('keyword', nextKeyword);
    setSearchParams(nextParams, { replace: true });
    setFilterSheetOpen(false);
  };

  const handleResetFilter = () => {
    setKeywordDraft('');
    setSearchParams(new URLSearchParams(), { replace: true });
    setFilterSheetOpen(false);
  };

  const openCreate = () => {
    if (!canCreateSticker || !actionsAreAuthoritative) {
      reportUnavailableAction(canCreateSticker);
      return;
    }
    setFormMode('create');
    setEditingGroup(undefined);
    setFormVisible(true);
  };

  const openEdit = (group: StickerGroupVo) => {
    if (!canEditSticker || !actionsAreAuthoritative) {
      reportUnavailableAction(canEditSticker);
      return;
    }
    setFormMode('edit');
    setEditingGroup(group);
    setFormVisible(true);
  };

  const handleDelete = async (id: string) => {
    if (!canDeleteStickerPermission || !actionsAreAuthoritative || busyAction) {
      reportUnavailableAction(canDeleteStickerPermission);
      return;
    }
    try {
      setBusyAction(`delete:${id}`);
      await deleteStickerGroup(id);
      message.success(t('stickers.group.feedback.deleted'));
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '删除表情包分组失败:', error);
      message.error(error instanceof Error ? error.message : t('stickers.group.feedback.deleteFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleToggleStatus = async (group: StickerGroupVo, enabled: boolean) => {
    if (!canToggleSticker || !actionsAreAuthoritative || busyAction) {
      reportUnavailableAction(canToggleSticker);
      return;
    }
    try {
      setBusyAction(`toggle:${group.voId}`);
      await updateStickerGroupStatus(group.voId, enabled);
      message.success(t(enabled ? 'stickers.group.feedback.enabled' : 'stickers.group.feedback.disabled'));
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '更新分组状态失败:', error);
      message.error(error instanceof Error ? error.message : t('stickers.group.feedback.toggleFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleManage = (group: StickerGroupVo) => {
    if (!canViewStickers) {
      reportUnavailableAction(canViewStickers);
      return;
    }
    navigate(`${ROUTES.STICKERS}/${group.voId}/items`);
  };

  const renderActions = (record: StickerGroupVo) => (
    <Space size="small" wrap>
      <Button variant="ghost" size="small" icon={<AppstoreOutlined />} onClick={() => handleManage(record)}>
        {t('stickers.group.actions.manage')}
      </Button>
      {canEditSticker ? (
        <Button variant="ghost" size="small" icon={<EditOutlined />} disabled={!actionsAreAuthoritative} onClick={() => openEdit(record)}>
          {t('stickers.common.edit')}
        </Button>
      ) : null}
      {canToggleSticker ? (
        <Button
          variant="ghost"
          size="small"
          disabled={!actionsAreAuthoritative || !!busyAction}
          onClick={() => void handleToggleStatus(record, !record.voIsEnabled)}
        >
          {t(record.voIsEnabled ? 'stickers.common.disabled' : 'stickers.common.enabled')}
        </Button>
      ) : null}
      {canDeleteStickerPermission ? (
        <Popconfirm
          title={t('stickers.group.delete.title')}
          description={t('stickers.group.delete.description')}
          disabled={!actionsAreAuthoritative || !!busyAction}
          onConfirm={() => void handleDelete(record.voId)}
          okText={t('stickers.common.confirm')}
          cancelText={t('stickers.common.cancel')}
        >
          <Button variant="ghost" size="small" icon={<DeleteOutlined />} disabled={!actionsAreAuthoritative || !!busyAction}>
            {t('stickers.common.delete')}
          </Button>
        </Popconfirm>
      ) : null}
    </Space>
  );

  const columns: TableColumnsType<StickerGroupVo> = [
    {
      title: t('stickers.group.table.cover'),
      key: 'cover',
      width: 84,
      render: (_, record) => {
        const coverImageUrl = getAvatarUrl(record.voCoverImageUrl);
        return <div className="sticker-group-cover">{coverImageUrl ? <img src={coverImageUrl} alt={record.voName} /> : <span>{t('stickers.group.table.none')}</span>}</div>;
      },
    },
    { title: t('stickers.group.table.name'), dataIndex: 'voName', key: 'voName', width: 180 },
    { title: 'Code', dataIndex: 'voCode', key: 'voCode', width: 170 },
    {
      title: t('stickers.group.table.type'),
      key: 'voGroupType',
      width: 110,
      render: (_, record) => <Tag color={record.voGroupType === 2 ? 'gold' : 'blue'}>{t(record.voGroupType === 2 ? 'stickers.group.type.paid' : 'stickers.group.type.official')}</Tag>,
    },
    {
      title: t('stickers.group.table.status'),
      key: 'voIsEnabled',
      width: 100,
      render: (_, record) => <Tag color={record.voIsEnabled ? 'success' : 'error'}>{t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}</Tag>,
    },
    { title: t('stickers.group.table.count'), dataIndex: 'voStickerCount', key: 'voStickerCount', width: 100 },
    { title: t('stickers.group.table.sort'), dataIndex: 'voSort', key: 'voSort', width: 90 },
    { title: t('stickers.group.table.description'), dataIndex: 'voDescription', key: 'voDescription', ellipsis: true },
    { title: t('stickers.group.table.actions'), key: 'actions', width: 360, fixed: 'right', render: (_, record) => renderActions(record) },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls">
      <Input
        allowClear
        className="sticker-list-filter-input"
        placeholder={t('stickers.group.filter.placeholder')}
        prefix={<SearchOutlined />}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={handleApplyFilter}
      />
      <div className="console-resource-filter-controls__actions">
        <Button onClick={handleResetFilter}>{t('stickers.common.resetFilters')}</Button>
        <Button variant="primary" onClick={handleApplyFilter}>{t('stickers.common.applyFilters')}</Button>
      </div>
    </div>
  );

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert">
      <div>
        <strong>{t(readState === 'stale' ? 'stickers.common.staleTitle' : 'stickers.common.unavailableTitle')}</strong>
        <span>{t(readState === 'stale' ? 'stickers.common.staleDescription' : 'stickers.common.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadGroups()}>{t('stickers.common.retry')}</Button>
    </div>
  ) : null;

  return (
    <div className="admin-feature-page sticker-group-list-page">
      <ConsolePageHeader
        eyebrow={t('stickers.common.eyebrow')}
        title={t('stickers.group.page.title')}
        description={t('stickers.group.page.description')}
        icon={<AppstoreOutlined />}
        status={<ConsoleStatusChip tone={actionsAreAuthoritative && canCreateSticker ? 'success' : 'neutral'}>{t(actionsAreAuthoritative && canCreateSticker ? 'stickers.common.createWritable' : 'stickers.common.readOnly')}</ConsoleStatusChip>}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} disabled={loading} onClick={() => void loadGroups()}>{t('stickers.common.refresh')}</Button>
            {canCreateSticker ? <Button variant="primary" icon={<PlusOutlined />} disabled={!actionsAreAuthoritative} onClick={openCreate}>{t('stickers.group.actions.create')}</Button> : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('stickers.group.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('stickers.group.metrics.total')} value={formatConsoleNumber(groups.length, language)} description={t('stickers.group.metrics.totalDescription')} />
        <ConsoleMetricCard label={t('stickers.group.metrics.result')} value={formatConsoleNumber(filteredGroups.length, language)} description={t('stickers.group.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('stickers.group.metrics.enabled')} value={formatConsoleNumber(enabledGroups, language)} description={t('stickers.group.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('stickers.group.metrics.stickers')} value={formatConsoleNumber(totalStickers, language)} description={t('stickers.group.metrics.stickersDescription')} />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={<ConsoleToolbar title={t('stickers.group.filter.title')} description={t('stickers.group.filter.description')} meta={<ConsoleStatusChip tone={activeFilterCount ? 'info' : 'neutral'}>{activeFilterCount ? t('stickers.common.filterCount', { count: activeFilterCount }) : t('stickers.common.notFiltered')}</ConsoleStatusChip>}>{filterControls}</ConsoleToolbar>}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy"><strong>{t('stickers.group.mobile.result', { count: filteredGroups.length })}</strong><span>{activeFilterCount ? t('stickers.common.filterCount', { count: activeFilterCount }) : t('stickers.common.notFiltered')}</span></div>
            <div className="console-resource-mobile-summary__actions"><Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>{t('stickers.common.filterAction')}</Button><Button size="small" icon={<ReloadOutlined />} disabled={loading} onClick={() => void loadGroups()}>{t('stickers.common.refresh')}</Button></div>
          </div>
        )}
        desktopList={<section className="admin-table-panel">{readNotice}<Table<StickerGroupVo> rowKey="voId" loading={loading} columns={columns} dataSource={filteredGroups} scroll={{ x: 1280 }} pagination={false} /></section>}
        mobileList={(
          <>{readNotice}{loading && groups.length === 0 ? <div className="console-resource-mobile-loading">{t('stickers.common.loading')}</div> : null}{readState === 'ready' && filteredGroups.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('stickers.group.mobile.emptyTitle')}</strong><span>{t('stickers.group.mobile.emptyDescription')}</span></div> : null}{filteredGroups.map((record) => {
            const coverUrl = getAvatarUrl(record.voCoverImageUrl);
            return <article className="console-resource-mobile-card sticker-resource-mobile-card" key={record.voId}><div className="console-resource-mobile-card__header"><div className="sticker-resource-mobile-card__identity">{coverUrl ? <img src={coverUrl} alt="" /> : <span className="sticker-resource-mobile-card__placeholder">{record.voName.slice(0, 1)}</span>}<div><strong>{record.voName}</strong><span>{record.voCode} · #{record.voId}</span></div></div><Tag color={record.voIsEnabled ? 'success' : 'error'}>{t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}</Tag></div><div className="console-resource-mobile-card__facts"><div className="console-resource-mobile-card__fact"><span>{t('stickers.group.table.type')}</span><strong>{t(record.voGroupType === 2 ? 'stickers.group.type.paid' : 'stickers.group.type.official')}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('stickers.group.table.count')}</span><strong>{formatConsoleNumber(record.voStickerCount, language)}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('stickers.group.table.sort')}</span><strong>{record.voSort}</strong></div></div>{record.voDescription ? <p className="console-resource-mobile-card__description">{record.voDescription}</p> : null}<div className="console-resource-mobile-card__footer"><span />{renderActions(record)}</div></article>;
          })}</>
        )}
        context={<><h3>{t('stickers.group.summary.title')}</h3><p className="admin-feature-subtle">{t('stickers.group.summary.description')}</p><div className="admin-table-summary"><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.group.summary.scope')}</span><span className="admin-table-summary__value">{t(activeFilterCount ? 'stickers.group.summary.filtered' : 'stickers.group.summary.all')}</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.group.summary.paid')}</span><span className="admin-table-summary__value">{formatConsoleNumber(paidGroups, language)}</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.group.summary.toggle')}</span><span className="admin-table-summary__value">{t(canToggleSticker ? 'stickers.group.summary.toggleWritable' : 'stickers.group.summary.toggleReadOnly')}</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.group.summary.maintenance')}</span><span className="admin-table-summary__value">{t(canEditSticker ? 'stickers.group.summary.maintenanceWritable' : 'stickers.group.summary.maintenanceReadOnly')}</span></div></div></>}
      />

      <BottomSheet isOpen={filterSheetOpen} onClose={() => setFilterSheetOpen(false)} closeLabel={t('stickers.common.closeFilters')} title={t('stickers.group.filter.title')} height="auto" className="console-resource-filter-sheet">{filterControls}</BottomSheet>
      <StickerGroupForm
        visible={formVisible}
        mode={formMode}
        group={editingGroup}
        canSubmit={actionsAreAuthoritative && (formMode === 'create' ? canCreateSticker : canEditSticker)}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => { setFormVisible(false); void loadGroups(); }}
      />
    </div>
  );
};
