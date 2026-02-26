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
} from '@radish/ui';
import {
  batchUpdateStickerSort,
  deleteSticker,
  getGroupStickers,
  type StickerVo,
} from '@/api/stickerApi';
import { ROUTES } from '@/router';
import { log } from '@/utils/logger';
import { StickerForm } from './StickerForm';
import { StickerBatchUploadModal } from './StickerBatchUploadModal';

const getPreviewUrl = (sticker: StickerVo) => sticker.voThumbnailUrl || sticker.voImageUrl;

export const StickerList = () => {
  const navigate = useNavigate();
  const { groupId } = useParams<{ groupId: string }>();
  const parsedGroupId = Number(groupId || 0);

  useDocumentTitle('分组表情管理');

  const [loading, setLoading] = useState(false);
  const [savingSort, setSavingSort] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [stickers, setStickers] = useState<StickerVo[]>([]);
  const [sortDrafts, setSortDrafts] = useState<Record<number, number>>({});
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingSticker, setEditingSticker] = useState<StickerVo | undefined>(undefined);
  const [batchModalVisible, setBatchModalVisible] = useState(false);

  const loadStickers = async () => {
    if (parsedGroupId <= 0) {
      return;
    }

    try {
      setLoading(true);
      const data = await getGroupStickers(parsedGroupId);
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
    void loadStickers();
  }, [parsedGroupId]);

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

  const handleDelete = async (id: number) => {
    try {
      await deleteSticker(id);
      message.success('删除表情成功');
      await loadStickers();
    } catch (error) {
      log.error('StickerList', '删除表情失败:', error);
      message.error('删除表情失败');
    }
  };

  const handleSortChange = (id: number, value: number | null) => {
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
          id: Number(id),
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
        <div style={{ width: 40, height: 40, borderRadius: 6, overflow: 'hidden', border: '1px solid #f0f0f0' }}>
          <img
            src={getPreviewUrl(record)}
            alt={record.voName}
            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
          />
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
        </Space>
      ),
    },
  ];

  if (parsedGroupId <= 0) {
    return (
      <div>
        <p>分组 ID 无效</p>
        <Button
          onClick={() => {
            navigate(ROUTES.STICKERS);
          }}
        >
          返回分组列表
        </Button>
      </div>
    );
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2 style={{ margin: 0 }}>分组表情管理（GroupId: {parsedGroupId}）</h2>
        <Space>
          <Button
            onClick={() => {
              navigate(ROUTES.STICKERS);
            }}
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
          <Button
            disabled={savingSort}
            onClick={() => {
              void handleSaveSort();
            }}
          >
            保存排序
          </Button>
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
          <Button
            onClick={() => {
              setBatchModalVisible(true);
            }}
          >
            批量上传
          </Button>
        </Space>
      </div>

      <div style={{ marginBottom: 16 }}>
        <Input
          allowClear
          placeholder="搜索名称 / Code"
          prefix={<SearchOutlined />}
          style={{ width: 280 }}
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </div>

      <Table<StickerVo>
        rowKey="voId"
        loading={loading}
        columns={columns}
        dataSource={filteredStickers}
        scroll={{ x: 1200 }}
        pagination={false}
      />

      <StickerForm
        visible={formVisible}
        groupId={parsedGroupId}
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
        groupId={parsedGroupId}
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
