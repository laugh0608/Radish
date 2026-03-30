import { useState, useEffect } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
  Popconfirm,
  AntSelect as Select,
  AntInput as Input,
  type TableColumnsType,
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@radish/ui';
import {
  getSystemConfigs,
  getConfigCategories,
  deleteConfig,
  updateConfig,
  type SystemConfigVo,
} from '@/api/systemConfigApi';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { getAvatarUrl } from '@/config/env';
import { SystemConfigForm } from './SystemConfigForm';
import { log } from '@/utils/logger';
import './SystemConfigList.css';

const SITE_FAVICON_KEY = 'Site.Branding.FaviconUrl';
const DEFAULT_SITE_FAVICON_PATH = '/uploads/DefaultIco/bailuobo.ico';

export const SystemConfigList = () => {
  useDocumentTitle('系统配置');
  const [configs, setConfigs] = useState<SystemConfigVo[]>([]);
  const [filteredConfigs, setFilteredConfigs] = useState<SystemConfigVo[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [faviconUploading, setFaviconUploading] = useState(false);
  const [faviconSaving, setFaviconSaving] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingConfigId, setEditingConfigId] = useState<number>();
  const canViewSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigView);
  const canCreateSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigCreate);
  const canEditSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigEdit);
  const canDeleteSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigDelete);

  // 筛选条件
  const [selectedCategory, setSelectedCategory] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');

  // 加载配置列表
  const loadConfigs = async () => {
    try {
      setLoading(true);
      const data = await getSystemConfigs();
      setConfigs(data);
      setFilteredConfigs(data);
    } catch (error) {
      log.error('SystemConfigList', '加载配置列表失败:', error);
      message.error('加载配置列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 加载配置分类
  const loadCategories = async () => {
    try {
      const data = await getConfigCategories();
      setCategories(data);
    } catch (error) {
      log.error('SystemConfigList', '加载配置分类失败:', error);
    }
  };

  useEffect(() => {
    if (!canViewSystemConfig) {
      return;
    }

    void loadConfigs();
    void loadCategories();
  }, [canViewSystemConfig]);

  // 筛选配置
  useEffect(() => {
    let filtered = configs;

    // 按分类筛选
    if (selectedCategory) {
      filtered = filtered.filter(config => config.voCategory === selectedCategory);
    }

    // 按关键词搜索
    if (searchKeyword) {
      const keyword = searchKeyword.toLowerCase();
      filtered = filtered.filter(config =>
        config.voName.toLowerCase().includes(keyword) ||
        config.voKey.toLowerCase().includes(keyword) ||
        config.voDescription?.toLowerCase().includes(keyword)
      );
    }

    setFilteredConfigs(filtered);
  }, [configs, selectedCategory, searchKeyword]);

  const faviconConfig = configs.find((config) => config.voKey === SITE_FAVICON_KEY);
  const faviconPreviewUrl = getAvatarUrl(faviconConfig?.voValue || DEFAULT_SITE_FAVICON_PATH);
  const isUsingDefaultFavicon = !faviconConfig || faviconConfig.voValue === DEFAULT_SITE_FAVICON_PATH;

  const handleRestoreDefaultFavicon = async () => {
    if (!faviconConfig) {
      message.error('站点图标配置尚未加载完成');
      return;
    }

    try {
      setFaviconSaving(true);
      await updateConfig(faviconConfig.voId, {
        value: DEFAULT_SITE_FAVICON_PATH,
        description: faviconConfig.voDescription,
        isEnabled: true,
      });
      message.success('已恢复为默认站点图标');
      await loadConfigs();
    } catch (error) {
      log.error('SystemConfigList', '恢复默认站点图标失败:', error);
      message.error(error instanceof Error ? error.message : '恢复默认站点图标失败');
    } finally {
      setFaviconSaving(false);
    }
  };

  const handleFaviconUpload: UploadProps['customRequest'] = async (options) => {
    const file = options.file;
    if (!(file instanceof File)) {
      const uploadError = new Error('无效的图标文件');
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    if (!/\.ico$/i.test(file.name)) {
      const uploadError = new Error('仅支持上传 .ico 格式的图标文件');
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    if (!faviconConfig) {
      const uploadError = new Error('站点图标配置尚未加载完成');
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    try {
      setFaviconUploading(true);
      const uploaded = await uploadAttachmentImage(
        file,
        {
          businessType: 'SiteFavicon',
          generateThumbnail: false,
          removeExif: false,
        },
        (percent) => {
          options.onProgress?.({ percent });
        }
      );

      if (!uploaded.url) {
        throw new Error('上传成功但未返回图标地址');
      }

      setFaviconSaving(true);
      await updateConfig(faviconConfig.voId, {
        value: uploaded.url,
        description: faviconConfig.voDescription,
        isEnabled: true,
      });
      await loadConfigs();
      options.onSuccess?.(uploaded);
      message.success('站点图标已更新');
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error('站点图标上传失败');
      log.error('SystemConfigList', '上传站点图标失败:', error);
      options.onError?.(uploadError);
      message.error(uploadError.message);
    } finally {
      setFaviconUploading(false);
      setFaviconSaving(false);
    }
  };

  // 新增配置
  const handleCreate = () => {
    setFormMode('create');
    setEditingConfigId(undefined);
    setFormVisible(true);
  };

  // 编辑配置
  const handleEdit = (record: SystemConfigVo) => {
    setFormMode('edit');
    setEditingConfigId(record.voId);
    setFormVisible(true);
  };

  // 删除配置
  const handleDelete = async (id: number) => {
    try {
      await deleteConfig(id);
      message.success('删除配置成功');
      loadConfigs();
    } catch (error) {
      log.error('SystemConfigList', '删除配置失败:', error);
      message.error('删除配置失败');
    }
  };

  // 表单提交成功
  const handleFormSuccess = () => {
    setFormVisible(false);
    loadConfigs();
  };

  // 重置筛选条件
  const handleResetFilter = () => {
    setSelectedCategory('');
    setSearchKeyword('');
  };

  // 获取类型标签颜色
  const getTypeTagColor = (type: string) => {
    const colorMap: Record<string, string> = {
      string: 'blue',
      number: 'green',
      boolean: 'orange',
      json: 'purple',
    };
    return colorMap[type] || 'default';
  };

  // 表格列定义
  const columns: TableColumnsType<SystemConfigVo> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 80,
    },
    {
      title: '分类',
      dataIndex: 'voCategory',
      key: 'voCategory',
      width: 120,
      render: (category: string) => (
        <Tag color="cyan">{category}</Tag>
      ),
    },
    {
      title: '配置名称',
      dataIndex: 'voName',
      key: 'voName',
      width: 150,
    },
    {
      title: '配置键',
      dataIndex: 'voKey',
      key: 'voKey',
      width: 200,
      render: (key: string) => (
        <code style={{ fontSize: '12px', background: '#f5f5f5', padding: '2px 4px', borderRadius: '2px' }}>
          {key}
        </code>
      ),
    },
    {
      title: '配置值',
      dataIndex: 'voValue',
      key: 'voValue',
      width: 150,
      ellipsis: true,
      render: (value: string, record) => {
        if (record.voType === 'boolean') {
          return <Tag color={value === 'true' ? 'success' : 'error'}>{value}</Tag>;
        }
        if (record.voKey === SITE_FAVICON_KEY) {
          return (
            <div className="favicon-cell">
              {faviconPreviewUrl ? (
                <img src={faviconPreviewUrl} alt="站点图标预览" className="favicon-cell__image" />
              ) : null}
              <span className="favicon-cell__value">{value}</span>
            </div>
          );
        }
        return <span>{value}</span>;
      },
    },
    {
      title: '类型',
      dataIndex: 'voType',
      key: 'voType',
      width: 80,
      render: (type: string) => (
        <Tag color={getTypeTagColor(type)}>{type}</Tag>
      ),
    },
    {
      title: '描述',
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
    },
    {
      title: '状态',
      key: 'status',
      width: 80,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {record.voIsEnabled ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '修改时间',
      dataIndex: 'voModifyTime',
      key: 'voModifyTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString('zh-CN') : '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 160,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            variant="ghost"
            size="small"
            icon={<EditOutlined />}
            onClick={() => handleEdit(record)}
            disabled={!canEditSystemConfig}
          >
            编辑
          </Button>
          {canDeleteSystemConfig ? (
            record.voKey === SITE_FAVICON_KEY ? null : (
            <Popconfirm
              title="确认删除"
              description="确定要删除这个配置吗？此操作不可恢复。"
              onConfirm={() => handleDelete(record.voId)}
              okText="确定"
              cancelText="取消"
            >
              <Button
                variant="danger"
                size="small"
                icon={<DeleteOutlined />}
              >
                删除
              </Button>
            </Popconfirm>
            )
          ) : null}
        </Space>
      ),
    },
  ];
  return (
    <div className="system-config-list-page">
      <div className="page-header">
        <h2>系统配置</h2>
        <Space>
          <Button
            icon={<ReloadOutlined />}
            onClick={loadConfigs}
          >
            刷新
          </Button>
          {canCreateSystemConfig ? (
            <Button
              variant="primary"
              icon={<PlusOutlined />}
              onClick={handleCreate}
            >
              新增配置
            </Button>
          ) : null}
        </Space>
      </div>

      <div className="branding-card">
        <div className="branding-card__main">
          <div className="branding-card__preview">
            {faviconPreviewUrl ? (
              <img src={faviconPreviewUrl} alt="当前站点图标" className="branding-card__image" />
            ) : (
              <span className="branding-card__empty">暂无图标</span>
            )}
          </div>
          <div className="branding-card__content">
            <div className="branding-card__title-row">
              <h3 className="branding-card__title">网站标签页图标</h3>
              <Tag color={isUsingDefaultFavicon ? 'default' : 'processing'}>
                {isUsingDefaultFavicon ? '默认种子' : '自定义图标'}
              </Tag>
            </div>
            <p className="branding-card__description">
              当前浏览器标签页左侧显示的站点图标。默认种子文件来自 `DataBases/Uploads/DefaultIco/bailuobo.ico`。
            </p>
            <code className="branding-card__value">{faviconConfig?.voValue || DEFAULT_SITE_FAVICON_PATH}</code>
          </div>
        </div>
        <Space wrap>
          <Upload
            accept=".ico"
            showUploadList={false}
            customRequest={handleFaviconUpload}
            disabled={!canEditSystemConfig || faviconUploading || faviconSaving}
          >
            <Button
              variant="primary"
              icon={<PlusOutlined />}
              disabled={!canEditSystemConfig || faviconUploading || faviconSaving}
            >
              {faviconUploading ? '上传中...' : '上传 ICO'}
            </Button>
          </Upload>
          <Button
            onClick={() => {
              void handleRestoreDefaultFavicon();
            }}
            disabled={!canEditSystemConfig || faviconUploading || faviconSaving || isUsingDefaultFavicon}
          >
            恢复默认
          </Button>
          <Button
            onClick={() => {
              void loadConfigs();
            }}
            disabled={faviconUploading || faviconSaving}
          >
            重新读取
          </Button>
        </Space>
      </div>

      {/* 筛选条件 */}
      <div className="filter-bar">
        <Space wrap>
          <Select
            placeholder="选择分类"
            style={{ width: 150 }}
            value={selectedCategory || undefined}
            onChange={setSelectedCategory}
            allowClear
            options={categories.map(category => ({ label: category, value: category }))}
          />
          <Input
            placeholder="搜索配置名称、键或描述"
            style={{ width: 250 }}
            value={searchKeyword}
            onChange={(e) => setSearchKeyword(e.target.value)}
            prefix={<SearchOutlined />}
            allowClear
          />
          <Button onClick={handleResetFilter}>重置</Button>
        </Space>
      </div>

      <Table
        columns={columns}
        dataSource={filteredConfigs}
        rowKey="voId"
        loading={loading}
        pagination={{
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total) => `共 ${total} 条`,
        }}
        scroll={{ x: 1400 }}
      />

      <SystemConfigForm
        visible={formVisible}
        mode={formMode}
        configId={editingConfigId}
        categories={categories}
        onCancel={() => setFormVisible(false)}
        onSuccess={handleFormSuccess}
      />
    </div>
  );
};
