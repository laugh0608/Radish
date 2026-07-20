import { Button, Table } from '@radish/ui';
import type { LevelConfigVo } from '@/api/experienceAdminApi';
import { createLevelColumns } from './experienceAdminColumns';
import { useTranslation } from 'react-i18next';

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
  const { t, i18n } = useTranslation();
  const levelColumns = createLevelColumns(t, i18n.resolvedLanguage);

  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h3>{t('experience.levels.title')}</h3>
          <p className="admin-feature-subtle">{t('experience.levels.description')}</p>
        </div>
        <Button
          variant="primary"
          disabled={!canRecalculate || recalculating}
          onClick={() => {
            void onRecalculate();
          }}
        >
          {recalculating ? t('experience.actions.recalculating') : t('experience.actions.recalculate')}
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
