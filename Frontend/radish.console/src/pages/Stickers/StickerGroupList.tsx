import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
import { StickerGroupForm } from './StickerGroupForm';
import '../adminFeature.css';
import './StickerGroupList.css';

const getGroupTypeText = (type: number) => (type === 2 ? '付费' : '官方');

export const StickerGroupList = () => {
  useDocumentTitle('表情包管理');

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

  const loadGroups = async () => {
    try {
      setLoading(true);
      const data = await getAdminStickerGroups();
      setGroups(data);
    } catch (error) {
      log.error('StickerGroupList', '加载表情包分组失败:', error);
      message.error('加载表情包分组失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canViewStickers) {
      return;
    }

    void loadGroups();
  }, [canViewStickers]);

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
      message.success('删除表情包分组成功');
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '删除表情包分组失败:', error);
      message.error('删除表情包分组失败');
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
      message.success(enabled ? '分组已启用' : '分组已禁用');
      await loadGroups();
    } catch (error) {
      log.error('StickerGroupList', '更新分组状态失败:', error);
      message.error('更新分组状态失败');
    }
  };

  const columns: TableColumnsType<StickerGroupVo> = [
    {
      title: '封面',
      key: 'cover',
      width: 84,
      render: (_, record) => {
        const coverImageUrl = getAvatarUrl(record.voCoverImageUrl);

        return (
          <div className="sticker-group-cover">
            {coverImageUrl ? (
              <img src={coverImageUrl} alt={record.voName} />
            ) : (
              <span>无</span>
            )}
          </div>
        );
      },
    },
    {
      title: '名称',
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
      title: '类型',
      key: 'voGroupType',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voGroupType === 2 ? 'gold' : 'blue'}>
          {getGroupTypeText(record.voGroupType)}
        </Tag>
      ),
    },
    {
      title: '状态',
      key: 'voIsEnabled',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {record.voIsEnabled ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '表情数',
      dataIndex: 'voStickerCount',
      key: 'voStickerCount',
      width: 100,
    },
    {
      title: '排序',
      dataIndex: 'voSort',
      key: 'voSort',
      width: 100,
    },
    {
      title: '描述',
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
    },
    {
      title: '操作',
      key: 'actions',
      width: 360,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          {canViewStickers ? (
            <Button
              variant="ghost"
              size="small"
              icon={<AppstoreOutlined />}
              onClick={() => {
                navigate(`${ROUTES.STICKERS}/${record.voId}/items`);
              }}
            >
              管理表情
            </Button>
          ) : null}

          {canEditSticker ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => openEdit(record)}
            >
              编辑
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
              {record.voIsEnabled ? '禁用' : '启用'}
            </Button>
          ) : null}

          {canDeleteStickerPermission ? (
            <Popconfirm
              title="确认删除分组"
              description={`删除后将软删除该分组及组内表情，确认继续？`}
              onConfirm={() => {
                void handleDelete(record.voId);
              }}
              okText="确认"
              cancelText="取消"
            >
              <Button variant="ghost" size="small" icon={<DeleteOutlined />}>
                删除
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
        eyebrow="STICKER ASSETS"
        title="表情包管理"
        description="维护表情包分组、启停状态和进入分组表情管理的操作入口。"
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateSticker ? 'success' : 'neutral'}>
            {canCreateSticker ? '可新增' : '只读'}
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
              刷新
            </Button>
            {canCreateSticker ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={openCreate}>
                新建分组
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label="表情包分组指标">
        <ConsoleMetricCard label="全部分组" value={groups.length} description="当前可见分组总数" />
        <ConsoleMetricCard label="当前结果" value={filteredGroups.length} description="当前筛选后的分组" tone="info" />
        <ConsoleMetricCard label="启用分组" value={enabledGroups} description="当前启用分组数量" tone="success" />
        <ConsoleMetricCard label="表情总数" value={totalStickers} description="所有分组表情合计" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选分组"
            description="按分组名称、Code 和描述定位表情包分组。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                className="sticker-list-filter-input"
                placeholder="搜索名称 / Code / 描述"
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
          <h3>分组摘要</h3>
          <p className="admin-feature-subtle">用于核对当前分组范围、类型分布和可执行动作。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? '名称 / Code / 描述筛选' : '全部表情包分组'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">付费分组</span>
              <span className="admin-table-summary__value">{paidGroups}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">启停权限</span>
              <span className="admin-table-summary__value">
                {canToggleSticker ? '可启用 / 禁用' : '仅可查看启停状态'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分组维护</span>
              <span className="admin-table-summary__value">
                {canEditSticker ? '可编辑分组并管理表情' : '仅可查看分组'}
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
