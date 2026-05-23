import { Button, ReloadOutlined } from '@radish/ui';

type ExperienceAdminHeaderProps = {
  onRefresh: () => void;
};

export const ExperienceAdminHeader = ({ onRefresh }: ExperienceAdminHeaderProps) => {
  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h2>经验等级</h2>
          <p className="admin-feature-subtle">支持按用户查看经验等级、调经验，并回看当前等级配置。</p>
        </div>
        <Button icon={<ReloadOutlined />} onClick={onRefresh}>
          刷新
        </Button>
      </div>
    </section>
  );
};
