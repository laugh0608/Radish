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
import { usePermission } from '@/hooks/usePermission';
import { getAvatarUrl } from '@/config/env';
import { ROUTES } from '@/router';
import { log } from '@/utils/logger';
import { StickerGroupForm } from './StickerGroupForm';
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
        coverImageUrl: group.voCoverImageUrl || undefined,
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

  if (!canViewStickers) {
    return <div className="sticker-group-list-page"><p>当前账号暂无表情包管理访问权限。</p></div>;
  }

  return (
    <div className="sticker-group-list-page">
      <div className="page-header">
        <h2>表情包管理</h2>
        <Space>
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
        </Space>
      </div>

      <div className="filter-bar">
        <Input
          allowClear
          placeholder="搜索名称 / Code / 描述"
          prefix={<SearchOutlined />}
          style={{ width: 280 }}
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </div>

      <Table<StickerGroupVo>
        rowKey="voId"
        loading={loading}
        columns={columns}
        dataSource={filteredGroups}
        scroll={{ x: 1200 }}
        pagination={false}
      />

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
