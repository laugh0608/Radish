import { ClockCircleOutlined, LinkOutlined } from '@ant-design/icons';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
} from '../../components/ConsolePage';
import { getApiBaseUrl } from '../../config/env';
import './HangfirePage.css';

export const HangfirePage = () => {
  const hangfireDashboardUrl = `${getApiBaseUrl()}/hangfire`;

  return (
    <div className="admin-feature-page hangfire-page">
      <ConsolePageHeader
        eyebrow="OPERATIONS JOBS"
        title="定时任务"
        description="通过受保护的 Console 入口查看 Hangfire Dashboard，当前仍保留外部运维面板承载。"
        icon={<ClockCircleOutlined />}
        status={<ConsoleStatusChip tone="warning">外部 Dashboard</ConsoleStatusChip>}
        actions={(
          <a
            className="hangfire-page__external-link"
            href={hangfireDashboardUrl}
            target="_blank"
            rel="noreferrer"
          >
            <LinkOutlined />
            打开新窗口
          </a>
        )}
      />

      <ConsoleMetricGrid label="定时任务入口状态">
        <ConsoleMetricCard label="入口类型" value="iframe" description="复用 Gateway 暴露的 Hangfire Dashboard" tone="info" />
        <ConsoleMetricCard label="权限守卫" value="已接入" description="沿用 console.hangfire.view 路由权限" tone="success" />
        <ConsoleMetricCard label="内部任务台" value="未启用" description="任务队列、重试和审计 API 尚未定义" tone="warning" />
      </ConsoleMetricGrid>

      <section className="hangfire-page__shell" aria-label="Hangfire Dashboard 外部面板">
        <div className="hangfire-page__notice">
          <span>当前页面只承载外部 Dashboard，不在前端内重建任务队列、失败重试或运行审计模型。</span>
        </div>
        <iframe
          className="hangfire-page__frame"
          src={hangfireDashboardUrl}
          title="Hangfire Dashboard"
        />
      </section>
    </div>
  );
};
