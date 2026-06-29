import { useState, useEffect } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
  AntModal as Modal,
  AntSelect as Select,
  AntInput as Input,
  type TableColumnsType,
} from '@radish/ui';
import { Grid, Upload } from 'antd';
import type { UploadProps } from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  SearchOutlined,
  SettingOutlined,
} from '@radish/ui';
import {
  getSystemConfigs,
  getConfigCategories,
  getConfigChangeLogs,
  updateConfig,
  restoreConfigDefault,
  type SystemConfigChangeLogVo,
  type SystemConfigVo,
} from '@/api/systemConfigApi';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { getAvatarUrl } from '@/config/env';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { SystemConfigForm } from './SystemConfigForm';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './SystemConfigList.css';

const SITE_FAVICON_KEY = 'Site.Branding.FaviconUrl';
const DEFAULT_SITE_FAVICON_PATH = '/uploads/DefaultIco/bailuobo.ico';

export const SystemConfigList = () => {
  useDocumentTitle('系统设置');
  const [configs, setConfigs] = useState<SystemConfigVo[]>([]);
  const [filteredConfigs, setFilteredConfigs] = useState<SystemConfigVo[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [faviconUploading, setFaviconUploading] = useState(false);
  const [faviconSaving, setFaviconSaving] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [editingConfigId, setEditingConfigId] = useState<number>();
  const [historyVisible, setHistoryVisible] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyConfig, setHistoryConfig] = useState<SystemConfigVo>();
  const [historyLogs, setHistoryLogs] = useState<SystemConfigChangeLogVo[]>([]);
  const canViewSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigView);
  const canEditSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigEdit);
  const screens = Grid.useBreakpoint();
  const isCompactTable = !screens.md;

  // 筛选条件
  const [selectedCategory, setSelectedCategory] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');
  const activeFilterCount = [
    selectedCategory ? 'category' : undefined,
    searchKeyword.trim() ? 'keyword' : undefined,
  ].filter(Boolean).length;

  // 加载系统设置列表
  const loadConfigs = async () => {
    try {
      setLoading(true);
      const data = await getSystemConfigs();
      setConfigs(data);
      setFilteredConfigs(data);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置列表失败:', error);
      message.error('加载系统设置列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 加载系统设置分类
  const loadCategories = async () => {
    try {
      const data = await getConfigCategories();
      setCategories(data);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置分类失败:', error);
    }
  };

  useEffect(() => {
    if (!canViewSystemConfig) {
      return;
    }

    void loadConfigs();
    void loadCategories();
  }, [canViewSystemConfig]);

  // 筛选系统设置
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
  const faviconPreviewUrl = getAvatarUrl(faviconConfig?.voEffectiveValue || DEFAULT_SITE_FAVICON_PATH);
  const isUsingDefaultFavicon = !faviconConfig || !faviconConfig.voIsOverridden;
  const overriddenConfigs = filteredConfigs.filter((config) => config.voIsOverridden).length;
  const editableConfigs = filteredConfigs.filter((config) => config.voIsEditable).length;

  const handleRestoreDefaultFavicon = async () => {
    if (!faviconConfig) {
      message.error('站点图标配置尚未加载完成');
      return;
    }

    try {
      setFaviconSaving(true);
      await restoreConfigDefault(faviconConfig.voId, {
        reason: '恢复站点 favicon 默认值',
        confirmRiskLevel: faviconConfig.voRiskLevel,
        confirmKey: faviconConfig.voKey,
        expectedVersion: faviconConfig.voVersion,
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
        isEnabled: true,
        reason: `上传站点 favicon：${file.name}`,
        confirmRiskLevel: faviconConfig.voRiskLevel,
        confirmKey: faviconConfig.voKey,
        expectedVersion: faviconConfig.voVersion,
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

  // 编辑系统设置覆盖值
  const handleEdit = (record: SystemConfigVo) => {
    setEditingConfigId(record.voId);
    setFormVisible(true);
  };

  const handleViewHistory = async (record: SystemConfigVo) => {
    setHistoryConfig(record);
    setHistoryVisible(true);
    setHistoryLoading(true);
    try {
      const logs = await getConfigChangeLogs(record.voId, 20);
      setHistoryLogs(logs);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置变更历史失败:', error);
      message.error(error instanceof Error ? error.message : '加载系统设置变更历史失败');
      setHistoryLogs([]);
    } finally {
      setHistoryLoading(false);
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

  const getRiskTagColor = (riskLevel: string) => {
    const colorMap: Record<string, string> = {
      Low: 'success',
      Medium: 'warning',
      High: 'error',
      Critical: 'magenta',
    };
    return colorMap[riskLevel] || 'default';
  };

  const formatEffectiveMode = (effectiveMode: string) => {
    const modeMap: Record<string, string> = {
      Immediate: '立即生效',
      RestartRequired: '重启生效',
    };
    return modeMap[effectiveMode] || effectiveMode;
  };

  const formatChangeAction = (actionType: string) => {
    const actionMap: Record<string, string> = {
      UpdateOverride: '更新覆盖值',
      RestoreDefault: '恢复默认',
    };
    return actionMap[actionType] || actionType;
  };

  const formatOperator = (record: SystemConfigChangeLogVo) => {
    if (record.voOperatorUserName && record.voOperatorUserId) {
      return `${record.voOperatorUserName} #${record.voOperatorUserId}`;
    }
    return record.voOperatorUserName || (record.voOperatorUserId ? `#${record.voOperatorUserId}` : '-');
  };

  const renderEffectiveValue = (value: string, record: SystemConfigVo) => {
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
  };

  const renderConfigActions = (record: SystemConfigVo, compact = false) => (
    <Space
      className="system-config-action-space"
      size={compact ? 4 : 'small'}
      orientation={compact ? 'vertical' : 'horizontal'}
    >
      <Button
        variant="ghost"
        size="small"
        icon={<EditOutlined />}
        aria-label={compact ? '编辑' : undefined}
        title={compact ? '编辑' : undefined}
        onClick={() => handleEdit(record)}
        disabled={!canEditSystemConfig || !record.voIsEditable}
      >
        {compact ? null : '编辑'}
      </Button>
      <Button
        variant="ghost"
        size="small"
        icon={<ClockCircleOutlined />}
        aria-label={compact ? '历史' : undefined}
        title={compact ? '历史' : undefined}
        onClick={() => {
          void handleViewHistory(record);
        }}
      >
        {compact ? null : '历史'}
      </Button>
    </Space>
  );

  const renderCompactConfig = (record: SystemConfigVo) => (
    <div className="system-config-mobile-config">
      <div className="system-config-mobile-config__header">
        <span className="system-config-mobile-config__name">{record.voName}</span>
        <Tag color="cyan">{record.voCategory}</Tag>
      </div>
      <code className="system-config-key-code system-config-key-code--mobile">
        {record.voKey}
      </code>
      <div className="system-config-mobile-config__value">
        <span className="system-config-mobile-config__label">当前值</span>
        <div className="system-config-mobile-config__current">
          {renderEffectiveValue(record.voEffectiveValue, record)}
        </div>
      </div>
    </div>
  );

  const historyColumns: TableColumnsType<SystemConfigChangeLogVo> = [
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 170,
      render: (time: string) => time ? new Date(time).toLocaleString('zh-CN') : '-',
    },
    {
      title: '动作',
      dataIndex: 'voActionType',
      key: 'voActionType',
      width: 110,
      render: (actionType: string) => (
        <Tag color={actionType === 'RestoreDefault' ? 'default' : 'processing'}>
          {formatChangeAction(actionType)}
        </Tag>
      ),
    },
    {
      title: '旧值',
      dataIndex: 'voOldValue',
      key: 'voOldValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => value || '-',
    },
    {
      title: '新值',
      dataIndex: 'voNewValue',
      key: 'voNewValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => value || '-',
    },
    {
      title: '原因',
      dataIndex: 'voReason',
      key: 'voReason',
      width: 220,
      ellipsis: true,
    },
    {
      title: '操作者',
      key: 'operator',
      width: 150,
      render: (_, record) => formatOperator(record),
    },
    {
      title: '来源',
      dataIndex: 'voRequestIp',
      key: 'voRequestIp',
      width: 140,
      render: (ip: string) => ip || '-',
    },
  ];

  const desktopColumns: TableColumnsType<SystemConfigVo> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 70,
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
      title: '设置名称',
      dataIndex: 'voName',
      key: 'voName',
      width: 150,
    },
    {
      title: '设置键',
      dataIndex: 'voKey',
      key: 'voKey',
      width: 220,
      render: (key: string) => (
        <code className="system-config-key-code">
          {key}
        </code>
      ),
    },
    {
      title: '当前值',
      dataIndex: 'voEffectiveValue',
      key: 'voEffectiveValue',
      width: 190,
      ellipsis: true,
      render: renderEffectiveValue,
    },
    {
      title: '默认值',
      dataIndex: 'voDefaultValue',
      key: 'voDefaultValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => <span>{value}</span>,
    },
    {
      title: '覆盖状态',
      dataIndex: 'voIsOverridden',
      key: 'voIsOverridden',
      width: 100,
      render: (isOverridden: boolean) => (
        <Tag color={isOverridden ? 'processing' : 'default'}>
          {isOverridden ? '已覆盖' : '使用默认'}
        </Tag>
      ),
    },
    {
      title: '风险',
      dataIndex: 'voRiskLevel',
      key: 'voRiskLevel',
      width: 90,
      render: (riskLevel: string) => (
        <Tag color={getRiskTagColor(riskLevel)}>{riskLevel}</Tag>
      ),
    },
    {
      title: '生效方式',
      dataIndex: 'voEffectiveMode',
      key: 'voEffectiveMode',
      width: 110,
      render: (effectiveMode: string) => (
        <Tag>{formatEffectiveMode(effectiveMode)}</Tag>
      ),
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
      title: '覆盖时间',
      dataIndex: 'voModifyTime',
      key: 'voModifyTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString('zh-CN') : '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 230,
      fixed: 'right',
      render: (_, record) => renderConfigActions(record),
    },
  ];

  const compactColumns: TableColumnsType<SystemConfigVo> = [
    {
      title: '设置',
      key: 'compactConfig',
      width: 224,
      render: (_, record) => renderCompactConfig(record),
    },
    {
      title: '操作',
      key: 'action',
      width: 60,
      render: (_, record) => renderConfigActions(record, true),
    },
  ];

  const tableColumns = isCompactTable ? compactColumns : desktopColumns;
  const tableScrollX = isCompactTable ? 292 : 1700;

  return (
    <div className="admin-feature-page system-config-list-page">
      <ConsolePageHeader
        eyebrow="SYSTEM POLICY"
        title="系统设置"
        description="按注册定义查看默认值、当前覆盖值、风险等级和生效方式。"
        icon={<SettingOutlined />}
        status={(
          <ConsoleStatusChip tone={canEditSystemConfig ? 'success' : 'neutral'}>
            {canEditSystemConfig ? '可编辑低风险设置' : '只读查看'}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="system-config-header-actions">
            <Button
              icon={<ReloadOutlined />}
              onClick={loadConfigs}
            >
              刷新
            </Button>
          </div>
        )}
      />

      <ConsoleMetricGrid label="系统设置指标">
        <ConsoleMetricCard label="注册设置" value={configs.length} description="代码注册的设置定义" tone="info" />
        <ConsoleMetricCard label="当前结果" value={filteredConfigs.length} description="当前筛选后的设置数量" />
        <ConsoleMetricCard label="已覆盖" value={overriddenConfigs} description="存在运行时覆盖值" tone="warning" />
        <ConsoleMetricCard label="可编辑" value={editableConfigs} description="允许在 Console 调整" tone="success" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="branding-card">
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
                    {isUsingDefaultFavicon ? '使用默认' : '已覆盖'}
                  </Tag>
                  <Tag color="success">
                    Low
                  </Tag>
                </div>
                <p className="branding-card__description">
                  当前浏览器标签页左侧显示的站点图标。默认文件来自 `DataBases/Uploads/DefaultIco/bailuobo.ico`，修改后立即生效。
                </p>
                <code className="branding-card__value">{faviconConfig?.voEffectiveValue || DEFAULT_SITE_FAVICON_PATH}</code>
              </div>
            </div>
            <div className="branding-card__actions">
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
            </div>
          </section>

          <ConsoleToolbar
            title="筛选设置"
            description="按分类、设置名称、设置键或说明定位配置项。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Select
                className="system-config-filter-select"
                placeholder="选择分类"
                value={selectedCategory || undefined}
                onChange={setSelectedCategory}
                allowClear
                options={categories.map(category => ({ label: category, value: category }))}
              />
              <Input
                className="system-config-filter-input"
                placeholder="搜索设置名称、键或描述"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                prefix={<SearchOutlined />}
                allowClear
              />
              <Button onClick={handleResetFilter}>重置</Button>
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table
              columns={tableColumns}
              dataSource={filteredConfigs}
              rowKey="voId"
              loading={loading}
              pagination={{
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => `共 ${total} 条`,
              }}
              scroll={{ x: tableScrollX }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>设置摘要</h3>
          <p className="admin-feature-subtle">用于核对当前注册设置范围、品牌图标状态和编辑权限。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部设置'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">站点图标</span>
              <span className="admin-table-summary__value">
                {isUsingDefaultFavicon ? '使用默认' : '已覆盖'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">设置分类</span>
              <span className="admin-table-summary__value">{categories.length} 类</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">编辑权限</span>
              <span className="admin-table-summary__value">
                {canEditSystemConfig ? '可编辑低风险设置' : '仅可查看设置'}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <SystemConfigForm
        visible={formVisible}
        configId={editingConfigId}
        onCancel={() => setFormVisible(false)}
        onSuccess={handleFormSuccess}
      />

      <Modal
        title="系统设置变更历史"
        open={historyVisible}
        onCancel={() => {
          setHistoryVisible(false);
          setHistoryConfig(undefined);
          setHistoryLogs([]);
        }}
        footer={null}
        width={960}
        destroyOnHidden
      >
        <div className="system-config-history-summary">
          <span>{historyConfig?.voName || '系统设置'}</span>
          {historyConfig ? <code>{historyConfig.voKey}</code> : null}
          {historyConfig ? <Tag color={getRiskTagColor(historyConfig.voRiskLevel)}>{historyConfig.voRiskLevel}</Tag> : null}
        </div>
        <Table
          columns={historyColumns}
          dataSource={historyLogs}
          rowKey="voId"
          loading={historyLoading}
          pagination={false}
          scroll={{ x: 1150 }}
          locale={{ emptyText: '暂无变更历史' }}
        />
      </Modal>
    </div>
  );
};
