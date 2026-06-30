import { AntInput as Input, Button, DatePicker, Form, InputNumber } from '@radish/ui';
import type { FormInstance } from 'antd';
import type { RefObject } from 'react';
import type { UserExperienceVo } from '@/api/experienceAdminApi';
import type { AdjustFormValues, FreezeFormValues } from './experienceAdminHelpers';

type ExperienceGovernanceActionFormsProps = {
  adjustForm: FormInstance<AdjustFormValues>;
  freezeForm: FormInstance<FreezeFormValues>;
  freezeSectionRef: RefObject<HTMLElement | null>;
  experience: UserExperienceVo | null;
  canAdjust: boolean;
  canFreeze: boolean;
  submitting: boolean;
  freezing: boolean;
  unfreezing: boolean;
  onAdjust: () => void;
  onFreeze: () => void;
  onUnfreeze: () => void;
};

export const ExperienceGovernanceActionForms = ({
  adjustForm,
  freezeForm,
  freezeSectionRef,
  experience,
  canAdjust,
  canFreeze,
  submitting,
  freezing,
  unfreezing,
  onAdjust,
  onFreeze,
  onUnfreeze,
}: ExperienceGovernanceActionFormsProps) => {
  return (
    <>
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>管理员调经验</h3>
            <p className="admin-feature-subtle">正数表示补发经验，负数表示扣减经验。</p>
          </div>
        </div>

        <Form form={adjustForm} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label="用户 ID"
            rules={[
              { required: true, message: '请输入用户 ID' },
              { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' },
            ]}
          >
            <Input className="experience-filter-control" />
          </Form.Item>

          <Form.Item
            name="deltaExp"
            label="经验变动量"
            rules={[{ required: true, message: '请输入经验变动量' }]}
          >
            <InputNumber className="experience-filter-control" />
          </Form.Item>

          <Form.Item name="reason" label="调整原因">
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="例如：补偿、回收、活动奖励" />
          </Form.Item>

          <div>
            <Button
              variant="primary"
              disabled={!canAdjust || submitting}
              onClick={() => {
                void onAdjust();
              }}
            >
              {submitting ? '提交中...' : '提交调整'}
            </Button>
          </div>
        </Form>
      </section>

      <section className="admin-feature-card" ref={freezeSectionRef}>
        <div className="admin-feature-header">
          <div>
            <h3>冻结 / 解冻经验</h3>
            <p className="admin-feature-subtle">可设置临时冻结或永久冻结；冻结中的用户不会继续累计经验，也不会参与经验排行榜。</p>
          </div>
        </div>

        <Form form={freezeForm} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label="用户 ID"
            rules={[
              { required: true, message: '请输入用户 ID' },
              { pattern: /^[1-9]\d*$/, message: '请输入有效的用户 ID' },
            ]}
          >
            <Input className="experience-filter-control" />
          </Form.Item>

          <Form.Item name="frozenUntil" label="冻结到期时间">
            <DatePicker
              showTime
              allowClear
              className="experience-filter-control"
              placeholder="留空表示永久冻结"
            />
          </Form.Item>

          <Form.Item
            name="reason"
            label="冻结原因"
            rules={[{ required: true, message: '请输入冻结原因' }]}
          >
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="例如：异常刷经验、待人工复核" />
          </Form.Item>

          <div className="experience-inline-actions">
            <Button
              variant="primary"
              disabled={!canFreeze || freezing}
              onClick={() => {
                void onFreeze();
              }}
            >
              {freezing ? '冻结中...' : '提交冻结'}
            </Button>
            <Button
              disabled={!canFreeze || unfreezing || !experience?.voExpFrozen}
              onClick={() => {
                void onUnfreeze();
              }}
            >
              {unfreezing ? '解冻中...' : '解除冻结'}
            </Button>
          </div>
        </Form>
      </section>
    </>
  );
};
