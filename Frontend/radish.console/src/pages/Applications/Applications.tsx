import { useEffect, useState } from 'react';
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
  useDocumentTitle('应用管理');
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

  useEffect(() => {
    if (!canViewApplications) {
      return;
    }

    void loadClients();
  }, [canViewApplications]);

  const loadClients = async () => {
    setLoading(true);
    try {
      const result = await clientApi.getClients({ page: 1, pageSize: 100 });
      if (result.ok && result.data) {
        setClients(result.data.data);
      } else {
        message.error(result.message || '加载失败');
      }
    } catch (error) {
      message.error('加载失败');
      log.error(error);
    } finally {
      setLoading(false);
    }
  };

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
        message.success('删除成功');
        await loadClients();
      } else {
        message.error(result.message || '删除失败');
      }
    } catch (error) {
      message.error('删除失败');
      log.error(error);
    }
  };

  const handleResetSecret = async (id: string) => {
    try {
      const result = await clientApi.resetClientSecret(id);
      if (result.ok && result.data) {
        AntModal.info({
          title: '客户端密钥已重置',
          content: (
            <div>
              <p>新的客户端密钥（请妥善保存，仅显示一次）：</p>
              <p className="applications-secret-box">
                {result.data.clientSecret}
              </p>
            </div>
          ),
          width: 600,
        });
      } else {
        message.error(result.message || '重置失败');
      }
    } catch (error) {
      message.error('重置失败');
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
          message.success('创建成功');
          AntModal.info({
            title: '客户端创建成功',
            content: (
              <div>
                <p>Client ID: {result.data.clientId}</p>
                <p>Client Secret（请妥善保存，仅显示一次）：</p>
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
          message.error(result.message || '创建失败');
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
          message.success('更新成功');
          setIsModalOpen(false);
          await loadClients();
        } else {
          message.error(result.message || '更新失败');
        }
      }
    } catch (error) {
      log.error('表单验证失败:', error);
    }
  };

  const columns: TableColumnsType<OidcClient> = [
    {
      title: 'Client ID',
      dataIndex: 'clientId',
      key: 'clientId',
      width: 200,
    },
    {
      title: '显示名称',
      dataIndex: 'displayName',
      key: 'displayName',
      width: 150,
    },
    {
      title: '描述',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: '类型',
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (type: string) => (
        <Tag color={type === 'Internal' ? 'blue' : 'green'}>
          {type === 'Internal' ? '官方' : '第三方'}
        </Tag>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 80,
      render: (status: string) => (
        <Tag color={status !== 'Disabled' ? 'success' : 'default'}>
          {status !== 'Disabled' ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '操作',
      key: 'action',
      width: 220,
      render: (_, record) => (
        <Space size="small">
          {canEditApplication ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => handleEdit(record)}
            >
              编辑
            </Button>
          ) : null}
          {canResetApplicationSecret ? (
            <Button
              variant="ghost"
              size="small"
              icon={<KeyOutlined />}
              onClick={() => void handleResetSecret(record.id)}
            >
              重置密钥
            </Button>
          ) : null}
          {canDeleteApplication ? (
            <Popconfirm
              title="确定删除此客户端吗？"
              onConfirm={() => void handleDelete(record.id)}
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
              <AppstoreOutlined /> 应用管理
            </h2>
            <p className="admin-feature-subtle">维护 OIDC 客户端、回调地址和客户端密钥重置入口。</p>
          </div>
          <div className="applications-header-actions">
            <Button icon={<ReloadOutlined />} onClick={() => void loadClients()}>
              刷新
            </Button>
            {canCreateApplication ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
                新增应用
              </Button>
            ) : null}
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="应用列表指标">
        <div className="admin-feature-metric">
          已加载应用
          <strong>{clients.length}</strong>
        </div>
        <div className="admin-feature-metric">
          启用应用
          <strong>{enabledClients}</strong>
        </div>
        <div className="admin-feature-metric">
          第三方应用
          <strong>{thirdPartyClients}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label="应用列表状态">
            <div className="admin-table-toolbar__title">
              <span>客户端列表</span>
              <Tag>{loading ? '加载中' : '最近 100 条'}</Tag>
            </div>
            <p className="admin-feature-subtle">当前列表沿用既有客户端接口，创建和编辑表单字段保持不变。</p>
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
                showTotal: (total) => `共 ${total} 条`,
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>应用摘要</h3>
          <p className="admin-feature-subtle">用于核对客户端状态、类型和敏感操作权限。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">客户端接口前 100 条</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">创建权限</span>
              <span className="admin-table-summary__value">
                {canCreateApplication ? '可新增应用' : '不可新增应用'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">密钥操作</span>
              <span className="admin-table-summary__value">
                {canResetApplicationSecret ? '可重置客户端密钥' : '无密钥重置权限'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">删除权限</span>
              <span className="admin-table-summary__value">
                {canDeleteApplication ? '可删除应用' : '不可删除应用'}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <AntModal
        title={modalMode === 'create' ? '新增应用' : '编辑应用'}
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
            label="Client ID"
            name="clientId"
            rules={[{ required: true, message: '请输入 Client ID' }]}
          >
            <AntInput placeholder="例如: my-app" disabled={modalMode === 'edit'} />
          </Form.Item>

          <Form.Item
            label="显示名称"
            name="displayName"
            rules={[{ required: true, message: '请输入显示名称' }]}
          >
            <AntInput placeholder="例如: My Application" />
          </Form.Item>

          <Form.Item label="描述" name="description">
            <AntInput.TextArea rows={3} placeholder="应用描述" />
          </Form.Item>

          <Form.Item label="开发者名称" name="developerName">
            <AntInput placeholder="开发者或组织名称" />
          </Form.Item>

          <Form.Item label="开发者邮箱" name="developerEmail">
            <AntInput type="email" placeholder="developer@example.com" />
          </Form.Item>

          <Form.Item
            label="回调 URI"
            name="redirectUris"
            rules={[{ required: true, message: '请输入至少一个回调 URI' }]}
          >
            <AntInput.TextArea
              rows={3}
              placeholder="每行一个 URI，例如：&#10;https://localhost:3000/callback"
            />
          </Form.Item>

          <Form.Item label="登出回调 URI" name="postLogoutRedirectUris">
            <AntInput.TextArea
              rows={2}
              placeholder="每行一个 URI，例如：&#10;https://localhost:3000"
            />
          </Form.Item>
        </Form>
      </AntModal>
    </div>
  );
};
