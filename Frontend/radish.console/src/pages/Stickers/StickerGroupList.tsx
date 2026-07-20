import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
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
  updateStickerGroup,
  type StickerGroupUpsertRequest,
  type StickerGroupVo,
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
import { getAvatarUrl } from '@/config/env';
import { ROUTES } from '@/router/routes';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { StickerGroupForm } from './StickerGroupForm';
import '../adminFeature.css';
import './StickerGroupList.css';

export const StickerGroupList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('stickers.group.documentTitle'));

  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [groups, setGroups] = useState<StickerGroupVo[]>([]);
  const [keyword, setKeyword] = useState('');
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingGroup, setEditingGroup] = useState<StickerGroupVo | undefined>(undefined);
  const canViewStickers = usePermission(CONSOLE_PERMISSIONS.stickersView);
  const canCreateSticker = usePermission(CONSOLE_PERMISSIONS.stickersCreate);
  const canEditSticker = usePermission(CONSOLE_PERMISSIONS.stickersEdit);
  const canDeleteStickerPermission = usePermission(CONSOLE_PERMISSIONS.stickersDelete);
  const canToggleSticker = usePermission(CONSOLE_PERMISSIONS.stickersToggle);

  const loadGroups = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getAdminStickerGroups();
      setGroups(data);
    } catch (error) {
      log.error('StickerGroupList', '加载表情包分组失败:', error);
      message.error(t('stickers.group.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!canViewStickers) {
      return;
    }

    void loadGroups();
  }, [canViewStickers, loadGroups]);

  const filteredGroups = useMemo(() => {
    const normalized = keyword.trim().toLowerCase();
    if (!normalized) {
      return groups;
    }

    return groups.filter((item) =>
      item.voName.toLowerCase().includes(normalized) ||
      item.voCode.toLowerCase().includes(normalized) ||
      (item.voDescription || '').toLowerCase().includes(normalized)
    );
  }, [groups, keyword]);
  const activeFilterCount = keyword.trim() ? 1 : 0;
  const enabledGroups = groups.filter((group) => group.voIsEnabled).length;
  const paidGroups = groups.filter((group) => group.voGroupType === 2).length;
  const totalStickers = groups.reduce((sum, group) => sum + group.voStickerCount, 0);

  const openCreate = () => {
    setFormMode('create');
    setEditingGroup(undefined);
    setFormVisible(true);
  };

  const openEdit = (group: StickerGroupVo) => {
    setFormMode('edit');
    setEditingGroup(group);
    setFormVisible(true);
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteStickerGroup(id);
      message.success(t('stickers.group.feedback.deleted'));
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '删除表情包分组失败:', error);
      message.error(t('stickers.group.feedback.deleteFailed'));
    }
  };

  const handleToggleStatus = async (group: StickerGroupVo, enabled: boolean) => {
    try {
      const request: StickerGroupUpsertRequest = {
        name: group.voName,
        description: group.voDescription || undefined,
        coverAttachmentId: group.voCoverAttachmentId || null,
        groupType: group.voGroupType,
        isEnabled: enabled,
        sort: group.voSort,
      };

      await updateStickerGroup(group.voId, request);
      message.success(t(enabled ? 'stickers.group.feedback.enabled' : 'stickers.group.feedback.disabled'));
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '更新分组状态失败:', error);
      message.error(t('stickers.group.feedback.toggleFailed'));
    }
  };

  const columns: TableColumnsType<StickerGroupVo> = [
    {
      title: t('stickers.group.table.cover'),
      key: 'cover',
      width: 84,
      render: (_, record) => {
        const coverImageUrl = getAvatarUrl(record.voCoverImageUrl);

        return (
          <div className="sticker-group-cover">
            {coverImageUrl ? (
              <img src={coverImageUrl} alt={record.voName} />
            ) : (
              <span>{t('stickers.group.table.none')}</span>
            )}
          </div>
        );
      },
    },
    {
      title: t('stickers.group.table.name'),
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
      title: t('stickers.group.table.type'),
      key: 'voGroupType',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voGroupType === 2 ? 'gold' : 'blue'}>
          {t(record.voGroupType === 2 ? 'stickers.group.type.paid' : 'stickers.group.type.official')}
        </Tag>
      ),
    },
    {
      title: t('stickers.group.table.status'),
      key: 'voIsEnabled',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {t(record.voIsEnabled ? 'stickers.common.enabled' : 'stickers.common.disabled')}
        </Tag>
      ),
    },
    {
      title: t('stickers.group.table.count'),
      dataIndex: 'voStickerCount',
      key: 'voStickerCount',
      width: 100,
      render: (value: number) => formatConsoleNumber(value, language),
    },
    {
      title: t('stickers.group.table.sort'),
      dataIndex: 'voSort',
      key: 'voSort',
      width: 100,
    },
    {
      title: t('stickers.group.table.description'),
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
    },
    {
      title: t('stickers.group.table.actions'),
      key: 'actions',
      width: 360,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small" wrap>
          {canViewStickers ? (
            <Button
              variant="ghost"
              size="small"
              icon={<AppstoreOutlined />}
              onClick={() => {
                navigate(`${ROUTES.STICKERS}/${record.voId}/items`);
              }}
            >
              {t('stickers.group.actions.manage')}
            </Button>
          ) : null}

          {canEditSticker ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => openEdit(record)}
            >
              {t('stickers.common.edit')}
            </Button>
          ) : null}

          {canToggleSticker ? (
            <Button
              variant="ghost"
              size="small"
              onClick={() => {
                void handleToggleStatus(record, !record.voIsEnabled);
              }}
            >
              {t(record.voIsEnabled ? 'stickers.common.disabled' : 'stickers.common.enabled')}
            </Button>
          ) : null}

          {canDeleteStickerPermission ? (
            <Popconfirm
              title={t('stickers.group.delete.title')}
              description={t('stickers.group.delete.description')}
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
  return (
    <div className="admin-feature-page sticker-group-list-page">
      <ConsolePageHeader
        eyebrow={t('stickers.common.eyebrow')}
        title={t('stickers.group.page.title')}
        description={t('stickers.group.page.description')}
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateSticker ? 'success' : 'neutral'}>
            {t(canCreateSticker ? 'stickers.common.createWritable' : 'stickers.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void loadGroups();
              }}
            >
              {t('stickers.common.refresh')}
            </Button>
            {canCreateSticker ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('stickers.group.actions.create')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('stickers.group.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('stickers.group.metrics.total')} value={formatConsoleNumber(groups.length, language)} description={t('stickers.group.metrics.totalDescription')} />
        <ConsoleMetricCard label={t('stickers.group.metrics.result')} value={formatConsoleNumber(filteredGroups.length, language)} description={t('stickers.group.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('stickers.group.metrics.enabled')} value={formatConsoleNumber(enabledGroups, language)} description={t('stickers.group.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('stickers.group.metrics.stickers')} value={formatConsoleNumber(totalStickers, language)} description={t('stickers.group.metrics.stickersDescription')} />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('stickers.group.filter.title')}
            description={t('stickers.group.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('stickers.common.filterCount', { count: activeFilterCount }) : t('stickers.common.notFiltered')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                className="sticker-list-filter-input"
                placeholder={t('stickers.group.filter.placeholder')}
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table<StickerGroupVo>
              rowKey="voId"
              loading={loading}
              columns={columns}
              dataSource={filteredGroups}
              scroll={{ x: 1200 }}
              pagination={false}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('stickers.group.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('stickers.group.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.group.summary.scope')}</span>
              <span className="admin-table-summary__value">
                {t(activeFilterCount > 0 ? 'stickers.group.summary.filtered' : 'stickers.group.summary.all')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.group.summary.paid')}</span>
              <span className="admin-table-summary__value">{formatConsoleNumber(paidGroups, language)}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.group.summary.toggle')}</span>
              <span className="admin-table-summary__value">
                {t(canToggleSticker ? 'stickers.group.summary.toggleWritable' : 'stickers.group.summary.toggleReadOnly')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('stickers.group.summary.maintenance')}</span>
              <span className="admin-table-summary__value">
                {t(canEditSticker ? 'stickers.group.summary.maintenanceWritable' : 'stickers.group.summary.maintenanceReadOnly')}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <StickerGroupForm
        visible={formVisible}
        mode={formMode}
        group={editingGroup}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadGroups();
        }}
      />
    </div>
  );
};
