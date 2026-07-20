import { AntInput as Input, Button, Tag } from '@radish/ui';
import type { UserExperienceVo } from '@/api/experienceAdminApi';
import { useTranslation } from 'react-i18next';
import { formatConsoleDateTime, formatConsoleInteger } from '@/utils/localeFormatters';

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
  const { t, i18n } = useTranslation();

  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h3>{t('experience.query.title')}</h3>
          <p className="admin-feature-subtle">{t('experience.query.description')}</p>
        </div>
        <div className="experience-inline-actions">
          <Input
            placeholder={t('experience.form.userId')}
            value={queryUserId}
            onChange={(event) => onQueryUserIdChange(event.target.value)}
            onPressEnter={onQuery}
            className="experience-query-control"
          />
          <Button variant="primary" onClick={onQuery} disabled={loadingExperience}>
            {loadingExperience ? t('experience.actions.searching') : t('experience.actions.search')}
          </Button>
        </div>
      </div>

      <div className="admin-feature-metrics experience-section-gap-md">
        <div className="experience-query-current">
          {loadedUserId && experience
            ? t('experience.query.current', { name: experience.voUserName || t('experience.common.unnamedUser'), userId: loadedUserId })
            : t('experience.query.notLoaded')}
        </div>
        <div className="admin-feature-metric">
          <span>{t('experience.query.currentLevel')}</span>
          <strong>{experience ? `${experience.voCurrentLevelName} (L${experience.voCurrentLevel})` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('experience.query.totalExp')}</span>
          <strong>{experience ? formatConsoleInteger(experience.voTotalExp, i18n.resolvedLanguage) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('experience.query.nextLevel')}</span>
          <strong>{experience ? formatConsoleInteger(experience.voExpToNextLevel, i18n.resolvedLanguage) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('experience.table.status')}</span>
          <strong>{experience ? (experience.voExpFrozen ? t('experience.common.frozen') : t('experience.common.normal')) : '--'}</strong>
        </div>
      </div>
      {experience?.voExpFrozen && (
        <div className="experience-inline-actions experience-inline-actions--center experience-section-gap-sm">
          <Tag color="warning">{t('experience.common.freezing')}</Tag>
          <span>{t('experience.query.expiresAt', { value: experience.voFrozenUntil ? formatConsoleDateTime(experience.voFrozenUntil, i18n.resolvedLanguage) : t('experience.common.permanentFreeze') })}</span>
          <span>{t('experience.query.reason', { value: experience.voFrozenReason || t('experience.common.notProvided') })}</span>
        </div>
      )}
    </section>
  );
};
