import { useCallback, useState, useEffect } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  AntSelect as Select,
  message,
} from '@radish/ui';
import { createRole, updateRole, getRoleById, type CreateRoleRequest } from '@/api/roleApi';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';

interface RoleFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  roleId?: string;
  onCancel: () => void;
  onSuccess: () => void;
}

export const RoleForm = ({ visible, mode, roleId, onCancel, onSuccess }: RoleFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(false);

  // 权限范围选项
  const authorityScopeOptions = [
    { label: t('roles.scope.none'), value: -1 },
    { label: t('roles.scope.custom'), value: 1 },
    { label: t('roles.scope.department'), value: 2 },
    { label: t('roles.scope.departmentAndChildren'), value: 3 },
    { label: t('roles.scope.self'), value: 4 },
    { label: t('roles.scope.all'), value: 9 },
  ];

  // 加载角色详情（编辑模式）
  const loadRoleDetail = useCallback(async (id: string) => {
    try {
      setInitialLoading(true);
      const role = await getRoleById(id);
      // 直接使用 vo 前缀字段
      form.setFieldsValue({
        voRoleName: role.voRoleName,
        voRoleDescription: role.voRoleDescription,
        voOrderSort: role.voOrderSort,
        voDepartmentIds: role.voDepartmentIds,
        voAuthorityScope: role.voAuthorityScope,
        voIsEnabled: role.voIsEnabled,
      });
    } catch (error) {
      log.error('RoleForm', '加载角色详情失败:', error);
      message.error(t('roles.feedback.detailLoadFailed'));
    } finally {
      setInitialLoading(false);
    }
  }, [form, t]);

  // 提交表单
  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // 直接使用 vo 前缀字段构建请求
      const roleData: CreateRoleRequest = {
        voRoleName: values.voRoleName,
        voRoleDescription: values.voRoleDescription,
        voOrderSort: values.voOrderSort,
        voDepartmentIds: values.voDepartmentIds,
        voAuthorityScope: values.voAuthorityScope,
        voIsEnabled: values.voIsEnabled,
      };

      if (mode === 'create') {
        await createRole(roleData);
        message.success(t('roles.feedback.created'));
      } else if (mode === 'edit' && roleId) {
        await updateRole(roleId, roleData);
        message.success(t('roles.feedback.updated'));
      }

      onSuccess();
    } catch (error) {
      log.error('RoleForm', '提交表单失败:', error);
      message.error(t(mode === 'create' ? 'roles.feedback.createFailed' : 'roles.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  // 重置表单
  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  // 监听 visible 和 roleId 变化
  useEffect(() => {
    if (visible) {
      if (mode === 'edit' && roleId) {
        loadRoleDetail(roleId);
      } else {
        // 创建模式，设置默认值
        form.setFieldsValue({
          voOrderSort: 0,
          voAuthorityScope: -1,
          voIsEnabled: true,
        });
      }
    } else {
      form.resetFields();
    }
  }, [visible, mode, roleId, form, loadRoleDetail]);

  return (
    <Modal
      title={t(mode === 'create' ? 'roles.form.createTitle' : 'roles.form.editTitle')}
      open={visible}
      onOk={handleSubmit}
      onCancel={handleCancel}
      confirmLoading={loading}
      width={600}
      destroyOnHidden
      forceRender
    >
      <Form
        form={form}
        layout="vertical"
        disabled={initialLoading}
      >
        <Form.Item
          name="voRoleName"
          label={t('roles.form.name')}
          rules={[
            { required: true, message: t('roles.form.nameRequired') },
            { max: 50, message: t('roles.form.nameMax') },
          ]}
        >
          <Input placeholder={t('roles.form.namePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="voRoleDescription"
          label={t('roles.form.description')}
          rules={[
            { max: 500, message: t('roles.form.descriptionMax') },
          ]}
        >
          <Input.TextArea
            placeholder={t('roles.form.descriptionPlaceholder')}
            rows={3}
            showCount
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          name="voOrderSort"
          label={t('roles.form.sort')}
          rules={[
            { required: true, message: t('roles.form.sortRequired') },
            { type: 'number', min: 0, message: t('roles.form.sortMin') },
          ]}
        >
          <InputNumber
            placeholder={t('roles.form.sortPlaceholder')}
            min={0}
            className="role-form-full-width"
          />
        </Form.Item>

        <Form.Item
          name="voAuthorityScope"
          label={t('roles.form.scope')}
          rules={[
            { required: true, message: t('roles.form.scopeRequired') },
          ]}
        >
          <Select
            placeholder={t('roles.form.scopePlaceholder')}
            options={authorityScopeOptions}
          />
        </Form.Item>

        <Form.Item
          name="voDepartmentIds"
          label={t('roles.form.departmentIds')}
          tooltip={t('roles.form.departmentIdsTooltip')}
        >
          <Input placeholder={t('roles.form.departmentIdsPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="voIsEnabled"
          label={t('roles.form.enabled')}
          valuePropName="checked"
        >
          <Switch checkedChildren={t('roles.status.enabled')} unCheckedChildren={t('roles.status.disabled')} />
        </Form.Item>
      </Form>
    </Modal>
  );
};
