import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { ApiResponseError } from '@radish/http';
import {
  AntModal as Modal,
  BottomSheet,
  Table,
  Button,
  Space,
  Tag,
  InputNumber,
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
  LeftOutlined,
} from '@radish/ui';
import {
  batchUpdateStickerSort,
  deleteSticker,
  getGroupStickers,
  type StickerVo,
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
import { ROUTES } from '@/router/routes';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { StickerForm } from './StickerForm';
import { StickerBatchUploadModal } from './StickerBatchUploadModal';
import '../adminFeature.css';
import './StickerResource.css';
import './StickerList.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

const getPreviewUrl = (sticker: StickerVo) => getAvatarUrl(sticker.voThumbnailUrl || sticker.voImageUrl);
const normalizeKeyword = (value: string | null) => value?.trim().slice(0, 100) ?? '';

export const StickerList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const navigate = useNavigate();
  const { groupId } = useParams<{ groupId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const normalizedGroupId = String(groupId || '').trim();
  const isValidGroupId = /^[1-9]\d*$/.test(normalizedGroupId);
  const appliedKeyword = normalizeKeyword(searchParams.get('keyword'));
  useDocumentTitle(t('stickers.item.documentTitle'));

  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [savingSort, setSavingSort] = useState(false);
  const [busyAction, setBusyAction] = useState<string>();
  const [keywordDraft, setKeywordDraft] = useState(appliedKeyword);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [stickers, setStickers] = useState<StickerVo[]>([]);
  const [sortDrafts, setSortDrafts] = useState<Record<string, number>>({});
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingSticker, setEditingSticker] = useState<StickerVo>();
  const [batchModalVisible, setBatchModalVisible] = useState(false);
  const requestSequence = useRef(0);
  const snapshotGroupId = useRef<string | undefined>(undefined);
  const canViewStickers = usePermission(CONSOLE_PERMISSIONS.stickersView);
  const canCreateSticker = usePermission(CONSOLE_PERMISSIONS.stickersCreate);
  const canEditSticker = usePermission(CONSOLE_PERMISSIONS.stickersEdit);
  const canDeleteStickerPermission = usePermission(CONSOLE_PERMISSIONS.stickersDelete);
  const canSortSticker = usePermission(CONSOLE_PERMISSIONS.stickersSort);
  const canBatchUploadSticker = usePermission(CONSOLE_PERMISSIONS.stickersBatchUpload);
  const actionsAreAuthoritative = readState === 'ready';
  const hasSortDrafts = Object.keys(sortDrafts).length > 0;

  useEffect(() => {
    setKeywordDraft(appliedKeyword);
  }, [appliedKeyword]);

  const loadStickers = useCallback(async () => {
    if (!isValidGroupId) return;
    const requestId = requestSequence.current + 1;
    const hasCurrentSnapshot = snapshotGroupId.current === normalizedGroupId;
    requestSequence.current = requestId;
    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setStickers([]);
        setSortDrafts({});
      }
      const data = await getGroupStickers(normalizedGroupId);
      if (requestSequence.current !== requestId) return;
      setStickers(data);
      if (!hasCurrentSnapshot) setSortDrafts({});
      snapshotGroupId.current = normalizedGroupId;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) return;
      log.error('StickerList', '加载分组表情失败:', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(error instanceof Error ? error.message : t('stickers.item.feedback.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) setLoading(false);
    }
  }, [isValidGroupId, normalizedGroupId, t]);

  useEffect(() => {
    if (canViewStickers) void loadStickers();
  }, [canViewStickers, loadStickers]);

  useEffect(() => {
    if (!hasSortDrafts) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasSortDrafts]);

  const filteredStickers = useMemo(() => {
    const normalized = appliedKeyword.toLowerCase();
    if (!normalized) return stickers;
    return stickers.filter((item) => item.voName.toLowerCase().includes(normalized) || item.voCode.toLowerCase().includes(normalized));
  }, [appliedKeyword, stickers]);

  const activeFilterCount = appliedKeyword ? 1 : 0;
  const enabledStickers = stickers.filter((sticker) => sticker.voIsEnabled).length;
  const animatedStickers = stickers.filter((sticker) => sticker.voIsAnimated).length;
  const inlineStickers = stickers.filter((sticker) => sticker.voAllowInline).length;
  const sortDraftCount = Object.keys(sortDrafts).length;

  const reportUnavailableAction = (hasPermission: boolean) => {
    message.error(t(!hasPermission ? 'stickers.common.permissionDenied' : 'stickers.common.authorityUnavailable'));
  };

  const reportSortDraftBlock = () => {
    message.warning(t('stickers.item.feedback.resolveSortDrafts'));
  };

  const confirmDiscardSortDrafts = () => {
    if (!hasSortDrafts) return Promise.resolve(true);
    return new Promise<boolean>((resolve) => {
      Modal.confirm({
        title: t('stickers.item.sortDiscard.title'),
        content: t('stickers.item.sortDiscard.description', { count: sortDraftCount }),
        okText: t('stickers.item.sortDiscard.confirm'),
        cancelText: t('stickers.common.continueEditing'),
        okButtonProps: { danger: true },
        onOk: () => resolve(true),
        onCancel: () => resolve(false),
      });
    });
  };

  const handleRefresh = async () => {
    if (!await confirmDiscardSortDrafts()) return;
    setSortDrafts({});
    await loadStickers();
  };

  const handleBack = async () => {
    if (!await confirmDiscardSortDrafts()) return;
    setSortDrafts({});
    navigate(ROUTES.STICKERS);
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

  const handleOpenCreate = () => {
    if (!canCreateSticker || !actionsAreAuthoritative) {
      reportUnavailableAction(canCreateSticker);
      return;
    }
    if (hasSortDrafts) {
      reportSortDraftBlock();
      return;
    }
    setFormMode('create');
    setEditingSticker(undefined);
    setFormVisible(true);
  };

  const handleOpenEdit = (sticker: StickerVo) => {
    if (!canEditSticker || !actionsAreAuthoritative) {
      reportUnavailableAction(canEditSticker);
      return;
    }
    if (hasSortDrafts) {
      reportSortDraftBlock();
      return;
    }
    setFormMode('edit');
    setEditingSticker(sticker);
    setFormVisible(true);
  };

  const handleOpenBatch = () => {
    if (!canBatchUploadSticker || !actionsAreAuthoritative) {
      reportUnavailableAction(canBatchUploadSticker);
      return;
    }
    if (hasSortDrafts) {
      reportSortDraftBlock();
      return;
    }
    setBatchModalVisible(true);
  };

  const handleDelete = async (id: string) => {
    if (!canDeleteStickerPermission || !actionsAreAuthoritative || busyAction) {
      reportUnavailableAction(canDeleteStickerPermission);
      return;
    }
    if (hasSortDrafts) {
      reportSortDraftBlock();
      return;
    }
    try {
      setBusyAction(`delete:${id}`);
      await deleteSticker(id);
      message.success(t('stickers.item.feedback.deleted'));
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '删除表情失败:', error);
      message.error(error instanceof Error ? error.message : t('stickers.item.feedback.deleteFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleSortChange = (sticker: StickerVo, value: number | null) => {
    if (!canSortSticker || !actionsAreAuthoritative || savingSort) {
      reportUnavailableAction(canSortSticker);
      return;
    }
    const normalizedSort = Math.max(0, Number(value || 0));
    setSortDrafts((current) => {
      const next = { ...current };
      if (normalizedSort === sticker.voSort) delete next[sticker.voId];
      else next[sticker.voId] = normalizedSort;
      return next;
    });
  };

  const handleSaveSort = async () => {
    if (!canSortSticker || !actionsAreAuthoritative || savingSort) {
      reportUnavailableAction(canSortSticker);
      return;
    }
    const entries = Object.entries(sortDrafts);
    if (entries.length === 0) {
      message.info(t('stickers.item.feedback.noSortChanges'));
      return;
    }
    try {
      setSavingSort(true);
      await batchUpdateStickerSort({
        groupId: normalizedGroupId,
        items: entries.map(([id, sort]) => ({ id, sort })),
      });
      setSortDrafts({});
      message.success(t('stickers.item.feedback.sortUpdated'));
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '批量更新排序失败:', error);
      if (error instanceof ApiResponseError && error.code === 'StickerSortSnapshotStale') {
        setReadState('stale');
      }
      message.error(error instanceof Error ? error.message : t('stickers.item.feedback.sortFailed'));
    } finally {
      setSavingSort(false);
    }
  };

  const renderActions = (record: StickerVo) => (
    <Space size="small" wrap>
      {canEditSticker ? <Button variant="ghost" size="small" icon={<EditOutlined />} disabled={!actionsAreAuthoritative || hasSortDrafts} onClick={() => handleOpenEdit(record)}>{t('stickers.common.edit')}</Button> : null}
      {canDeleteStickerPermission ? (
        <Popconfirm title={t('stickers.item.delete.title')} description={t('stickers.item.delete.description')} disabled={!actionsAreAuthoritative || hasSortDrafts || !!busyAction} onConfirm={() => void handleDelete(record.voId)} okText={t('stickers.common.confirm')} cancelText={t('stickers.common.cancel')}>
          <Button variant="ghost" size="small" icon={<DeleteOutlined />} disabled={!actionsAreAuthoritative || hasSortDrafts || !!busyAction}>{t('stickers.common.delete')}</Button>
        </Popconfirm>
      ) : null}
    </Space>
  );

  const columns: TableColumnsType<StickerVo> = [
    { title: t('stickers.item.table.preview'), key: 'preview', width: 90, render: (_, record) => <div className="sticker-item-preview"><img src={getPreviewUrl(record)} alt={record.voName} /></div> },
    { title: t('stickers.item.table.name'), dataIndex: 'voName', key: 'voName', width: 180 },
    { title: 'Code', dataIndex: 'voCode', key: 'voCode', width: 180 },
    { title: t('stickers.item.table.type'), key: 'voIsAnimated', width: 100, render: (_, record) => <Tag color={record.voIsAnimated ? 'blue' : 'default'}>{record.voIsAnimated ? 'GIF' : t('stickers.item.type.static')}</Tag> },
    { title: t('stickers.item.table.inline'), key: 'voAllowInline', width: 110, render: (_, record) => <Tag color={record.voAllowInline ? 'success' : 'default'}>{t(record.voAllowInline ? 'stickers.item.inline.allowed' : 'stickers.item.inline.reactionOnly')}</Tag> },
    { title: t('stickers.item.table.status'), key: 'voIsEnabled', width: 90, render: (_, record) => <Tag color={record.voIsEnabled ? 'success' : 'error'}>{t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}</Tag> },
    { title: t('stickers.item.table.uses'), dataIndex: 'voUseCount', key: 'voUseCount', width: 100, render: (value: number) => formatConsoleNumber(value, language) },
    { title: t('stickers.item.table.sort'), key: 'voSort', width: 120, render: (_, record) => <InputNumber className="sticker-item-sort-input" disabled={!canSortSticker || !actionsAreAuthoritative || savingSort} min={0} value={sortDrafts[record.voId] ?? record.voSort} onChange={(value) => handleSortChange(record, value)} /> },
    { title: t('stickers.item.table.actions'), key: 'actions', width: 220, fixed: 'right', render: (_, record) => renderActions(record) },
  ];

  if (!isValidGroupId) {
    return <div className="admin-feature-page sticker-item-list-page"><ConsolePageHeader eyebrow={t('stickers.common.eyebrow')} title={t('stickers.item.page.title')} description={t('stickers.item.page.invalidDescription')} icon={<AppstoreOutlined />} status={<ConsoleStatusChip tone="warning">{t('stickers.item.page.invalidStatus')}</ConsoleStatusChip>} actions={<Button icon={<LeftOutlined />} onClick={() => navigate(ROUTES.STICKERS)}>{t('stickers.item.actions.back')}</Button>} /></div>;
  }

  const filterControls = (
    <div className="console-resource-filter-controls">
      <Input allowClear className="sticker-list-filter-input" placeholder={t('stickers.item.filter.placeholder')} prefix={<SearchOutlined />} value={keywordDraft} onChange={(event) => setKeywordDraft(event.target.value)} onPressEnter={handleApplyFilter} />
      <div className="console-resource-filter-controls__actions"><Button onClick={handleResetFilter}>{t('stickers.common.resetFilters')}</Button><Button variant="primary" onClick={handleApplyFilter}>{t('stickers.common.applyFilters')}</Button></div>
    </div>
  );

  const sortActions = canSortSticker ? (
    <Space wrap>
      {hasSortDrafts ? <Button disabled={savingSort} onClick={() => setSortDrafts({})}>{t('stickers.item.actions.discardSort')}</Button> : null}
      <Button variant={hasSortDrafts ? 'primary' : 'secondary'} disabled={!actionsAreAuthoritative || !hasSortDrafts || savingSort} onClick={() => void handleSaveSort()}>{t('stickers.item.actions.saveSort')}</Button>
    </Space>
  ) : null;

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert"><div><strong>{t(readState === 'stale' ? 'stickers.common.staleTitle' : 'stickers.common.unavailableTitle')}</strong><span>{t(readState === 'stale' ? 'stickers.common.staleDescription' : 'stickers.common.unavailableDescription')}</span></div><Button size="small" onClick={() => void handleRefresh()}>{t('stickers.common.retry')}</Button></div>
  ) : null;

  return (
    <div className="admin-feature-page sticker-item-list-page">
      <ConsolePageHeader
        eyebrow={t('stickers.common.eyebrow')}
        title={t('stickers.item.page.title')}
        description={t('stickers.item.page.description', { groupId: normalizedGroupId })}
        icon={<AppstoreOutlined />}
        status={<ConsoleStatusChip tone={actionsAreAuthoritative && canCreateSticker ? 'success' : 'neutral'}>{t(actionsAreAuthoritative && canCreateSticker ? 'stickers.common.createWritable' : 'stickers.common.readOnly')}</ConsoleStatusChip>}
        actions={<><Button onClick={() => void handleBack()} icon={<LeftOutlined />}>{t('stickers.item.actions.back')}</Button><Button icon={<ReloadOutlined />} disabled={loading} onClick={() => void handleRefresh()}>{t('stickers.common.refresh')}</Button>{canCreateSticker ? <Button variant="primary" icon={<PlusOutlined />} disabled={!actionsAreAuthoritative || hasSortDrafts} onClick={handleOpenCreate}>{t('stickers.item.actions.create')}</Button> : null}{canBatchUploadSticker ? <Button disabled={!actionsAreAuthoritative || hasSortDrafts} onClick={handleOpenBatch}>{t('stickers.item.actions.batchUpload')}</Button> : null}</>}
      />

      <ConsoleMetricGrid label={t('stickers.item.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('stickers.item.metrics.total')} value={formatConsoleNumber(stickers.length, language)} description={t('stickers.item.metrics.totalDescription')} />
        <ConsoleMetricCard label={t('stickers.item.metrics.result')} value={formatConsoleNumber(filteredStickers.length, language)} description={t('stickers.item.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('stickers.item.metrics.enabled')} value={formatConsoleNumber(enabledStickers, language)} description={t('stickers.item.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('stickers.item.metrics.drafts')} value={formatConsoleNumber(sortDraftCount, language)} description={t('stickers.item.metrics.draftsDescription')} tone={sortDraftCount > 0 ? 'warning' : 'neutral'} />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={<ConsoleToolbar title={t('stickers.item.filter.title')} description={t('stickers.item.filter.description')} meta={<ConsoleStatusChip tone={activeFilterCount ? 'info' : 'neutral'}>{activeFilterCount ? t('stickers.common.filterCount', { count: activeFilterCount }) : t('stickers.common.notFiltered')}</ConsoleStatusChip>} actions={sortActions}>{filterControls}</ConsoleToolbar>}
        mobileToolbar={<div className="console-resource-mobile-summary"><div className="console-resource-mobile-summary__copy"><strong>{t('stickers.item.mobile.result', { count: filteredStickers.length })}</strong><span>{hasSortDrafts ? t('stickers.item.mobile.draftSummary', { count: sortDraftCount }) : t('stickers.common.notFiltered')}</span></div><div className="console-resource-mobile-summary__actions"><Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>{t('stickers.common.filterAction')}</Button>{sortActions}</div></div>}
        desktopList={<section className="admin-table-panel">{readNotice}<Table<StickerVo> rowKey="voId" loading={loading} columns={columns} dataSource={filteredStickers} scroll={{ x: 1200 }} pagination={false} /></section>}
        mobileList={<>{readNotice}{loading && stickers.length === 0 ? <div className="console-resource-mobile-loading">{t('stickers.common.loading')}</div> : null}{readState === 'ready' && filteredStickers.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('stickers.item.mobile.emptyTitle')}</strong><span>{t('stickers.item.mobile.emptyDescription')}</span></div> : null}{filteredStickers.map((record) => <article className="console-resource-mobile-card sticker-resource-mobile-card" key={record.voId}><div className="console-resource-mobile-card__header"><div className="sticker-resource-mobile-card__identity"><img src={getPreviewUrl(record)} alt="" /><div><strong>{record.voName}</strong><span>{record.voCode} · #{record.voId}</span></div></div><Tag color={record.voIsEnabled ? 'success' : 'error'}>{t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}</Tag></div><div className="console-resource-mobile-card__facts"><div className="console-resource-mobile-card__fact"><span>{t('stickers.item.table.type')}</span><strong>{record.voIsAnimated ? 'GIF' : t('stickers.item.type.static')}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('stickers.item.table.uses')}</span><strong>{formatConsoleNumber(record.voUseCount, language)}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('stickers.item.table.inline')}</span><strong>{t(record.voAllowInline ? 'stickers.item.inline.allowed' : 'stickers.item.inline.reactionOnly')}</strong></div></div><div className="console-resource-mobile-card__footer">{canSortSticker ? <InputNumber className="sticker-item-sort-input" disabled={!actionsAreAuthoritative || savingSort} min={0} value={sortDrafts[record.voId] ?? record.voSort} onChange={(value) => handleSortChange(record, value)} /> : <span />}{renderActions(record)}</div></article>)}</>}
        context={<><h3>{t('stickers.item.summary.title')}</h3><p className="admin-feature-subtle">{t('stickers.item.summary.description')}</p><div className="admin-table-summary"><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.item.summary.groupId')}</span><span className="admin-table-summary__value">{normalizedGroupId}</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.item.summary.scope')}</span><span className="admin-table-summary__value">{t(activeFilterCount ? 'stickers.item.summary.filtered' : 'stickers.item.summary.all')}</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.item.summary.types')}</span><span className="admin-table-summary__value">{animatedStickers} GIF / {inlineStickers} inline</span></div><div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('stickers.item.summary.sort')}</span><span className="admin-table-summary__value">{t(canSortSticker ? 'stickers.item.summary.sortWritable' : 'stickers.item.summary.sortReadOnly')}</span></div></div></>}
      />

      <BottomSheet isOpen={filterSheetOpen} onClose={() => setFilterSheetOpen(false)} closeLabel={t('stickers.common.closeFilters')} title={t('stickers.item.filter.title')} height="auto" className="console-resource-filter-sheet">{filterControls}</BottomSheet>
      <StickerForm visible={formVisible} groupId={normalizedGroupId} mode={formMode} sticker={editingSticker} canSubmit={actionsAreAuthoritative && (formMode === 'create' ? canCreateSticker : canEditSticker)} onCancel={() => setFormVisible(false)} onSuccess={() => { setFormVisible(false); void loadStickers(); }} />
      <StickerBatchUploadModal visible={batchModalVisible} groupId={normalizedGroupId} canSubmit={actionsAreAuthoritative && canBatchUploadSticker} onCancel={() => setBatchModalVisible(false)} onSuccess={() => { setBatchModalVisible(false); void loadStickers(); }} />
    </div>
  );
};
