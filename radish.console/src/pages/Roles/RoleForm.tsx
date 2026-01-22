import { useState, useEffect } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  AntSelect as Select,
  message,
} from '@radish/ui';
import { createRole, updateRole, getRoleById, type Role, type RoleRequest } from '@/api/roleApi';
import { log } from '@/utils/logger';

interface RoleFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  roleId?: number;
  onCancel: () => void;
  onSuccess: () => void;
}

export const RoleForm = ({ visible, mode, roleId, onCancel, onSuccess }: RoleFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(false);

  // 权限范围选项
  const authorityScopeOptions = [
    { label: '无任何权限', value: -1 },
    { label: '自定义权限', value: 1 },
    { label: '本部门', value: 2 },
    { label: '本部门及以下', value: 3 },
    { label: '仅自己', value: 4 },
    { label: '全部', value: 9 },
  ];

  // 加载角色详情（编辑模式）
  const loadRoleDetail = async (id: number) => {
    try {
      setInitialLoading(true);
      const role = await getRoleById(id);
      form.setFieldsValue({
        roleName: role.roleName,
        roleDescription: role.roleDescription,
        orderSort: role.orderSort,
        departmentIds: role.departmentIds,
        authorityScope: role.authorityScope,
        isEnabled: role.isEnabled,
      });
    } catch (error) {
      log.error('RoleForm', '加载角色详情失败:', error);
      message.error('加载角色详情失败');
    } finally {
      setInitialLoading(false);
    }
  };

  // 提交表单
  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const roleData: RoleRequest = {
        roleName: values.roleName,
        roleDescription: values.roleDescription,
        orderSort: values.orderSort,
        departmentIds: values.departmentIds,
        authorityScope: values.authorityScope,
        isEnabled: values.isEnabled,
      };

      if (mode === 'create') {
        await createRole(roleData);
        message.success('创建角色成功');
      } else if (mode === 'edit' && roleId) {
        await updateRole(roleId, roleData);
        message.success('更新角色成功');
      }

      onSuccess();
    } catch (error) {
      log.error('RoleForm', '提交表单失败:', error);
      message.error(mode === 'create' ? '创建角色失败' : '更新角色失败');
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
          orderSort: 0,
          authorityScope: -1,
          isEnabled: true,
        });
      }
    } else {
      form.resetFields();
    }
  }, [visible, mode, roleId, form]);

  return (
    <Modal
      title={mode === 'create' ? '新增角色' : '编辑角色'}
      open={visible}
      onOk={handleSubmit}
      onCancel={handleCancel}
      confirmLoading={loading}
      width={600}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={initialLoading}
      >
        <Form.Item
          name="roleName"
          label="角色名称"
          rules={[
            { required: true, message: '请输入角色名称' },
            { max: 50, message: '角色名称不能超过50个字符' },
          ]}
        >
          <Input placeholder="请输入角色名称" />
        </Form.Item>

        <Form.Item
          name="roleDescription"
          label="角色描述"
          rules={[
            { max: 500, message: '角色描述不能超过500个字符' },
          ]}
        >
          <Input.TextArea
            placeholder="请输入角色描述"
            rows={3}
            showCount
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          name="orderSort"
          label="排序"
          rules={[
            { required: true, message: '请输入排序值' },
            { type: 'number', min: 0, message: '排序值不能小于0' },
          ]}
        >
          <InputNumber
            placeholder="请输入排序值"
            min={0}
            style={{ width: '100%' }}
          />
        </Form.Item>

        <Form.Item
          name="authorityScope"
          label="权限范围"
          rules={[
            { required: true, message: '请选择权限范围' },
          ]}
        >
          <Select
            placeholder="请选择权限范围"
            options={authorityScopeOptions}
          />
        </Form.Item>

        <Form.Item
          name="departmentIds"
          label="部门ID"
          tooltip="自定义权限时使用，多个部门ID用逗号分隔"
        >
          <Input placeholder="请输入部门ID，多个用逗号分隔" />
        </Form.Item>

        <Form.Item
          name="isEnabled"
          label="启用状态"
          valuePropName="checked"
        >
          <Switch checkedChildren="启用" unCheckedChildren="禁用" />
        </Form.Item>
      </Form>
    </Modal>
  );
};