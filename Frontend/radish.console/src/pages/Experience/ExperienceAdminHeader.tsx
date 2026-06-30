import { Button, ReloadOutlined, TrophyOutlined } from '@radish/ui';
import {
  ConsolePageHeader,
  ConsoleStatusChip,
} from '@/components/ConsolePage';

type ExperienceAdminHeaderProps = {
  onRefresh: () => void;
  canAdjust: boolean;
  canFreeze: boolean;
};

export const ExperienceAdminHeader = ({ onRefresh, canAdjust, canFreeze }: ExperienceAdminHeaderProps) => {
  return (
    <ConsolePageHeader
      eyebrow="EXPERIENCE LEDGER"
      title="经验等级"
      description="支持按用户查看经验等级、调经验、冻结复核，并回看当前等级配置。"
      icon={<TrophyOutlined />}
      status={(
        <ConsoleStatusChip tone={canFreeze ? 'success' : (canAdjust ? 'info' : 'neutral')}>
          {canFreeze ? '可复核冻结' : (canAdjust ? '可调经验' : '只读查看')}
        </ConsoleStatusChip>
      )}
      actions={(
        <Button icon={<ReloadOutlined />} onClick={onRefresh}>
          刷新
        </Button>
      )}
    />
  );
};
