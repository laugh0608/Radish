import { Button, Table } from '@radish/ui';
import type { LevelConfigVo } from '@/api/experienceAdminApi';
import { createLevelColumns } from './experienceAdminColumns';

type ExperienceLevelConfigSectionProps = {
  levels: LevelConfigVo[];
  loadingLevels: boolean;
  canRecalculate: boolean;
  recalculating: boolean;
  onRecalculate: () => void;
};

export const ExperienceLevelConfigSection = ({
  levels,
  loadingLevels,
  canRecalculate,
  recalculating,
  onRecalculate,
}: ExperienceLevelConfigSectionProps) => {
  const levelColumns = createLevelColumns();

  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h3>等级配置</h3>
          <p className="admin-feature-subtle">查看当前等级曲线，必要时重新按后端配置重算。</p>
        </div>
        <Button
          variant="primary"
          disabled={!canRecalculate || recalculating}
          onClick={() => {
            void onRecalculate();
          }}
        >
          {recalculating ? '重算中...' : '重算等级配置'}
        </Button>
      </div>

      <Table<LevelConfigVo>
        rowKey="voLevel"
        columns={levelColumns}
        dataSource={levels}
        loading={loadingLevels}
        pagination={false}
        scroll={{ x: 960 }}
      />
    </section>
  );
};
