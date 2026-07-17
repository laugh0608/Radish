import { AntInput as Input, Button, DatePicker, Form, InputNumber } from '@radish/ui';
import type { FormInstance } from 'antd';
import type { RefObject } from 'react';
import type { UserExperienceVo } from '@/api/experienceAdminApi';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();

  return (
    <>
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>{t('experience.adjust.title')}</h3>
            <p className="admin-feature-subtle">{t('experience.adjust.description')}</p>
          </div>
        </div>

        <Form form={adjustForm} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label={t('experience.form.userId')}
            rules={[
              { required: true, message: t('experience.form.userIdRequired') },
              { pattern: /^[1-9]\d*$/, message: t('experience.form.userIdInvalid') },
            ]}
          >
            <Input className="experience-filter-control" />
          </Form.Item>

          <Form.Item
            name="deltaExp"
            label={t('experience.form.delta')}
            rules={[{ required: true, message: t('experience.form.deltaRequired') }]}
          >
            <InputNumber className="experience-filter-control" />
          </Form.Item>

          <Form.Item name="reason" label={t('experience.form.adjustReason')}>
            <Input.TextArea rows={4} maxLength={500} showCount placeholder={t('experience.form.adjustReasonPlaceholder')} />
          </Form.Item>

          <div>
            <Button
              variant="primary"
              disabled={!canAdjust || submitting}
              onClick={() => {
                void onAdjust();
              }}
            >
              {submitting ? t('experience.actions.submitting') : t('experience.actions.submitAdjust')}
            </Button>
          </div>
        </Form>
      </section>

      <section className="admin-feature-card" ref={freezeSectionRef}>
        <div className="admin-feature-header">
          <div>
            <h3>{t('experience.freeze.title')}</h3>
            <p className="admin-feature-subtle">{t('experience.freeze.description')}</p>
          </div>
        </div>

        <Form form={freezeForm} layout="vertical" className="admin-feature-form">
          <Form.Item
            name="userId"
            label={t('experience.form.userId')}
            rules={[
              { required: true, message: t('experience.form.userIdRequired') },
              { pattern: /^[1-9]\d*$/, message: t('experience.form.userIdInvalid') },
            ]}
          >
            <Input className="experience-filter-control" />
          </Form.Item>

          <Form.Item name="frozenUntil" label={t('experience.form.frozenUntil')}>
            <DatePicker
              showTime
              allowClear
              className="experience-filter-control"
              placeholder={t('experience.form.frozenUntilPlaceholder')}
            />
          </Form.Item>

          <Form.Item
            name="reason"
            label={t('experience.form.freezeReason')}
            rules={[{ required: true, message: t('experience.form.freezeReasonRequired') }]}
          >
            <Input.TextArea rows={4} maxLength={500} showCount placeholder={t('experience.form.freezeReasonPlaceholder')} />
          </Form.Item>

          <div className="experience-inline-actions">
            <Button
              variant="primary"
              disabled={!canFreeze || freezing}
              onClick={() => {
                void onFreeze();
              }}
            >
              {freezing ? t('experience.actions.freezing') : t('experience.actions.submitFreeze')}
            </Button>
            <Button
              disabled={!canFreeze || unfreezing || !experience?.voExpFrozen}
              onClick={() => {
                void onUnfreeze();
              }}
            >
              {unfreezing ? t('experience.actions.unfreezing') : t('experience.actions.unfreeze')}
            </Button>
          </div>
        </Form>
      </section>
    </>
  );
};
