import { Button, ReloadOutlined, TrophyOutlined } from '@radish/ui';
import {
  ConsolePageHeader,
  ConsoleStatusChip,
} from '@/components/ConsolePage';
import { useTranslation } from 'react-i18next';

type ExperienceAdminHeaderProps = {
  onRefresh: () => void;
  canAdjust: boolean;
  canFreeze: boolean;
};

export const ExperienceAdminHeader = ({ onRefresh, canAdjust, canFreeze }: ExperienceAdminHeaderProps) => {
  const { t } = useTranslation();

  return (
    <ConsolePageHeader
      eyebrow={t('experience.header.eyebrow')}
      title={t('experience.header.title')}
      description={t('experience.header.description')}
      icon={<TrophyOutlined />}
      status={(
        <ConsoleStatusChip tone={canFreeze ? 'success' : (canAdjust ? 'info' : 'neutral')}>
          {canFreeze ? t('experience.header.canFreeze') : (canAdjust ? t('experience.header.canAdjust') : t('experience.header.readOnly'))}
        </ConsoleStatusChip>
      )}
      actions={(
        <Button icon={<ReloadOutlined />} onClick={onRefresh}>
          {t('experience.actions.refresh')}
        </Button>
      )}
    />
  );
};
