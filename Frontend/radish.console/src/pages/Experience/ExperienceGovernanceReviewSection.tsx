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
  const governanceActionColumns = createGovernanceActionColumns();

  return (
    <section className="admin-feature-card" ref={reviewSectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>复核结论与留痕</h3>
          <p className="admin-feature-subtle">记录人工复核结论；冻结 / 解冻动作会自动写入治理留痕，便于后续回看。</p>
        </div>
      </div>

      {loadedUserId ? (
        <>
          <div className="experience-review-target experience-section-gap-sm">
            当前目标：{experience?.voUserName || '未命名用户'}（ID: {loadedUserId}）
          </div>

          <div className="admin-feature-banner experience-section-gap-sm">
            {reviewContextDraft ? (
              <>
                <div>{reviewContextDraft.hint}</div>
                {reviewContextDraft.recommendationReason && (
                  <div className="experience-review-draft-reason">
                    建议原因快照：{reviewContextDraft.recommendationReason}
                  </div>
                )}
              </>
            ) : '可从上方治理建议、规则摘要或每日异常一键带入复核草稿，也可直接手动填写结论。'}
          </div>

          <Form form={reviewForm} layout="vertical" className="admin-feature-form experience-form-spaced">
            <Form.Item
              name="reviewResult"
              label="复核结论"
              rules={[{ required: true, message: '请选择复核结论' }]}
            >
              <Select
                placeholder="选择复核结论"
                options={[
                  { label: '已复核，未见异常', value: 'NoIssue' },
                  { label: '已复核，继续观察', value: 'Observe' },
                  { label: '已复核，可考虑冻结', value: 'FreezeSuggest' },
                ]}
                disabled={!canFreeze || reviewing}
              />
            </Form.Item>

            <Form.Item
              name="remark"
              label="复核备注"
              rules={[
                { required: true, message: '请输入复核备注' },
                { max: 500, message: '复核备注不能超过500个字符' },
              ]}
            >
              <Input.TextArea
                rows={4}
                maxLength={500}
                showCount
                placeholder="例如：已回看对应日期经验流水与目标内容，暂未发现异常；继续观察。"
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
                {reviewing ? '记录中...' : '记录复核结论'}
              </Button>
              <Button disabled={reviewing} onClick={onClearReviewDraft}>
                清空复核草稿
              </Button>
            </div>
          </Form>

          <div className="experience-section-title experience-section-gap-lg">最近治理留痕</div>
          <Table<UserExperienceGovernanceActionVo>
            rowKey="voActionId"
            columns={governanceActionColumns}
            dataSource={governanceActions}
            loading={loadingGovernanceActions}
            pagination={false}
            scroll={{ x: 1280 }}
            className="experience-section-gap-sm"
            locale={{
              emptyText: loadingGovernanceActions ? '治理留痕加载中...' : '该用户暂无治理留痕',
            }}
          />
        </>
      ) : (
        <div className="experience-empty-hint">
          请先查询用户经验，再记录复核结论或查看治理留痕。
        </div>
      )}
    </section>
  );
};
