import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
import { usePermission } from '@/hooks/usePermission';
import { ROUTES } from '@/router/routes';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import { StickerForm } from './StickerForm';
import { StickerBatchUploadModal } from './StickerBatchUploadModal';
import '../adminFeature.css';
import './StickerList.css';

const getPreviewUrl = (sticker: StickerVo) => getAvatarUrl(sticker.voThumbnailUrl || sticker.voImageUrl);

export const StickerList = () => {
  const navigate = useNavigate();
  const { groupId } = useParams<{ groupId: string }>();
  const normalizedGroupId = String(groupId || '').trim();
  const isValidGroupId = /^[1-9]\d*$/.test(normalizedGroupId);

  useDocumentTitle('分组表情管理');

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

  const loadStickers = async () => {
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
      message.error('加载分组表情失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canViewStickers) {
      return;
    }

    void loadStickers();
  }, [normalizedGroupId, isValidGroupId, canViewStickers]);

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
      message.success('删除表情成功');
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '删除表情失败:', error);
      message.error('删除表情失败');
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
      message.info('没有待保存的排序变更');
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

      message.success('排序已更新');
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '批量更新排序失败:', error);
      message.error('批量更新排序失败');
    } finally {
      setSavingSort(false);
    }
  };

  const columns: TableColumnsType<StickerVo> = [
    {
      title: '预览',
      key: 'preview',
      width: 90,
      render: (_, record) => (
        <div className="sticker-item-preview">
          <img src={getPreviewUrl(record)} alt={record.voName} />
        </div>
      ),
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
      key: 'voIsAnimated',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsAnimated ? 'blue' : 'default'}>
          {record.voIsAnimated ? 'GIF' : '静图'}
        </Tag>
      ),
    },
    {
      title: '允许内嵌',
      key: 'voAllowInline',
      width: 110,
      render: (_, record) => (
        <Tag color={record.voAllowInline ? 'success' : 'default'}>
          {record.voAllowInline ? '允许' : '仅Reaction'}
        </Tag>
      ),
    },
    {
      title: '状态',
      key: 'voIsEnabled',
      width: 90,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {record.voIsEnabled ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '使用次数',
      dataIndex: 'voUseCount',
      key: 'voUseCount',
      width: 100,
    },
    {
      title: '排序',
      key: 'voSort',
      width: 120,
      render: (_, record) => (
        <InputNumber
          disabled={!canSortSticker}
          min={0}
          value={sortDrafts[record.voId] ?? record.voSort}
          onChange={(value) => handleSortChange(record.voId, value)}
          style={{ width: '100%' }}
        />
      ),
    },
    {
      title: '操作',
      key: 'actions',
      width: 220,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
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
              编辑
            </Button>
          ) : null}

          {canDeleteStickerPermission ? (
            <Popconfirm
              title="确认删除表情"
              description="删除后将软删除该表情，确认继续？"
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

  if (!isValidGroupId) {
    return (
      <div className="admin-feature-page sticker-item-list-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <AppstoreOutlined /> 分组表情管理
              </h2>
              <p className="admin-feature-subtle">分组 ID 无效，无法加载表情列表。</p>
            </div>
            <Button
              icon={<LeftOutlined />}
              onClick={() => {
                navigate(ROUTES.STICKERS);
              }}
            >
              返回分组列表
            </Button>
          </div>
        </section>
      </div>
    );
  }
  return (
    <div className="admin-feature-page sticker-item-list-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <AppstoreOutlined /> 分组表情管理
            </h2>
            <p className="admin-feature-subtle">当前分组：{normalizedGroupId}。维护表情图片、编码、内嵌能力和排序权重。</p>
          </div>
          <div className="sticker-list-header-actions">
            <Button
              onClick={() => {
                navigate(ROUTES.STICKERS);
              }}
              icon={<LeftOutlined />}
            >
              返回分组列表
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void loadStickers();
              }}
            >
              刷新
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
                新增表情
              </Button>
            ) : null}
            {canBatchUploadSticker ? (
              <Button
                onClick={() => {
                  setBatchModalVisible(true);
                }}
              >
                批量上传
              </Button>
            ) : null}
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="分组表情指标">
        <div className="admin-feature-metric">
          全部表情
          <strong>{stickers.length}</strong>
        </div>
        <div className="admin-feature-metric">
          当前结果
          <strong>{filteredStickers.length}</strong>
        </div>
        <div className="admin-feature-metric">
          启用表情
          <strong>{enabledStickers}</strong>
        </div>
        <div className="admin-feature-metric">
          排序草稿
          <strong>{sortDraftCount}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label="分组表情筛选">
            <div className="admin-table-toolbar__title">
              <span>筛选表情</span>
              <Tag>{activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}</Tag>
            </div>
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                className="sticker-list-filter-input"
                placeholder="搜索名称 / Code"
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
              {canSortSticker ? (
                <Button
                  disabled={savingSort}
                  onClick={() => {
                    void handleSaveSort();
                  }}
                >
                  保存排序
                </Button>
              ) : null}
            </div>
          </section>

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
          <h3>表情摘要</h3>
          <p className="admin-feature-subtle">用于核对当前分组筛选、排序草稿和表情类型分布。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分组 ID</span>
              <span className="admin-table-summary__value">{normalizedGroupId}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? '名称 / Code 筛选' : '全部分组表情'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">GIF / 内嵌</span>
              <span className="admin-table-summary__value">{animatedStickers} / {inlineStickers}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">排序权限</span>
              <span className="admin-table-summary__value">
                {canSortSticker ? '可调整排序' : '仅可查看排序'}
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
