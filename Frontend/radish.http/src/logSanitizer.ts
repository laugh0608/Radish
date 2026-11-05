/** 浏览器日志只输出受控摘要；这个模块不读取应用配置或执行网络请求。 */
export const REDACTED_LOG_VALUE = '[REDACTED]';

const SENSITIVE_FIELDS = new Set([
  'password', 'pwd', 'passcode', 'paymentpassword', 'paymentpasscode',
  'currentpassword', 'newpassword', 'confirmpassword', 'oldpassword',
  'accesstoken', 'refreshtoken', 'idtoken', 'token', 'secret', 'apikey',
  'authorization', 'cookie', 'setcookie', 'body', 'requestbody', 'responsebody',
]);

export function isSensitiveLogField(fieldName: string): boolean {
  return SENSITIVE_FIELDS.has(fieldName.toLowerCase().replace(/[^a-z0-9]/g, ''));
}

export type LogFieldRule =
  | { readonly kind: 'integer'; readonly min: number; readonly max: number }
  | { readonly kind: 'boolean' }
  | { readonly kind: 'enum'; readonly values: readonly string[] };

export type LogFieldRules = Readonly<Record<string, LogFieldRule>>;

export interface SafeLogPosition {
  resource: 'application_bundle' | 'application_source';
  line: number;
  column: number;
}

export interface SafeErrorSummary {
  failureKind: 'error' | 'type_error' | 'range_error' | 'syntax_error' | 'reference_error' | 'uri_error' | 'unknown';
  positions?: SafeLogPosition[];
  omitted: true;
  truncated?: true;
}

/** 访问器不求值；Proxy 反射失败仅影响该日志字段。 */
export function readLogDataProperty(value: unknown, key: string): unknown {
  if (value === null || typeof value !== 'object') return undefined;
  try {
    const descriptor = Object.getOwnPropertyDescriptor(value, key);
    return descriptor && 'value' in descriptor ? descriptor.value : undefined;
  } catch {
    return undefined;
  }
}

const ERROR_PROTOTYPES: readonly [object, SafeErrorSummary['failureKind']][] = [
  [TypeError.prototype, 'type_error'], [RangeError.prototype, 'range_error'],
  [SyntaxError.prototype, 'syntax_error'], [ReferenceError.prototype, 'reference_error'],
  [URIError.prototype, 'uri_error'], [Error.prototype, 'error'],
];

/** 不读取异常 name / message / cause，也不调用异常转换方法。 */
export function summarizeLogError(value: unknown): SafeErrorSummary | undefined {
  if (value === null || typeof value !== 'object') return undefined;
  try {
    let prototype: object | null = Object.getPrototypeOf(value);
    for (let depth = 0; prototype && depth < 4; depth += 1) {
      const match = ERROR_PROTOTYPES.find(([known]) => known === prototype);
      if (match) {
        const stack = readLogDataProperty(value, 'stack');
        const positions = parseLogPositions(stack);
        const truncated = typeof stack === 'string'
          && (stack.length > 65536 || positions.length === 32 || stack.split('\n', 66).length > 65);
        return { failureKind: match[1], ...(positions.length ? { positions } : {}),
          omitted: true, ...(truncated ? { truncated: true } : {}) };
      }
      prototype = Object.getPrototypeOf(prototype);
    }
  } catch {
    // 不输出反射异常：它本身可能携带输入原文。
  }
  return undefined;
}

/** 只识别应用资源位置，绝不返回资源名称、地址、函数名或异常首行。 */
export function parseLogPositions(stack: unknown): SafeLogPosition[] {
  if (typeof stack !== 'string' || stack.length > 65536) return [];
  const positions: SafeLogPosition[] = [];
  const lines = stack.split('\n', 65);
  for (const line of lines.slice(1)) {
    if (positions.length === 32) break;
    // Chromium 的 at (...) / at URL 与 Firefox 的 function@URL。
    const match = /(?:\(|@|\bat )(https?:\/\/[^\s()?#]+):(\d{1,7}):(\d{1,7})\)?$/.exec(line.trim());
    if (!match) continue;
    try {
      const url = new URL(match[1]);
      if (url.username || url.password || url.search || url.hash) continue;
      const resource = /^\/(?:console\/)?assets\/[a-zA-Z0-9_-]+\.js$/.test(url.pathname)
        ? 'application_bundle'
        : /^\/src\/(?:[a-zA-Z0-9_-]+\/)*[a-zA-Z0-9_-]+\.tsx?$/.test(url.pathname)
          ? 'application_source'
          : undefined;
      const row = Number(match[2]);
      const column = Number(match[3]);
      if (resource && row > 0 && column > 0) positions.push({ resource, line: row, column });
    } catch {
      // 不支持的栈格式省略，不降级到原文。
    }
  }
  return positions;
}

/** 字段规则来自编译期事件目录；任意输入键、字符串和数字不自动放行。 */
export function collectLogFields(args: readonly unknown[], rules: LogFieldRules = {}): Record<string, string | number | boolean> {
  const result: Record<string, string | number | boolean> = Object.create(null);
  for (const [key, rule] of Object.entries(rules).slice(0, 32)) {
    if (isSensitiveLogField(key) || key === '__proto__' || key === 'constructor' || key === 'prototype') continue;
    for (const arg of args.slice(0, 32)) {
      const value = readLogDataProperty(arg, key);
      if (rule.kind === 'integer' && typeof value === 'number' && Number.isSafeInteger(value) && value >= rule.min && value <= rule.max) {
        result[key] = value;
        break;
      }
      if (rule.kind === 'boolean' && typeof value === 'boolean') {
        result[key] = value;
        break;
      }
      if (rule.kind === 'enum' && typeof value === 'string' && rule.values.includes(value)) {
        // 从可信目录取枚举常量，未知字符串不输出。
        result[key] = rule.values[rule.values.indexOf(value)];
        break;
      }
    }
  }
  return result;
}

/** 兼容入口现在返回摘要；不再递归复制业务对象。 */
export function sanitizeLogValue(value: unknown): unknown {
  if (value === null || value === undefined) return null;
  return summarizeLogError(value) ?? REDACTED_LOG_VALUE;
}

export function sanitizeLogArgs(args: unknown[]): unknown[] {
  const length = readLogDataProperty(args, 'length');
  if (typeof length !== 'number' || !Number.isSafeInteger(length) || length < 0) return [];
  return Array.from({ length: Math.min(length, 32) }, (_, index) =>
    sanitizeLogValue(readLogDataProperty(args, String(index))));
}
