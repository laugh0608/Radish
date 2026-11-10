import { collectLogFields, summarizeLogError } from './logSanitizer';
import type { LogFieldRules, SafeErrorSummary } from './logSanitizer';

export const LogLevel = { INFO: 'Info', WARN: 'Warning', ERROR: 'Error' } as const;
export type LogLevel = typeof LogLevel[keyof typeof LogLevel];
export type BrowserLogSource = 'radish.client' | 'radish.console' | 'radish.http';

/** 每项对应经过审阅的固定 tag / message；属性按事件单独登记。 */
export type BrowserLogDefinition = readonly [tag: string | null, message: string, fields?: LogFieldRules];
export type BrowserLogCatalog = readonly BrowserLogDefinition[];

interface BrowserLoggerOptions {
  source: BrowserLogSource;
  catalog: BrowserLogCatalog;
  isDevelopment: boolean;
  diagnostics: boolean;
}

interface LogEvent {
  schemaVersion: 1;
  source: BrowserLogSource;
  eventCode: string;
  level: LogLevel;
  diagnostic: boolean;
  message: string;
  tag?: string;
  properties?: Record<string, string | number | boolean>;
  failures?: SafeErrorSummary[];
  omitted?: boolean;
  truncated?: boolean;
}

const EVENT_CODES: Record<LogLevel, string> = {
  Info: 'browser.application_info', Warning: 'browser.application_warning', Error: 'browser.application_failed',
};
const MAX_EVENT_BYTES = 8192;
const encoder = new TextEncoder();

/** 最终出口只有固定格式串和已序列化快照，浏览器不能延迟展开原对象。 */
function serializeEvent(event: LogEvent): string {
  const serialized = JSON.stringify(event);
  if (encoder.encode(serialized).byteLength <= MAX_EVENT_BYTES) return serialized;
  // 优先裁剪位置细节，保留已登记事件的业务说明和分类。
  const compact = JSON.stringify({ ...event, truncated: true, omitted: true,
    failures: event.failures?.map(({ failureKind }) => ({ failureKind, omitted: true })) });
  if (encoder.encode(compact).byteLength <= MAX_EVENT_BYTES) return compact;
  return JSON.stringify({
    schemaVersion: 1, source: event.source, eventCode: 'browser.log_truncated',
    level: event.level, diagnostic: event.diagnostic, message: '日志超出输出预算', truncated: true, omitted: true,
  });
}

export function createBrowserLogger(options: BrowserLoggerOptions) {
  const diagnostics = options.isDevelopment && options.diagnostics;
  const definitions = new Map<string, BrowserLogDefinition>();
  for (const definition of options.catalog) {
    definitions.set(JSON.stringify([definition[0], definition[1]]), definition);
  }
  const timers = new Map<string, number>();
  let writing = false;
  let processing = false;

  const output = (event: LogEvent): void => {
    if (writing) return;
    writing = true;
    try {
      const serialized = serializeEvent(event);
      const method = event.level === 'Error' ? 'error' : event.level === 'Warning' ? 'warn' : 'info';
      console[method]('%s', serialized);
    } catch {
      // console 被覆盖或序列化失败不改变业务行为，不再次输出不可信原文。
    } finally {
      writing = false;
    }
  };

  const resolve = (first: string, second?: unknown): { definition?: BrowserLogDefinition; tagged: boolean } => {
    if (typeof first !== 'string' || first.length > 2048) return { tagged: false };
    if (typeof second === 'string' && second.length <= 2048) {
      const definition = definitions.get(JSON.stringify([first, second]));
      if (definition) return { definition, tagged: true };
    }
    return { definition: definitions.get(JSON.stringify([null, first])), tagged: false };
  };

  const write = (level: LogLevel, diagnostic: boolean, first: string, args: unknown[]): void => {
    if (processing || (diagnostic && !diagnostics)) return;
    processing = true;
    try {
      const { definition, tagged } = resolve(first, args[0]);
      if (!definition) {
        output({ schemaVersion: 1, source: options.source, eventCode: 'browser.log_unregistered', level,
          diagnostic, message: '未登记的日志内容已省略', omitted: true });
        return;
      }
      const values = tagged ? args.slice(1, 33) : args.slice(0, 32);
      const failures: SafeErrorSummary[] = [];
      let truncated = args.length > (tagged ? 33 : 32);
      for (const value of values) {
        const error = summarizeLogError(value);
        if (error) {
          if (failures.length < 4) failures.push(error);
          else truncated = true;
          if (error.truncated) truncated = true;
        }
      }
      const properties = collectLogFields(values, definition[2]);
      output({
        schemaVersion: 1, source: options.source, eventCode: EVENT_CODES[level], level, diagnostic,
        message: definition[1], ...(definition[0] ? { tag: definition[0] } : {}),
        ...(Object.keys(properties).length ? { properties } : {}),
        ...(failures.length ? { failures } : {}),
        ...(values.length ? { omitted: true } : {}),
        ...(truncated ? { truncated: true } : {}),
      });
    } finally {
      processing = false;
    }
  };

  const auxiliary = (action: string, label?: string, extra: Record<string, string | number | boolean> = {}): void => {
    if (!diagnostics) return;
    const definition = label === undefined ? undefined : resolve(label).definition;
    output({ schemaVersion: 1, source: options.source,
      eventCode: 'browser.diagnostic_helper', level: 'Info', diagnostic: true,
      message: '开发诊断辅助事件', properties: { action, ...extra },
      ...(definition ? { tag: definition[1] } : {}), omitted: true });
  };

  return {
    debug: (message: string, ...args: unknown[]) => write('Info', true, message, args),
    info: (message: string, ...args: unknown[]) => write('Info', false, message, args),
    warn: (message: string, ...args: unknown[]) => write('Warning', false, message, args),
    error: (message: string, ...args: unknown[]) => write('Error', false, message, args),
    group: (label: string, collapsed = false) => auxiliary('group', label, { collapsed: collapsed === true }),
    groupEnd: () => auxiliary('group_end'),
    table: (data: Record<string, unknown> | unknown[]) => {
      // 保留调用签名，只记录动作，不访问表格内容。
      void data;
      auxiliary('table');
    },
    time: (label: string) => {
      if (!diagnostics) return;
      const definition = resolve(label).definition;
      if (!definition) return auxiliary('time', undefined, { state: 'unregistered' });
      if (timers.has(definition[1])) return auxiliary('time', label, { state: 'already_started' });
      if (timers.size >= 32) return auxiliary('time', label, { state: 'limit' });
      timers.set(definition[1], performance.now());
      auxiliary('time', label, { state: 'started' });
    },
    timeEnd: (label: string) => {
      if (!diagnostics) return;
      const definition = resolve(label).definition;
      if (!definition) return auxiliary('time_end', undefined, { state: 'unregistered' });
      const start = timers.get(definition[1]);
      if (start === undefined) return auxiliary('time_end', label, { state: 'not_started' });
      timers.delete(definition[1]);
      const elapsed = Math.max(0, Math.min(86400000, Math.round(performance.now() - start)));
      auxiliary('time_end', label, { state: 'completed', durationMs: elapsed });
    },
  };
}

const HTTP_EVENTS: Record<string, { message: string; fields?: LogFieldRules }> = {
  'http.operation_failed': { message: 'API 操作失败' },
  'http.api_failed': { message: 'API 返回业务错误', fields: {
    code: { kind: 'enum', values: ['Common.ValidationFailed', 'Common.Conflict', 'Common.NotFound', 'Common.Unauthorized', 'Common.Forbidden'] },
  } },
  'http.network_failed': { message: '网络请求失败' },
  'http.response_failed': { message: 'HTTP 请求失败', fields: { status: { kind: 'integer', min: 100, max: 599 } } },
  'http.token_refresh_failed': { message: 'Token 刷新失败', fields: {
    errorType: { kind: 'enum', values: ['network_error', 'invalid_token', 'server_error', 'unknown'] },
  } },
};
let writingHttpLog = false;

/** HTTP 事件没有诊断开关，必须在应用尚未初始化时也能安全输出。 */
export function writeHttpLog(eventCode: 'http.operation_failed' | 'http.api_failed' | 'http.network_failed' | 'http.response_failed' | 'http.token_refresh_failed', value: unknown): void {
  if (writingHttpLog) return;
  if (!Object.prototype.hasOwnProperty.call(HTTP_EVENTS, eventCode)) return;
  const definition = HTTP_EVENTS[eventCode];
  writingHttpLog = true;
  try {
    const properties = collectLogFields([value], definition.fields);
    if (eventCode === 'http.api_failed' && properties.code === undefined) properties.code = 'unknown';
    if (eventCode === 'http.token_refresh_failed' && properties.errorType === undefined) properties.errorType = 'unknown';
    const failure = summarizeLogError(value);
    console.error('%s', serializeEvent({
      schemaVersion: 1, source: 'radish.http', eventCode, level: 'Error', diagnostic: false,
      message: definition.message, ...(Object.keys(properties).length ? { properties } : {}),
      ...(failure ? { failures: [failure] } : {}), omitted: true,
    }));
  } catch {
    // 日志输出故障不阻止原错误回调、响应返回和认证失效通知。
  } finally {
    writingHttpLog = false;
  }
}
