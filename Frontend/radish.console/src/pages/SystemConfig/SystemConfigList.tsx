import { useCallback, useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
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
  formatLocalizedDateTime,
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
import {
  getSystemConfigCategoryLabel,
  getSystemConfigDescription,
  getSystemConfigName,
  getSystemConfigPresentation,
} from './systemConfigPresentation';
import '../adminFeature.css';
import './SystemConfigList.css';

const SITE_FAVICON_KEY = 'Site.Branding.FaviconUrl';
const DEFAULT_SITE_FAVICON_PATH = '/uploads/DefaultIco/bailuobo.ico';

export const SystemConfigList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('console.route.system-config'));
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
  const loadConfigs = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getSystemConfigs(t);
      setConfigs(data);
      setFilteredConfigs(data);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置列表失败:', error);
      message.error(error instanceof Error ? error.message : t('systemConfig.feedback.loadListFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  // 加载系统设置分类
  const loadCategories = useCallback(async () => {
    try {
      const data = await getConfigCategories(t);
      setCategories(data);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置分类失败:', error);
    }
  }, [t]);

  useEffect(() => {
    if (!canViewSystemConfig) {
      return;
    }

    void loadConfigs();
    void loadCategories();
  }, [canViewSystemConfig, loadCategories, loadConfigs]);

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
      filtered = filtered.filter((config) => {
        const presentation = getSystemConfigPresentation(config, t);
        return presentation.name.toLowerCase().includes(keyword)
          || config.voName.toLowerCase().includes(keyword)
          || config.voKey.toLowerCase().includes(keyword)
          || presentation.description.toLowerCase().includes(keyword)
          || config.voDescription?.toLowerCase().includes(keyword);
      });
    }

    setFilteredConfigs(filtered);
  }, [configs, selectedCategory, searchKeyword, t]);

  const faviconConfig = configs.find((config) => config.voKey === SITE_FAVICON_KEY);
  const faviconPreviewUrl = getAvatarUrl(faviconConfig?.voEffectiveValue || DEFAULT_SITE_FAVICON_PATH);
  const isUsingDefaultFavicon = !faviconConfig || !faviconConfig.voIsOverridden;
  const overriddenConfigs = filteredConfigs.filter((config) => config.voIsOverridden).length;
  const editableConfigs = filteredConfigs.filter((config) => config.voIsEditable).length;

  const handleRestoreDefaultFavicon = async () => {
    if (!canEditSystemConfig || !faviconConfig) {
      message.error(canEditSystemConfig
        ? t('systemConfig.feedback.faviconNotReady')
        : t('systemConfig.feedback.editDenied'));
      return;
    }

    try {
      setFaviconSaving(true);
      await restoreConfigDefault(faviconConfig.voId, {
        reason: t('systemConfig.reason.restoreFavicon'),
        confirmRiskLevel: faviconConfig.voRiskLevel,
        confirmKey: faviconConfig.voKey,
        expectedVersion: faviconConfig.voVersion,
      }, t);
      message.success(t('systemConfig.feedback.faviconRestored'));
      await loadConfigs();
    } catch (error) {
      log.error('SystemConfigList', '恢复默认站点图标失败:', error);
      message.error(error instanceof Error ? error.message : t('systemConfig.feedback.faviconRestoreFailed'));
    } finally {
      setFaviconSaving(false);
    }
  };

  const handleFaviconUpload: UploadProps['customRequest'] = async (options) => {
    const file = options.file;
    if (!canEditSystemConfig) {
      const uploadError = new Error(t('systemConfig.feedback.editDenied'));
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    if (!(file instanceof File)) {
      const uploadError = new Error(t('systemConfig.feedback.invalidIcon'));
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    if (!/\.ico$/i.test(file.name)) {
      const uploadError = new Error(t('systemConfig.feedback.icoOnly'));
      options.onError?.(uploadError);
      message.error(uploadError.message);
      return;
    }

    if (!faviconConfig) {
      const uploadError = new Error(t('systemConfig.feedback.faviconNotReady'));
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
        throw new Error(t('systemConfig.feedback.missingUploadUrl'));
      }

      setFaviconSaving(true);
      await updateConfig(faviconConfig.voId, {
        value: uploaded.url,
        isEnabled: true,
        reason: t('systemConfig.reason.uploadFavicon', { name: file.name }),
        confirmRiskLevel: faviconConfig.voRiskLevel,
        confirmKey: faviconConfig.voKey,
        expectedVersion: faviconConfig.voVersion,
      }, t);
      await loadConfigs();
      options.onSuccess?.(uploaded);
      message.success(t('systemConfig.feedback.faviconUpdated'));
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error(t('systemConfig.feedback.faviconUploadFailed'));
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
    if (!canEditSystemConfig || !record.voIsEditable) {
      message.error(canEditSystemConfig
        ? t('systemConfig.feedback.notEditable')
        : t('systemConfig.feedback.editDenied'));
      return;
    }

    setEditingConfigId(record.voId);
    setFormVisible(true);
  };

  const handleViewHistory = async (record: SystemConfigVo) => {
    setHistoryConfig(record);
    setHistoryVisible(true);
    setHistoryLoading(true);
    try {
      const logs = await getConfigChangeLogs(record.voId, 20, t);
      setHistoryLogs(logs);
    } catch (error) {
      log.error('SystemConfigList', '加载系统设置变更历史失败:', error);
      message.error(error instanceof Error ? error.message : t('systemConfig.feedback.historyLoadFailed'));
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
    return t(`systemConfig.mode.${effectiveMode}`, { defaultValue: effectiveMode });
  };

  const formatChangeAction = (actionType: string) => {
    return t(`systemConfig.action.${actionType}`, { defaultValue: actionType });
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
            <img src={faviconPreviewUrl} alt={t('systemConfig.branding.previewAlt')} className="favicon-cell__image" />
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
        aria-label={compact ? t('systemConfig.common.edit') : undefined}
        title={compact ? t('systemConfig.common.edit') : undefined}
        onClick={() => handleEdit(record)}
        disabled={!canEditSystemConfig || !record.voIsEditable}
      >
        {compact ? null : t('systemConfig.common.edit')}
      </Button>
      <Button
        variant="ghost"
        size="small"
        icon={<ClockCircleOutlined />}
        aria-label={compact ? t('systemConfig.common.history') : undefined}
        title={compact ? t('systemConfig.common.history') : undefined}
        onClick={() => {
          void handleViewHistory(record);
        }}
      >
        {compact ? null : t('systemConfig.common.history')}
      </Button>
    </Space>
  );

  const renderCompactConfig = (record: SystemConfigVo) => (
    <div className="system-config-mobile-config">
      <div className="system-config-mobile-config__header">
        <span className="system-config-mobile-config__name">{getSystemConfigName(record, t)}</span>
        <Tag color="cyan">{getSystemConfigCategoryLabel(record.voKey, record.voCategory, t)}</Tag>
      </div>
      <code className="system-config-key-code system-config-key-code--mobile">
        {record.voKey}
      </code>
      <div className="system-config-mobile-config__value">
        <span className="system-config-mobile-config__label">{t('systemConfig.common.currentValue')}</span>
        <div className="system-config-mobile-config__current">
          {renderEffectiveValue(record.voEffectiveValue, record)}
        </div>
      </div>
    </div>
  );

  const historyColumns: TableColumnsType<SystemConfigChangeLogVo> = [
    {
      title: t('systemConfig.history.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 170,
      render: (time: string) => time ? formatLocalizedDateTime(time, language) : '-',
    },
    {
      title: t('systemConfig.history.action'),
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
      title: t('systemConfig.history.oldValue'),
      dataIndex: 'voOldValue',
      key: 'voOldValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => value || '-',
    },
    {
      title: t('systemConfig.history.newValue'),
      dataIndex: 'voNewValue',
      key: 'voNewValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => value || '-',
    },
    {
      title: t('systemConfig.history.reason'),
      dataIndex: 'voReason',
      key: 'voReason',
      width: 220,
      ellipsis: true,
    },
    {
      title: t('systemConfig.history.operator'),
      key: 'operator',
      width: 150,
      render: (_, record) => formatOperator(record),
    },
    {
      title: t('systemConfig.history.source'),
      dataIndex: 'voRequestIp',
      key: 'voRequestIp',
      width: 140,
      render: (ip: string) => ip || '-',
    },
  ];

  const desktopColumns: TableColumnsType<SystemConfigVo> = [
    {
      title: t('systemConfig.table.id'),
      dataIndex: 'voId',
      key: 'voId',
      width: 70,
    },
    {
      title: t('systemConfig.table.category'),
      dataIndex: 'voCategory',
      key: 'voCategory',
      width: 120,
      render: (category: string, record) => (
        <Tag color="cyan">{getSystemConfigCategoryLabel(record.voKey, category, t)}</Tag>
      ),
    },
    {
      title: t('systemConfig.table.name'),
      dataIndex: 'voName',
      key: 'voName',
      width: 150,
      render: (_, record) => getSystemConfigName(record, t),
    },
    {
      title: t('systemConfig.table.key'),
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
      title: t('systemConfig.table.currentValue'),
      dataIndex: 'voEffectiveValue',
      key: 'voEffectiveValue',
      width: 190,
      ellipsis: true,
      render: renderEffectiveValue,
    },
    {
      title: t('systemConfig.table.defaultValue'),
      dataIndex: 'voDefaultValue',
      key: 'voDefaultValue',
      width: 180,
      ellipsis: true,
      render: (value: string) => <span>{value}</span>,
    },
    {
      title: t('systemConfig.table.overrideStatus'),
      dataIndex: 'voIsOverridden',
      key: 'voIsOverridden',
      width: 100,
      render: (isOverridden: boolean) => (
        <Tag color={isOverridden ? 'processing' : 'default'}>
          {isOverridden ? t('systemConfig.status.overridden') : t('systemConfig.status.default')}
        </Tag>
      ),
    },
    {
      title: t('systemConfig.table.risk'),
      dataIndex: 'voRiskLevel',
      key: 'voRiskLevel',
      width: 90,
      render: (riskLevel: string) => (
        <Tag color={getRiskTagColor(riskLevel)}>
          {t(`systemConfig.risk.${riskLevel}`, { defaultValue: riskLevel })}
        </Tag>
      ),
    },
    {
      title: t('systemConfig.table.effectiveMode'),
      dataIndex: 'voEffectiveMode',
      key: 'voEffectiveMode',
      width: 110,
      render: (effectiveMode: string) => (
        <Tag>{formatEffectiveMode(effectiveMode)}</Tag>
      ),
    },
    {
      title: t('systemConfig.table.type'),
      dataIndex: 'voType',
      key: 'voType',
      width: 80,
      render: (type: string) => (
        <Tag color={getTypeTagColor(type)}>
          {t(`systemConfig.type.${type}`, { defaultValue: type })}
        </Tag>
      ),
    },
    {
      title: t('systemConfig.table.description'),
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
      render: (_, record) => getSystemConfigDescription(record, t),
    },
    {
      title: t('systemConfig.table.overrideTime'),
      dataIndex: 'voModifyTime',
      key: 'voModifyTime',
      width: 180,
      render: (time: string) => time ? formatLocalizedDateTime(time, language) : '-',
    },
    {
      title: t('systemConfig.table.actions'),
      key: 'action',
      width: 230,
      fixed: 'right',
      render: (_, record) => renderConfigActions(record),
    },
  ];

  const compactColumns: TableColumnsType<SystemConfigVo> = [
    {
      title: t('systemConfig.table.compactSetting'),
      key: 'compactConfig',
      width: 224,
      render: (_, record) => renderCompactConfig(record),
    },
    {
      title: t('systemConfig.table.actions'),
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
        eyebrow={t('systemConfig.page.eyebrow')}
        title={t('systemConfig.page.title')}
        description={t('systemConfig.page.description')}
        icon={<SettingOutlined />}
        status={(
          <ConsoleStatusChip tone={canEditSystemConfig ? 'success' : 'neutral'}>
            {canEditSystemConfig ? t('systemConfig.page.editable') : t('systemConfig.page.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="system-config-header-actions">
            <Button
              icon={<ReloadOutlined />}
              onClick={loadConfigs}
            >
              {t('systemConfig.page.refresh')}
            </Button>
          </div>
        )}
      />

      <ConsoleMetricGrid label={t('systemConfig.metrics.label')}>
        <ConsoleMetricCard label={t('systemConfig.metrics.registered')} value={configs.length} description={t('systemConfig.metrics.registeredDescription')} tone="info" />
        <ConsoleMetricCard label={t('systemConfig.metrics.results')} value={filteredConfigs.length} description={t('systemConfig.metrics.resultsDescription')} />
        <ConsoleMetricCard label={t('systemConfig.metrics.overridden')} value={overriddenConfigs} description={t('systemConfig.metrics.overriddenDescription')} tone="warning" />
        <ConsoleMetricCard label={t('systemConfig.metrics.editable')} value={editableConfigs} description={t('systemConfig.metrics.editableDescription')} tone="success" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="branding-card">
            <div className="branding-card__main">
              <div className="branding-card__preview">
                {faviconPreviewUrl ? (
                  <img src={faviconPreviewUrl} alt={t('systemConfig.branding.currentAlt')} className="branding-card__image" />
                ) : (
                  <span className="branding-card__empty">{t('systemConfig.branding.empty')}</span>
                )}
              </div>
              <div className="branding-card__content">
                <div className="branding-card__title-row">
                  <h3 className="branding-card__title">{t('systemConfig.branding.title')}</h3>
                  <Tag color={isUsingDefaultFavicon ? 'default' : 'processing'}>
                    {isUsingDefaultFavicon ? t('systemConfig.status.default') : t('systemConfig.status.overridden')}
                  </Tag>
                  <Tag color="success">
                    Low
                  </Tag>
                </div>
                <p className="branding-card__description">
                  {t('systemConfig.branding.description')}
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
                  {faviconUploading ? t('systemConfig.branding.uploading') : t('systemConfig.branding.upload')}
                </Button>
              </Upload>
              <Button
                onClick={() => {
                  void handleRestoreDefaultFavicon();
                }}
                disabled={!canEditSystemConfig || faviconUploading || faviconSaving || isUsingDefaultFavicon}
              >
                {t('systemConfig.branding.restore')}
              </Button>
              <Button
                onClick={() => {
                  void loadConfigs();
                }}
                disabled={faviconUploading || faviconSaving}
              >
                {t('systemConfig.branding.reload')}
              </Button>
            </div>
          </section>

          <ConsoleToolbar
            title={t('systemConfig.filter.title')}
            description={t('systemConfig.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0
                  ? t('systemConfig.filter.active', { count: activeFilterCount })
                  : t('systemConfig.filter.none')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Select
                className="system-config-filter-select"
                placeholder={t('systemConfig.filter.categoryPlaceholder')}
                value={selectedCategory || undefined}
                onChange={setSelectedCategory}
                allowClear
                options={categories.map((category) => {
                  const categoryConfig = configs.find((config) => config.voCategory === category);
                  return {
                    label: categoryConfig
                      ? getSystemConfigCategoryLabel(categoryConfig.voKey, category, t)
                      : category,
                    value: category,
                  };
                })}
              />
              <Input
                className="system-config-filter-input"
                placeholder={t('systemConfig.filter.searchPlaceholder')}
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                prefix={<SearchOutlined />}
                allowClear
              />
              <Button onClick={handleResetFilter}>{t('systemConfig.filter.reset')}</Button>
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
                showTotal: (total) => t('systemConfig.pagination.total', { count: total }),
              }}
              scroll={{ x: tableScrollX }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('systemConfig.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('systemConfig.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('systemConfig.summary.queryScope')}</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0
                  ? t('systemConfig.summary.filtered', { count: activeFilterCount })
                  : t('systemConfig.summary.all')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('systemConfig.summary.favicon')}</span>
              <span className="admin-table-summary__value">
                {isUsingDefaultFavicon ? t('systemConfig.status.default') : t('systemConfig.status.overridden')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('systemConfig.summary.categories')}</span>
              <span className="admin-table-summary__value">{t('systemConfig.summary.categoryCount', { count: categories.length })}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('systemConfig.summary.permission')}</span>
              <span className="admin-table-summary__value">
                {canEditSystemConfig ? t('systemConfig.page.editable') : t('systemConfig.summary.viewOnly')}
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
        title={t('systemConfig.history.title')}
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
          <span>{historyConfig ? getSystemConfigName(historyConfig, t) : t('systemConfig.history.fallbackName')}</span>
          {historyConfig ? <code>{historyConfig.voKey}</code> : null}
          {historyConfig ? (
            <Tag color={getRiskTagColor(historyConfig.voRiskLevel)}>
              {t(`systemConfig.risk.${historyConfig.voRiskLevel}`, { defaultValue: historyConfig.voRiskLevel })}
            </Tag>
          ) : null}
        </div>
        <div className="admin-table-scroll-region">
          <Table
            columns={historyColumns}
            dataSource={historyLogs}
            rowKey="voId"
            loading={historyLoading}
            pagination={false}
            scroll={{ x: 1150 }}
            locale={{ emptyText: t('systemConfig.history.empty') }}
          />
        </div>
      </Modal>
    </div>
  );
};
