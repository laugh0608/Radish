import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router';
import {
  ApiResponseError, runtimeLogConsole, type RuntimeLogAlert, type RuntimeLogAlerts, type RuntimeLogDetail,
  type RuntimeLogHistory, type RuntimeLogPage, type RuntimeLogPipeline, type RuntimeLogQuery,
} from '@radish/http';
import { usePermission } from '@/hooks/usePermission';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import './runtimeLogs.css';

const sources = ['api', 'auth', 'gateway', 'frontend', 'dbmigrate', 'postgres', 'redis', 'log-collector'];
const displayTime = (value: string) => new Date(value).toLocaleString();
const pipelineLabels = { Unknown: '状态未知', Faulted: '采集异常', Healthy: '采集已确认' };
const alertLabels = { Open: '待确认', Acknowledged: '已确认', Resolved: '已解决' };
const isDenied = (error: unknown) => error instanceof ApiResponseError &&
  [401, 403].includes(error.httpStatus ?? error.statusCode ?? 0);
const errorText = (error: unknown) => isDenied(error) ? '当前账号无法访问此数据' :
  error instanceof Error ? error.message : '日志服务暂不可用';

function PipelineStatus({ data }: { data?: RuntimeLogPipeline }) {
  const state = data?.voState ?? 'Unknown';
  return <section className="runtime-log-panel runtime-log-pipeline" aria-label="采集状态" role="status">
    <div><span className="runtime-log-label">采集状态</span><strong data-state={state}>{pipelineLabels[state]}</strong></div>
    {data?.voObservedAtUtc && <span>采样 {displayTime(data.voObservedAtUtc)}</span>}
    {data?.voActiveChunks != null && <span>活跃 chunk {data.voActiveChunks}</span>}
    {data?.voKnownWriteFailures != null && <span>累计写入失败 {data.voKnownWriteFailures}</span>}
    {data?.voTailCountUnknown && <span>未确认尾段数量未知</span>}
  </section>;
}

function EventDetail({ value, onClose }: { value: RuntimeLogDetail; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { dialog.current?.showModal(); }, []);
  return <dialog ref={dialog} onClose={onClose} className="runtime-log-detail">
    <header><h2>事件详情</h2><button type="button" onClick={onClose}>关闭</button></header>
    <p>{value.voMessage}</p>
    <dl>
      <dt>事件 ID</dt><dd>{value.voEvent.voEventId}</dd>
      <dt>事件码</dt><dd>{value.voEvent.voEventCode}</dd>
      <dt>关联 ID</dt><dd>{[value.voEvent.voTraceId, value.voEvent.voOperationId, value.voRequestId].filter(Boolean).join(' / ') || '—'}</dd>
      <dt>观察时间</dt><dd>{displayTime(value.voObservedAtUtc)}</dd>
      <dt>入库时间</dt><dd>{displayTime(value.voIngestedAtUtc)}</dd>
    </dl>
    {Object.keys(value.voProperties).length > 0 && <pre>{JSON.stringify(value.voProperties, null, 2)}</pre>}
  </dialog>;
}

function LogQuery({ disabled }: { disabled: boolean }) {
  const [search] = useSearchParams();
  const eventId = search.get('event');
  const canExport = usePermission(CONSOLE_PERMISSIONS.logsExport);
  const canAudit = usePermission(CONSOLE_PERMISSIONS.logsAudit);
  const [tab, setTab] = useState<'runtime' | 'history' | 'audit'>('runtime');
  const [filter, setFilter] = useState<RuntimeLogQuery>({});
  const [page, setPage] = useState<RuntimeLogPage>();
  const [history, setHistory] = useState<RuntimeLogHistory[]>();
  const [detail, setDetail] = useState<RuntimeLogDetail>();
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [offset, setOffset] = useState<string>();
  const detailRequest = useRef<AbortController | undefined>(undefined);

  useEffect(() => {
    if (disabled) return;
    const controller = new AbortController();
    const load = async () => {
      setLoading(true);
      setError('');
      try {
        if (tab === 'runtime') {
          setPage(await runtimeLogConsole.query(offset ? { cursor: offset } : filter, controller.signal));
        } else setHistory(await runtimeLogConsole.history(tab === 'audit', filter, controller.signal));
      } catch (failure) {
        if (!controller.signal.aborted) {
          setPage(undefined);
          setHistory(undefined);
          setDetail(undefined);
          setError(errorText(failure));
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };
    void load();
    return () => controller.abort();
  }, [disabled, filter, offset, tab]);

  const loadDetail = (id: string) => {
    detailRequest.current?.abort();
    const controller = new AbortController();
    detailRequest.current = controller;
    setError('');
    void runtimeLogConsole.detail(id, controller.signal).then(setDetail).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(errorText(failure));
    });
  };
  useEffect(() => {
    if (eventId && !disabled) loadDetail(eventId);
    return () => detailRequest.current?.abort();
  }, [disabled, eventId]);

  if (disabled) return <p role="alert">当前账号无法访问此数据</p>;

  const apply = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    const field = (key: string) => String(fields.get(key) ?? '').trim() || undefined;
    const date = (key: string) => {
      const value = field(key);
      return value ? new Date(value).toISOString() : undefined;
    };
    setOffset(undefined);
    setFilter({
      from: date('from'), to: date('to'), services: fields.getAll('services').map(String),
      level: field('level'), eventCode: field('eventCode'), traceId: field('traceId'),
      operationId: field('operationId'), keyword: field('keyword'),
    });
  };

  const download = async () => {
    try {
      const result = await runtimeLogConsole.export(filter);
      const url = URL.createObjectURL(new Blob([result.voCsv], { type: 'text/csv;charset=utf-8' }));
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'runtime-logs.csv';
      anchor.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (failure) { setError(errorText(failure)); }
  };

  const changeTab = (next: typeof tab) => {
    setOffset(undefined);
    setTab(next);
  };

  return <>
    <nav className="runtime-log-actions" aria-label="日志视图">
      <button type="button" aria-pressed={tab === 'runtime'} onClick={() => changeTab('runtime')}>运行日志</button>
      <button type="button" aria-pressed={tab === 'history'} onClick={() => changeTab('history')}>旧日志</button>
      {canAudit && <button type="button" aria-pressed={tab === 'audit'} onClick={() => changeTab('audit')}>审计摘要</button>}
    </nav>
    <form onSubmit={apply} className="runtime-log-panel runtime-log-filters">
      <label>起始时间<input type="datetime-local" name="from" /></label>
      <label>结束时间<input type="datetime-local" name="to" /></label>
      {tab === 'runtime' && <>
        <label>等级<select name="level"><option value="">全部</option><option>Info</option><option>Warning</option><option>Error</option></select></label>
        <label>事件码<input name="eventCode" maxLength={128} /></label>
        <label>事件码关键词<input name="keyword" maxLength={64} /></label>
        <label>Trace ID<input name="traceId" maxLength={32} /></label>
        <label>Operation ID<input name="operationId" maxLength={36} /></label>
        <fieldset><legend>来源</legend><div className="runtime-log-actions">{sources.map(source =>
          <label key={source} className="runtime-log-source"><input type="checkbox" name="services" value={source} />{source}</label>)}</div></fieldset>
      </>}
      <div className="runtime-log-actions runtime-log-filter-actions">
        <button type="submit">查询</button>
        {tab === 'runtime' && canExport && <button type="button" onClick={() => void download()}>导出 CSV</button>}
      </div>
    </form>
    <section className="runtime-log-panel" aria-label="查询结果" aria-busy={loading}>
      {error && <p role="alert">{error}</p>}
      {loading && <p role="status">正在查询</p>}
      {tab === 'runtime' && page && <div>快照 {displayTime(page.voSnapshotUtc)}</div>}
      {!loading && !error && (tab === 'runtime' ? !page?.voItems.length : !history?.length) && <p>此窗口没有记录</p>}
      {tab === 'runtime' && page?.voItems.length ? <table className="runtime-log-table">
        <thead><tr><th>发生时间</th><th>来源</th><th>等级</th><th>事件码</th><th>详情</th></tr></thead>
        <tbody>{page.voItems.map(row => <tr key={row.voEventId}>
          <td data-label="发生时间">{displayTime(row.voOccurredAtUtc)}</td><td data-label="来源">{row.voService}</td>
          <td data-label="等级">{row.voLevel}</td><td data-label="事件码"><code>{row.voEventCode}</code></td>
          <td><button type="button" onClick={() => loadDetail(row.voEventId)}>查看详情</button></td>
        </tr>)}</tbody>
      </table> : null}
      {tab === 'runtime' && page?.voNextCursor && <button type="button" disabled={loading} onClick={() => setOffset(page.voNextCursor)}>下一页</button>}
      {tab !== 'runtime' && history?.length ? <ul className="runtime-log-history">{history.map(row =>
        <li key={row.voId}>{row.voLocalTime} · {row.voCategory ?? '未知'} · {row.voSource === 'audit' ? '审计' : '旧日志'} · 时区未知</li>
      )}</ul> : null}
    </section>
    {detail && <EventDetail value={detail} onClose={() => setDetail(undefined)} />}
  </>;
}

export const RuntimeLogsPage = () => {
  const location = useLocation();
  const alertView = location.pathname.endsWith('/alerts');
  const canReadAlerts = usePermission(CONSOLE_PERMISSIONS.alertsView);
  const canManage = usePermission(CONSOLE_PERMISSIONS.alertsManage);
  const canReadLogs = usePermission(CONSOLE_PERMISSIONS.logsView);
  const [pipeline, setPipeline] = useState<RuntimeLogPipeline>();
  const [alerts, setAlerts] = useState<RuntimeLogAlerts>();
  const [error, setError] = useState('');
  const [actionError, setActionError] = useState('');
  const [denied, setDenied] = useState(false);
  const [reload, setReload] = useState(0);
  const [updating, setUpdating] = useState<string>();

  useEffect(() => {
    if (denied || !(canReadAlerts || canReadLogs)) return;
    let pending: AbortController | undefined;
    const poll = async () => {
      if (document.hidden || pending) return;
      const controller = new AbortController();
      pending = controller;
      const reads = await Promise.allSettled([
        runtimeLogConsole.pipeline(controller.signal),
        canReadAlerts ? runtimeLogConsole.alerts(controller.signal) : Promise.resolve(undefined),
      ]);
      pending = undefined;
      if (controller.signal.aborted) return;
      if (reads.some(result => result.status === 'rejected' && isDenied(result.reason))) {
        setDenied(true);
        setPipeline(undefined);
        setAlerts(undefined);
        return;
      }
      setPipeline(reads[0].status === 'fulfilled' ? reads[0].value : undefined);
      setAlerts(reads[1].status === 'fulfilled' ? reads[1].value : undefined);
      setError(reads[1].status === 'rejected' ? errorText(reads[1].reason) : '');
    };
    void poll();
    const timer = window.setInterval(() => void poll(), 2500);
    const visibility = () => { if (document.hidden) pending?.abort(); else void poll(); };
    document.addEventListener('visibilitychange', visibility);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', visibility);
      pending?.abort();
    };
  }, [canReadAlerts, canReadLogs, denied, reload]);

  const transition = async (item: RuntimeLogAlert, action: 'acknowledge' | 'resolve') => {
    setUpdating(item.voId);
    setActionError('');
    try {
      await runtimeLogConsole.transition(item, action);
    } catch (failure) { setActionError(errorText(failure)); }
    finally {
      setUpdating(undefined);
      setReload(value => value + 1);
    }
  };

  return <main className="runtime-log-page">
    <header className="runtime-log-heading">
      <h1>{alertView ? '运维告警' : '运行日志'}</h1>
      <nav className="runtime-log-actions" aria-label="运行日志和告警">
        {canReadLogs && <Link to="/system/logs">日志查询</Link>}
        {canReadAlerts && <Link to="/system/alerts">运维告警{alerts && ' · ' + alerts.voOpenCount + ' 待确认'}</Link>}
      </nav>
    </header>
    {!denied && <PipelineStatus data={pipeline} />}
    {alertView ? <section className="runtime-log-panel" aria-label="告警列表">
      {(denied || error || actionError) && <p role="alert">{denied ? '当前账号无法查看告警' : (actionError || error)}</p>}
      {!error && !denied && alerts && alerts.voItems.length === 0 && <p>暂无告警</p>}
      {!denied && alerts?.voItems.map(item => <article key={item.voId} className="runtime-log-alert">
        <header><strong>{item.voCode === 'collector.storage-write-failed' ? '采集缓冲写入失败' : item.voCode}</strong>
          <span>{alertLabels[item.voState]}</span></header>
        <p>{item.voService} · {displayTime(item.voOpenedUtc)} · 已确认失败 {item.voKnownFailures}</p>
        {item.voCode === 'collector.storage-write-failed' && <p>故障窗口的尾段数量未知</p>}
        <div className="runtime-log-actions">
          {canReadLogs && item.voEventId && <Link to={'/system/logs?event=' + encodeURIComponent(item.voEventId)}>关联事件</Link>}
          {canManage && item.voState === 'Open' &&
            <button type="button" disabled={updating === item.voId} onClick={() => void transition(item, 'acknowledge')}>确认</button>}
          {canManage && item.voState !== 'Resolved' && item.voCode !== 'collector.storage-write-failed' &&
            <button type="button" disabled={updating === item.voId} onClick={() => void transition(item, 'resolve')}>解决</button>}
        </div>
      </article>)}
    </section> : <LogQuery disabled={denied} />}
  </main>;
};
