import { AntInput as Input, Button, Tag } from '@radish/ui';
import type { UserExperienceVo } from '@/api/experienceAdminApi';

type ExperienceUserQuerySummaryProps = {
  queryUserId: string;
  loadedUserId: string | null;
  experience: UserExperienceVo | null;
  loadingExperience: boolean;
  onQueryUserIdChange: (value: string) => void;
  onQuery: () => void;
};

export const ExperienceUserQuerySummary = ({
  queryUserId,
  loadedUserId,
  experience,
  loadingExperience,
  onQueryUserIdChange,
  onQuery,
}: ExperienceUserQuerySummaryProps) => {
  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h3>用户经验查询</h3>
          <p className="admin-feature-subtle">输入用户 ID 查看当前等级、总经验、下一级与冻结状态。</p>
        </div>
        <div className="experience-inline-actions">
          <Input
            placeholder="用户 ID"
            value={queryUserId}
            onChange={(event) => onQueryUserIdChange(event.target.value)}
            onPressEnter={onQuery}
            className="experience-query-control"
          />
          <Button variant="primary" onClick={onQuery} disabled={loadingExperience}>
            {loadingExperience ? '查询中...' : '查询'}
          </Button>
        </div>
      </div>

      <div className="admin-feature-metrics experience-section-gap-md">
        <div className="experience-query-current">
          {loadedUserId && experience
            ? `当前展示：${experience.voUserName || '未命名用户'}（ID: ${loadedUserId}）`
            : '当前未加载用户经验数据'}
        </div>
        <div className="admin-feature-metric">
          <span>当前等级</span>
          <strong>{experience ? `${experience.voCurrentLevelName} (L${experience.voCurrentLevel})` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>总经验</span>
          <strong>{experience ? experience.voTotalExp : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>距下一级</span>
          <strong>{experience ? experience.voExpToNextLevel : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>状态</span>
          <strong>{experience ? (experience.voExpFrozen ? '经验冻结' : '正常') : '--'}</strong>
        </div>
      </div>
      {experience?.voExpFrozen && (
        <div className="experience-inline-actions experience-inline-actions--center experience-section-gap-sm">
          <Tag color="warning">冻结中</Tag>
          <span>到期时间：{experience.voFrozenUntil || '永久冻结'}</span>
          <span>原因：{experience.voFrozenReason || '未填写'}</span>
        </div>
      )}
    </section>
  );
};
