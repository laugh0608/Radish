import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
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
import './StickerList.css';

const getPreviewUrl = (sticker: StickerVo) => getAvatarUrl(sticker.voThumbnailUrl || sticker.voImageUrl);

export const StickerList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const navigate = useNavigate();
  const { groupId } = useParams<{ groupId: string }>();
  const normalizedGroupId = String(groupId || '').trim();
  const isValidGroupId = /^[1-9]\d*$/.test(normalizedGroupId);

  useDocumentTitle(t('stickers.item.documentTitle'));

  const [loading, setLoading] = useState(false);
  const [savingSort, setSavingSort] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [stickers, setStickers] = useState<StickerVo[]>([]);
  const [sortDrafts, setSortDrafts] = useState<Record<string, number>>({});
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingSticker, setEditingSticker] = useState<StickerVo | undefined>(undefined);
  const [batchModalVisible, setBatchModalVisible] = useState(false);
  const canViewStickers = usePermission(CONSOLE_PERMISSIONS.stickersView);
  const canCreateSticker = usePermission(CONSOLE_PERMISSIONS.stickersCreate);
  const canEditSticker = usePermission(CONSOLE_PERMISSIONS.stickersEdit);
  const canDeleteStickerPermission = usePermission(CONSOLE_PERMISSIONS.stickersDelete);
  const canSortSticker = usePermission(CONSOLE_PERMISSIONS.stickersSort);
  const canBatchUploadSticker = usePermission(CONSOLE_PERMISSIONS.stickersBatchUpload);

  const loadStickers = useCallback(async () => {
    if (!isValidGroupId) {
      return;
    }

    try {
      setLoading(true);
      const data = await getGroupStickers(normalizedGroupId);
      setStickers(data);
      setSortDrafts({});
    } catch (error) {
      log.error('StickerList', '加载分组表情失败:', error);
      message.error(t('stickers.item.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [isValidGroupId, normalizedGroupId, t]);

  useEffect(() => {
    if (!canViewStickers) {
      return;
    }

    void loadStickers();
  }, [canViewStickers, loadStickers]);

  const filteredStickers = useMemo(() => {
    const normalized = keyword.trim().toLowerCase();
    if (!normalized) {
      return stickers;
    }

    return stickers.filter((item) =>
      item.voName.toLowerCase().includes(normalized) ||
      item.voCode.toLowerCase().includes(normalized)
    );
  }, [stickers, keyword]);
  const activeFilterCount = keyword.trim() ? 1 : 0;
  const enabledStickers = stickers.filter((sticker) => sticker.voIsEnabled).length;
  const animatedStickers = stickers.filter((sticker) => sticker.voIsAnimated).length;
  const inlineStickers = stickers.filter((sticker) => sticker.voAllowInline).length;
  const sortDraftCount = Object.keys(sortDrafts).length;

  const handleDelete = async (id: string) => {
    try {
      await deleteSticker(id);
      message.success(t('stickers.item.feedback.deleted'));
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '删除表情失败:', error);
      message.error(t('stickers.item.feedback.deleteFailed'));
    }
  };

  const handleSortChange = (id: string, value: number | null) => {
    setSortDrafts((prev) => ({
      ...prev,
      [id]: Math.max(0, Number(value || 0)),
    }));
  };

  const handleSaveSort = async () => {
    const entries = Object.entries(sortDrafts);
    if (entries.length === 0) {
      message.info(t('stickers.item.feedback.noSortChanges'));
      return;
    }

    try {
      setSavingSort(true);
      await batchUpdateStickerSort({
        items: entries.map(([id, sort]) => ({
          id,
          sort,
        })),
      });

      message.success(t('stickers.item.feedback.sortUpdated'));
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '批量更新排序失败:', error);
      message.error(t('stickers.item.feedback.sortFailed'));
    } finally {
      setSavingSort(false);
    }
  };

  const columns: TableColumnsType<StickerVo> = [
    {
      title: t('stickers.item.table.preview'),
      key: 'preview',
      width: 90,
      render: (_, record) => (
        <div className="sticker-item-preview">
          <img src={getPreviewUrl(record)} alt={record.voName} />
        </div>
      ),
    },
    {
      title: t('stickers.item.table.name'),
      dataIndex: 'voName',
      key: 'voName',
      width: 180,
    },
    {
      title: 'Code',
      dataIndex: 'voCode',
      key: 'voCode',
      width: 180,
    },
    {
      title: t('stickers.item.table.type'),
      key: 'voIsAnimated',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsAnimated ? 'blue' : 'default'}>
          {record.voIsAnimated ? 'GIF' : t('stickers.item.type.static')}
        </Tag>
      ),
    },
    {
      title: t('stickers.item.table.inline'),
      key: 'voAllowInline',
      width: 110,
      render: (_, record) => (
        <Tag color={record.voAllowInline ? 'success' : 'default'}>
          {t(record.voAllowInline ? 'stickers.item.inline.allowed' : 'stickers.item.inline.reactionOnly')}
        </Tag>
      ),
    },
    {
      title: t('stickers.item.table.status'),
      key: 'voIsEnabled',
      width: 90,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}
        </Tag>
      ),
    },
    {
      title: t('stickers.item.table.uses'),
      dataIndex: 'voUseCount',
      key: 'voUseCount',
      width: 100,
      render: (value: number) => formatConsoleNumber(value, language),
    },
    {
      title: t('stickers.item.table.sort'),
      key: 'voSort',
      width: 120,
      render: (_, record) => (
        <InputNumber
          className="sticker-item-sort-input"
          disabled={!canSortSticker}
          min={0}
          value={sortDrafts[record.voId] ?? record.voSort}
          onChange={(value) => handleSortChange(record.voId, value)}
        />
      ),
    },
    {
      title: t('stickers.item.table.actions'),
      key: 'actions',
      width: 220,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small" wrap>
          {canEditSticker ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => {
                setFormMode('edit');
                setEditingSticker(record);
                setFormVisible(true);
              }}
            >
              {t('stickers.common.edit')}
            </Button>
          ) : null}

          {canDeleteStickerPermission ? (
            <Popconfirm
              title={t('stickers.item.delete.title')}
              description={t('stickers.item.delete.description')}
              onConfirm={() => {
                void handleDelete(record.voId);
              }}
              okText={t('stickers.common.confirm')}
              cancelText={t('stickers.common.cancel')}
            >
              <Button variant="ghost" size="small" icon={<DeleteOutlined />}>
                {t('stickers.common.delete')}
              </Button>
            </Popconfirm>
          ) : null}
        </Space>
      ),
    },
  ];

  if (!isValidGroupId) {
    return (
      <div className="admin-feature-page sticker-item-list-page">
        <ConsolePageHeader
          eyebrow={t('stickers.common.eyebrow')}
          title={t('stickers.item.page.title')}
          description={t('stickers.item.page.invalidDescription')}
          icon={<AppstoreOutlined />}
          status={<ConsoleStatusChip tone="warning">{t('stickers.item.page.invalidStatus')}</ConsoleStatusChip>}
          actions={(
            <Button
              icon={<LeftOutlined />}
              onClick={() => {
                navigate(ROUTES.STICKERS);
              }}
            >
              {t('stickers.item.actions.back')}
            </Button>
          )}
        />
      </div>
    );
  }
  return (
    <div className="admin-feature-page sticker-item-list-page">
      <ConsolePageHeader
        eyebrow={t('stickers.common.eyebrow')}
        title={t('stickers.item.page.title')}
        description={t('stickers.item.page.description', { groupId: normalizedGroupId })}
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateSticker ? 'success' : 'neutral'}>
            {t(canCreateSticker ? 'stickers.common.createWritable' : 'stickers.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button
              onClick={() => {
                navigate(ROUTES.STICKERS);
              }}
              icon={<LeftOutlined />}
            >
              {t('stickers.item.actions.back')}
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void loadStickers();
              }}
            >
              {t('stickers.common.refresh')}
            </Button>
            {canCreateSticker ? (
              <Button
                variant="primary"
                icon={<PlusOutlined />}
                onClick={() => {
                  setFormMode('create');
                  setEditingSticker(undefined);
                  setFormVisible(true);
                }}
              >
                {t('stickers.item.actions.create')}
              </Button>
            ) : null}
            {canBatchUploadSticker ? (
              <Button
                onClick={() => {
                  setBatchModalVisible(true);
                }}
              >
                {t('stickers.item.actions.batchUpload')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('stickers.item.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('stickers.item.metrics.total')} value={formatConsoleNumber(stickers.length, language)} description={t('stickers.item.metrics.totalDescription')} />
        <ConsoleMetricCard label={t('stickers.item.metrics.result')} value={formatConsoleNumber(filteredStickers.length, language)} description={t('stickers.item.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('stickers.item.metrics.enabled')} value={formatConsoleNumber(enabledStickers, language)} description={t('stickers.item.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('stickers.item.metrics.drafts')} value={formatConsoleNumber(sortDraftCount, language)} description={t('stickers.item.metrics.draftsDescription')} tone={sortDraftCount > 0 ? 'warning' : 'neutral'} />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('stickers.item.filter.title')}
            description={t('stickers.item.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('stickers.common.filterCount', { count: activeFilterCount }) : t('stickers.common.notFiltered')}
              </ConsoleStatusChip>
            )}
            actions={canSortSticker ? (
              <Button
                disabled={savingSort}
                onClick={() => {
                  void handleSaveSort();
                }}
              >
                {t('stickers.item.actions.saveSort')}
              </Button>
            ) : null}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                className="sticker-list-filter-input"
                placeholder={t('stickers.item.filter.placeholder')}
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table<StickerVo>
              rowKey="voId"
              loading={loading}
              columns={columns}
              dataSource={filteredStickers}
              scroll={{ x: 1200 }}
              pagination={false}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('stickers.item.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('stickers.item.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.item.summary.groupId')}</span>
              <span className="admin-table-summary__value">{normalizedGroupId}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.item.summary.scope')}</span>
              <span className="admin-table-summary__value">
                {t(activeFilterCount > 0 ? 'stickers.item.summary.filtered' : 'stickers.item.summary.all')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.item.summary.types')}</span>
              <span className="admin-table-summary__value">{formatConsoleNumber(animatedStickers, language)} / {formatConsoleNumber(inlineStickers, language)}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.item.summary.sort')}</span>
              <span className="admin-table-summary__value">
                {t(canSortSticker ? 'stickers.item.summary.sortWritable' : 'stickers.item.summary.sortReadOnly')}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <StickerForm
        visible={formVisible}
        groupId={normalizedGroupId}
        mode={formMode}
        sticker={editingSticker}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadStickers();
        }}
      />

      <StickerBatchUploadModal
        visible={batchModalVisible}
        groupId={normalizedGroupId}
        onCancel={() => {
          setBatchModalVisible(false);
        }}
        onSuccess={() => {
          setBatchModalVisible(false);
          void loadStickers();
        }}
      />
    </div>
  );
};
