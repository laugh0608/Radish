import { useEffect, useState } from 'react';
import { ClockCircleOutlined, LinkOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { Button } from '@radish/ui';
import { ConsolePageHeader } from '../../components/ConsolePage';
import { createHangfireSession } from '../../api/hangfire';
import { getApiBaseUrl } from '../../config/env';
import { useDocumentTitle } from '../../hooks/useDocumentTitle';
import { log } from '../../utils/logger';
import './HangfirePage.css';

export const HangfirePage = () => {
  const { t } = useTranslation();
  useDocumentTitle(t('systemTools.hangfire.documentTitle'));
  const [attempt, setAttempt] = useState(0);
  const [dashboardUrl, setDashboardUrl] = useState<string>();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    let renewal: ReturnType<typeof setTimeout>;
    const connect = async () => {
      try {
        const session = await createHangfireSession(controller.signal);
        if (controller.signal.aborted) return;
        setDashboardUrl(`${getApiBaseUrl()}${session.voDashboardPath}`);
        setFailed(false);
        const delay = Math.max(10_000, Date.parse(session.voExpiresAtUtc) - Date.now() - 30_000);
        renewal = setTimeout(() => { void connect(); }, delay);
      } catch (error) {
        if (controller.signal.aborted) return;
        log.error('Hangfire', '任务看板会话建立或续期失败', error);
        setDashboardUrl(undefined);
        setFailed(true);
      }
    };
    void connect();
    return () => {
      controller.abort();
      clearTimeout(renewal);
    };
  }, [attempt]);

  return (
    <div className="admin-feature-page hangfire-page">
      <ConsolePageHeader
        title={t('systemTools.hangfire.title')}
        description={t('systemTools.hangfire.description')}
        icon={<ClockCircleOutlined />}
        actions={dashboardUrl ? (
          <a className="hangfire-page__external-link" href={dashboardUrl} target="_blank" rel="noreferrer">
            <LinkOutlined />{t('systemTools.hangfire.open')}
          </a>
        ) : undefined}
      />
      <section className="hangfire-page__shell" aria-label={t('systemTools.hangfire.panel.label')}>
        {dashboardUrl ? (
          <iframe className="hangfire-page__frame" src={dashboardUrl} title={t('systemTools.hangfire.panel.title')} />
        ) : (
          <div className="hangfire-page__state" role={failed ? 'alert' : 'status'}>
            <p>{t(failed ? 'systemTools.hangfire.failed' : 'systemTools.hangfire.loading')}</p>
            {failed ? <Button onClick={() => { setFailed(false); setAttempt(value => value + 1); }}>
              {t('systemTools.hangfire.retry')}
            </Button> : null}
          </div>
        )}
      </section>
    </div>
  );
};
