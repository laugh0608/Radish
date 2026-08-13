import { useState } from 'react';
import { AntInput as Input, Button, Table } from '@radish/ui';
import type {
  ExperienceLevelRecalculationAuditVo,
  ExperienceLevelRecalculationPreviewVo,
  LevelConfigVo,
} from '@/api/experienceAdminApi';
import { createLevelColumns } from './experienceAdminColumns';
import { useTranslation } from 'react-i18next';
import { formatConsoleDateTime, formatConsoleInteger } from '@/utils/localeFormatters';

type ExperienceLevelConfigSectionProps = {
  levels: LevelConfigVo[];
  loadingLevels: boolean;
  canRecalculate: boolean;
  recalculating: boolean;
  previewing: boolean;
  preview: ExperienceLevelRecalculationPreviewVo | null;
  audits: ExperienceLevelRecalculationAuditVo[];
  onPreview: () => void;
  onRecalculate: (reason: string) => void;
};

export const ExperienceLevelConfigSection = ({
  levels,
  loadingLevels,
  canRecalculate,
  recalculating,
  previewing,
  preview,
  audits,
  onPreview,
  onRecalculate,
}: ExperienceLevelConfigSectionProps) => {
  const { t, i18n } = useTranslation();
  const [reason, setReason] = useState('');
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
          disabled={!canRecalculate || previewing || recalculating}
          onClick={() => void onPreview()}
        >
          {previewing ? t('experience.actions.previewing') : t('experience.actions.previewRecalculate')}
        </Button>
      </div>

      {preview && (
        <div className="experience-recalculation-preview experience-section-gap-sm">
          <div>
            <strong>{t('experience.levels.previewTitle')}</strong>
            <p>{preview.voFormulaSummary}</p>
            <p>{t('experience.levels.previewChanged', { count: preview.voChangedLevelCount })}</p>
          </div>
          <div className="experience-recalculation-changes">
            {preview.voChanges.filter((item) => item.voChanged).map((item) => (
              <div key={item.voLevel} className="experience-mobile-card">
                <strong>Lv.{item.voLevel} · {item.voLevelName}</strong>
                <span>{formatConsoleInteger(item.voBeforeExpCumulative, i18n.resolvedLanguage)} → {formatConsoleInteger(item.voAfterExpCumulative, i18n.resolvedLanguage)}</span>
              </div>
            ))}
          </div>
          <Input.TextArea
            rows={3}
            maxLength={500}
            showCount
            value={reason}
            placeholder={t('experience.levels.reasonPlaceholder')}
            onChange={(event) => setReason(event.target.value)}
          />
          <Button
            variant="primary"
            disabled={!canRecalculate || recalculating || preview.voChangedLevelCount <= 0 || reason.trim().length === 0 || preview.voMissingLevels.length > 0}
            onClick={() => void onRecalculate(reason.trim())}
          >
            {recalculating ? t('experience.actions.recalculating') : t('experience.actions.confirmRecalculate')}
          </Button>
        </div>
      )}

      <Table<LevelConfigVo>
        rowKey="voLevel"
        columns={levelColumns}
        dataSource={levels}
        loading={loadingLevels}
        pagination={false}
        scroll={{ x: 960 }}
        className="experience-responsive-table experience-section-gap-sm"
      />
      <div className="experience-mobile-list">
        {levels.map((level) => (
          <article key={level.voLevel} className="experience-mobile-card">
            <strong>Lv.{level.voLevel} · {level.voLevelName}</strong>
            <span>{t('experience.levels.requiredValue', { value: formatConsoleInteger(level.voExpRequired, i18n.resolvedLanguage) })}</span>
            <span>{t('experience.levels.cumulativeValue', { value: formatConsoleInteger(level.voExpCumulative, i18n.resolvedLanguage) })}</span>
          </article>
        ))}
      </div>

      {audits.length > 0 && (
        <div className="experience-section-gap-lg">
          <div className="experience-section-title">{t('experience.levels.auditTitle')}</div>
          <div className="experience-mobile-list experience-mobile-list--always experience-section-gap-sm">
            {audits.map((audit) => (
              <article key={audit.voAuditId} className="experience-mobile-card">
                <strong>{audit.voOperatorName} · {formatConsoleDateTime(audit.voCreateTime, i18n.resolvedLanguage)}</strong>
                <span>{t('experience.levels.previewChanged', { count: audit.voChangedLevelCount })}</span>
                <span>{audit.voReason}</span>
              </article>
            ))}
          </div>
        </div>
      )}
    </section>
  );
};
