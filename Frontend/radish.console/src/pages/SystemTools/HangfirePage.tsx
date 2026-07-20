import { ClockCircleOutlined, LinkOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
} from '../../components/ConsolePage';
import { getApiBaseUrl } from '../../config/env';
import { useDocumentTitle } from '../../hooks/useDocumentTitle';
import './HangfirePage.css';

export const HangfirePage = () => {
  const { t } = useTranslation();
  useDocumentTitle(t('systemTools.hangfire.documentTitle'));
  const hangfireDashboardUrl = `${getApiBaseUrl()}/hangfire`;

  return (
    <div className="admin-feature-page hangfire-page">
      <ConsolePageHeader
        eyebrow={t('systemTools.hangfire.eyebrow')}
        title={t('systemTools.hangfire.title')}
        description={t('systemTools.hangfire.description')}
        icon={<ClockCircleOutlined />}
        status={<ConsoleStatusChip tone="warning">{t('systemTools.hangfire.status')}</ConsoleStatusChip>}
        actions={(
          <a
            className="hangfire-page__external-link"
            href={hangfireDashboardUrl}
            target="_blank"
            rel="noreferrer"
          >
            <LinkOutlined />
            {t('systemTools.hangfire.open')}
          </a>
        )}
      />

      <ConsoleMetricGrid label={t('systemTools.hangfire.metrics.label')}>
        <ConsoleMetricCard
          label={t('systemTools.hangfire.metrics.entry.label')}
          value={t('systemTools.hangfire.metrics.entry.value')}
          description={t('systemTools.hangfire.metrics.entry.description')}
          tone="info"
        />
        <ConsoleMetricCard
          label={t('systemTools.hangfire.metrics.guard.label')}
          value={t('systemTools.hangfire.metrics.guard.value')}
          description={t('systemTools.hangfire.metrics.guard.description')}
          tone="success"
        />
        <ConsoleMetricCard
          label={t('systemTools.hangfire.metrics.internal.label')}
          value={t('systemTools.hangfire.metrics.internal.value')}
          description={t('systemTools.hangfire.metrics.internal.description')}
          tone="warning"
        />
      </ConsoleMetricGrid>

      <section className="hangfire-page__shell" aria-label={t('systemTools.hangfire.panel.label')}>
        <div className="hangfire-page__notice">
          <span>{t('systemTools.hangfire.panel.notice')}</span>
        </div>
        <iframe
          className="hangfire-page__frame"
          src={hangfireDashboardUrl}
          title={t('systemTools.hangfire.panel.title')}
        />
      </section>
    </div>
  );
};
