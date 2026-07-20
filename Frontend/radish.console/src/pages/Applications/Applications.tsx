import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { log } from '@/utils/logger';
import {
  Table,
  Button,
  Space,
  Tag,
  Popconfirm,
  message,
  AntModal,
  Form,
  AntInput,
  type TableColumnsType,
} from '@radish/ui';
import {
  EditOutlined,
  DeleteOutlined,
  PlusOutlined,
  ReloadOutlined,
  AppstoreOutlined,
  KeyOutlined,
} from '@radish/ui';
import { clientApi } from '../../api/clients';
import type { OidcClient, CreateClientRequest } from '../../types/oidc';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import '../adminFeature.css';
import './Applications.css';

export const Applications = () => {
  const { t } = useTranslation();
  useDocumentTitle(t('applications.documentTitle'));
  const [clients, setClients] = useState<OidcClient[]>([]);
  const [loading, setLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create');
  const [currentClient, setCurrentClient] = useState<OidcClient | null>(null);
  const [form] = Form.useForm();
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);
  const canCreateApplication = usePermission(CONSOLE_PERMISSIONS.applicationsCreate);
  const canEditApplication = usePermission(CONSOLE_PERMISSIONS.applicationsEdit);
  const canDeleteApplication = usePermission(CONSOLE_PERMISSIONS.applicationsDelete);
  const canResetApplicationSecret = usePermission(CONSOLE_PERMISSIONS.applicationsResetSecret);
  const enabledClients = clients.filter((client) => client.status !== 'Disabled').length;
  const thirdPartyClients = clients.filter((client) => client.type === 'ThirdParty').length;

  const loadClients = useCallback(async () => {
    setLoading(true);
    try {
      const result = await clientApi.getClients({ page: 1, pageSize: 100 });
      if (result.ok && result.data) {
        setClients(result.data.data);
      } else {
        message.error(result.message || t('applications.feedback.loadFailed'));
      }
    } catch (error) {
      message.error(t('applications.feedback.loadFailed'));
      log.error(error);
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!canViewApplications) {
      return;
    }

    void loadClients();
  }, [canViewApplications, loadClients]);

  const handleCreate = () => {
    setModalMode('create');
    setCurrentClient(null);
    form.resetFields();
    setIsModalOpen(true);
  };

  const handleEdit = (record: OidcClient) => {
    setModalMode('edit');
    setCurrentClient(record);
    form.setFieldsValue({
      clientId: record.clientId,
      displayName: record.displayName,
      description: record.description,
      developerName: record.developerName,
      developerEmail: record.developerEmail,
      redirectUris: record.redirectUris?.join('\n'),
      postLogoutRedirectUris: record.postLogoutRedirectUris?.join('\n'),
    });
    setIsModalOpen(true);
  };

  const handleDelete = async (id: string) => {
    try {
      const result = await clientApi.deleteClient(id);
      if (result.ok) {
        message.success(t('applications.feedback.deleted'));
        await loadClients();
      } else {
        message.error(result.message || t('applications.feedback.deleteFailed'));
      }
    } catch (error) {
      message.error(t('applications.feedback.deleteFailed'));
      log.error(error);
    }
  };

  const handleResetSecret = async (id: string) => {
    try {
      const result = await clientApi.resetClientSecret(id);
      if (result.ok && result.data) {
        AntModal.info({
          title: t('applications.secret.resetTitle'),
          content: (
            <div>
              <p>{t('applications.secret.newSecret')}</p>
              <p className="applications-secret-box">
                {result.data.clientSecret}
              </p>
            </div>
          ),
          width: 600,
        });
      } else {
        message.error(result.message || t('applications.feedback.resetFailed'));
      }
    } catch (error) {
      message.error(t('applications.feedback.resetFailed'));
      log.error(error);
    }
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();

      if (modalMode === 'create') {
        const data: CreateClientRequest = {
          clientId: values.clientId,
          displayName: values.displayName,
          description: values.description,
          developerName: values.developerName,
          developerEmail: values.developerEmail,
          redirectUris: values.redirectUris?.split('\n').filter((s: string) => s.trim()) || [],
          postLogoutRedirectUris: values.postLogoutRedirectUris?.split('\n').filter((s: string) => s.trim()) || [],
        };
        const result = await clientApi.createClient(data);
        if (result.ok && result.data) {
          message.success(t('applications.feedback.created'));
          AntModal.info({
            title: t('applications.secret.createdTitle'),
            content: (
              <div>
                <p>{t('applications.secret.clientId', { clientId: result.data.clientId })}</p>
                <p>{t('applications.secret.clientSecret')}</p>
                <p className="applications-secret-box">
                  {result.data.clientSecret}
                </p>
              </div>
            ),
            width: 600,
          });
          setIsModalOpen(false);
          await loadClients();
        } else {
          message.error(result.message || t('applications.feedback.createFailed'));
        }
      } else if (modalMode === 'edit' && currentClient) {
        const data = {
          displayName: values.displayName,
          description: values.description,
          developerName: values.developerName,
          developerEmail: values.developerEmail,
          redirectUris: values.redirectUris?.split('\n').filter((s: string) => s.trim()) || [],
          postLogoutRedirectUris: values.postLogoutRedirectUris?.split('\n').filter((s: string) => s.trim()) || [],
        };
        const result = await clientApi.updateClient(currentClient.id, data);
        if (result.ok) {
          message.success(t('applications.feedback.updated'));
          setIsModalOpen(false);
          await loadClients();
        } else {
          message.error(result.message || t('applications.feedback.updateFailed'));
        }
      }
    } catch (error) {
      log.error('表单验证失败:', error);
    }
  };

  const columns: TableColumnsType<OidcClient> = [
    {
      title: t('applications.form.clientId'),
      dataIndex: 'clientId',
      key: 'clientId',
      width: 200,
    },
    {
      title: t('applications.column.displayName'),
      dataIndex: 'displayName',
      key: 'displayName',
      width: 150,
    },
    {
      title: t('applications.column.description'),
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: t('applications.column.type'),
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (type: string) => (
        <Tag color={type === 'Internal' ? 'blue' : 'green'}>
          {type === 'Internal' ? t('applications.type.internal') : t('applications.type.thirdParty')}
        </Tag>
      ),
    },
    {
      title: t('applications.column.status'),
      dataIndex: 'status',
      key: 'status',
      width: 80,
      render: (status: string) => (
        <Tag color={status !== 'Disabled' ? 'success' : 'default'}>
          {status !== 'Disabled' ? t('applications.status.enabled') : t('applications.status.disabled')}
        </Tag>
      ),
    },
    {
      title: t('applications.column.actions'),
      key: 'action',
      width: 220,
      render: (_, record) => (
        <Space size="small" wrap>
          {canEditApplication ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => handleEdit(record)}
            >
              {t('applications.action.edit')}
            </Button>
          ) : null}
          {canResetApplicationSecret ? (
            <Button
              variant="ghost"
              size="small"
              icon={<KeyOutlined />}
              onClick={() => void handleResetSecret(record.id)}
            >
              {t('applications.action.resetSecret')}
            </Button>
          ) : null}
          {canDeleteApplication ? (
            <Popconfirm
              title={t('applications.delete.confirm')}
              onConfirm={() => void handleDelete(record.id)}
              okText={t('applications.delete.ok')}
              cancelText={t('applications.delete.cancel')}
            >
              <Button
                variant="danger"
                size="small"
                icon={<DeleteOutlined />}
              >
                {t('applications.action.delete')}
              </Button>
            </Popconfirm>
          ) : null}
        </Space>
      ),
    },
  ];
  return (
    <div className="admin-feature-page applications-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <AppstoreOutlined /> {t('applications.title')}
            </h2>
            <p className="admin-feature-subtle">{t('applications.description')}</p>
          </div>
          <div className="applications-header-actions">
            <Button icon={<ReloadOutlined />} onClick={() => void loadClients()}>
              {t('applications.action.refresh')}
            </Button>
            {canCreateApplication ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
                {t('applications.action.create')}
              </Button>
            ) : null}
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label={t('applications.metrics.label')}>
        <div className="admin-feature-metric">
          {t('applications.metrics.loaded')}
          <strong>{clients.length}</strong>
        </div>
        <div className="admin-feature-metric">
          {t('applications.metrics.enabled')}
          <strong>{enabledClients}</strong>
        </div>
        <div className="admin-feature-metric">
          {t('applications.metrics.thirdParty')}
          <strong>{thirdPartyClients}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label={t('applications.list.label')}>
            <div className="admin-table-toolbar__title">
              <span>{t('applications.list.title')}</span>
              <Tag>{loading ? t('applications.list.loading') : t('applications.list.recent')}</Tag>
            </div>
            <p className="admin-feature-subtle">{t('applications.list.description')}</p>
          </section>

          <section className="admin-table-panel">
            <Table<OidcClient>
              columns={columns}
              dataSource={clients}
              rowKey="id"
              loading={loading}
              scroll={{ x: 980 }}
              pagination={{
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => t('applications.list.total', { count: total }),
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('applications.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('applications.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('applications.summary.scopeLabel')}</span>
              <span className="admin-table-summary__value">{t('applications.summary.scopeValue')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('applications.summary.createPermission')}</span>
              <span className="admin-table-summary__value">
                {canCreateApplication ? t('applications.summary.createAllowed') : t('applications.summary.createDenied')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('applications.summary.secretPermission')}</span>
              <span className="admin-table-summary__value">
                {canResetApplicationSecret ? t('applications.summary.secretAllowed') : t('applications.summary.secretDenied')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('applications.summary.deletePermission')}</span>
              <span className="admin-table-summary__value">
                {canDeleteApplication ? t('applications.summary.deleteAllowed') : t('applications.summary.deleteDenied')}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <AntModal
        title={modalMode === 'create' ? t('applications.modal.createTitle') : t('applications.modal.editTitle')}
        open={isModalOpen}
        onOk={() => void handleModalOk()}
        onCancel={() => setIsModalOpen(false)}
        width={600}
        forceRender
      >
        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          <Form.Item
            label={t('applications.form.clientId')}
            name="clientId"
            rules={[{ required: true, message: t('applications.form.clientIdRequired') }]}
          >
            <AntInput placeholder={t('applications.form.clientIdPlaceholder')} disabled={modalMode === 'edit'} />
          </Form.Item>

          <Form.Item
            label={t('applications.form.displayName')}
            name="displayName"
            rules={[{ required: true, message: t('applications.form.displayNameRequired') }]}
          >
            <AntInput placeholder={t('applications.form.displayNamePlaceholder')} />
          </Form.Item>

          <Form.Item label={t('applications.form.description')} name="description">
            <AntInput.TextArea rows={3} placeholder={t('applications.form.descriptionPlaceholder')} />
          </Form.Item>

          <Form.Item label={t('applications.form.developerName')} name="developerName">
            <AntInput placeholder={t('applications.form.developerNamePlaceholder')} />
          </Form.Item>

          <Form.Item label={t('applications.form.developerEmail')} name="developerEmail">
            <AntInput type="email" placeholder="developer@example.com" />
          </Form.Item>

          <Form.Item
            label={t('applications.form.redirectUris')}
            name="redirectUris"
            rules={[{ required: true, message: t('applications.form.redirectUrisRequired') }]}
          >
            <AntInput.TextArea
              rows={3}
              placeholder={t('applications.form.redirectUrisPlaceholder')}
            />
          </Form.Item>

          <Form.Item label={t('applications.form.postLogoutRedirectUris')} name="postLogoutRedirectUris">
            <AntInput.TextArea
              rows={2}
              placeholder={t('applications.form.postLogoutRedirectUrisPlaceholder')}
            />
          </Form.Item>
        </Form>
      </AntModal>
    </div>
  );
};
