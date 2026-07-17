import { AntInput as Input, AntSelect as Select, Button, Form, Table } from '@radish/ui';
import type { FormInstance } from 'antd';
import type { RefObject } from 'react';
import {
  type UserExperienceGovernanceActionVo,
  type UserExperienceVo,
} from '@/api/experienceAdminApi';
import { createGovernanceActionColumns } from './experienceAdminColumns';
import {
  type GovernanceReviewDraftContext,
  type GovernanceReviewFormValues,
} from './experienceAdminHelpers';
import { useTranslation } from 'react-i18next';

type ExperienceGovernanceReviewSectionProps = {
  reviewSectionRef: RefObject<HTMLElement | null>;
  loadedUserId: string | null;
  experience: UserExperienceVo | null;
  reviewForm: FormInstance<GovernanceReviewFormValues>;
  reviewContextDraft: GovernanceReviewDraftContext | null;
  canFreeze: boolean;
  reviewing: boolean;
  governanceActions: UserExperienceGovernanceActionVo[];
  loadingGovernanceActions: boolean;
  onRecordGovernanceReview: () => void;
  onClearReviewDraft: () => void;
};

export const ExperienceGovernanceReviewSection = ({
  reviewSectionRef,
  loadedUserId,
  experience,
  reviewForm,
  reviewContextDraft,
  canFreeze,
  reviewing,
  governanceActions,
  loadingGovernanceActions,
  onRecordGovernanceReview,
  onClearReviewDraft,
}: ExperienceGovernanceReviewSectionProps) => {
  const { t, i18n } = useTranslation();
  const governanceActionColumns = createGovernanceActionColumns(t, i18n.resolvedLanguage);

  return (
    <section className="admin-feature-card" ref={reviewSectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>{t('experience.review.title')}</h3>
          <p className="admin-feature-subtle">{t('experience.review.description')}</p>
        </div>
      </div>

      {loadedUserId ? (
        <>
          <div className="experience-review-target experience-section-gap-sm">
            {t('experience.review.target', { name: experience?.voUserName || t('experience.common.unnamedUser'), userId: loadedUserId })}
          </div>

          <div className="admin-feature-banner experience-section-gap-sm">
            {reviewContextDraft ? (
              <>
                <div>{reviewContextDraft.hint}</div>
                {reviewContextDraft.recommendationReason && (
                  <div className="experience-review-draft-reason">
                    {t('experience.review.reasonSnapshot', { value: reviewContextDraft.recommendationReason })}
                  </div>
                )}
              </>
            ) : t('experience.review.draftHint')}
          </div>

          <Form form={reviewForm} layout="vertical" className="admin-feature-form experience-form-spaced">
            <Form.Item
              name="reviewResult"
              label={t('experience.review.result')}
              rules={[{ required: true, message: t('experience.review.resultRequired') }]}
            >
              <Select
                placeholder={t('experience.review.resultPlaceholder')}
                options={[
                  { label: t('experience.review.noIssue'), value: 'NoIssue' },
                  { label: t('experience.review.observe'), value: 'Observe' },
                  { label: t('experience.review.freezeSuggest'), value: 'FreezeSuggest' },
                ]}
                disabled={!canFreeze || reviewing}
              />
            </Form.Item>

            <Form.Item
              name="remark"
              label={t('experience.review.remark')}
              rules={[
                { required: true, message: t('experience.review.remarkRequired') },
                { max: 500, message: t('experience.review.remarkMax') },
              ]}
            >
              <Input.TextArea
                rows={4}
                maxLength={500}
                showCount
                placeholder={t('experience.review.remarkPlaceholder')}
                disabled={!canFreeze || reviewing}
              />
            </Form.Item>

            <div className="experience-inline-actions">
              <Button
                variant="primary"
                disabled={!canFreeze || reviewing}
                onClick={() => {
                  void onRecordGovernanceReview();
                }}
              >
                {reviewing ? t('experience.actions.recording') : t('experience.actions.recordReview')}
              </Button>
              <Button disabled={reviewing} onClick={onClearReviewDraft}>
                {t('experience.actions.clearReview')}
              </Button>
            </div>
          </Form>

          <div className="experience-section-title experience-section-gap-lg">{t('experience.review.recentActions')}</div>
          <Table<UserExperienceGovernanceActionVo>
            rowKey="voActionId"
            columns={governanceActionColumns}
            dataSource={governanceActions}
            loading={loadingGovernanceActions}
            pagination={false}
            scroll={{ x: 1280 }}
            className="experience-section-gap-sm"
            locale={{
              emptyText: loadingGovernanceActions ? t('experience.review.loading') : t('experience.review.empty'),
            }}
          />
        </>
      ) : (
        <div className="experience-empty-hint">
          {t('experience.review.queryFirst')}
        </div>
      )}
    </section>
  );
};
