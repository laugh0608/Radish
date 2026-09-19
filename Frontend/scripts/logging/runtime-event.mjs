import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';

// 与 .NET 嵌入资源使用同一个策略文件；禁止维护第二份等级 / 白名单。
const contract = JSON.parse(readFileSync(new URL('../../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json', import.meta.url), 'utf8'));
const rank = { Info: 0, Warning: 1, Error: 2 };
const sourceToken = /^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$/u;
const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const allowedInput = new Set(['level', 'diagnostic', 'eventCode', 'sourceCategory', 'properties', 'traceId', 'spanId', 'operationId']);
const correlations = {
  traceId: /^[0-9a-f]{32}$/u,
  spanId: /^[0-9a-f]{16}$/u,
  operationId: /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/u,
};

/** source 只能来自受信宿主配置；返回的事件生成一次，供所有介质及重试复用。 */
export function createRuntimeLogPolicy(source, { mode = 'Production', minimumLevel = 'Info', diagnostics = false, now = () => new Date() } = {}) {
  if (!['Development', 'Production'].includes(mode) || !Object.hasOwn(rank, minimumLevel) ||
      typeof diagnostics !== 'boolean' || (mode === 'Production' && diagnostics)) {
    throw new Error('Invalid runtime logging mode, level or diagnostics configuration.');
  }
  if (!contract.services.includes(source.service) ||
      [source.deploymentId, source.instanceId, source.release].some((value) => typeof value !== 'string' || !sourceToken.test(value) || /[\r\n]/u.test(value))) {
    throw new Error('Invalid runtime logging source configuration.');
  }
  const origin = { deploymentId: source.deploymentId, service: source.service, instanceId: source.instanceId, release: source.release };
  return (input) => {
    if (!isObject(input)) throw new Error('Expected event object.');
    if ((Object.hasOwn(input, 'level') && typeof input.level !== 'string') ||
        (Object.hasOwn(input, 'diagnostic') && typeof input.diagnostic !== 'boolean')) {
      throw new Error('Invalid runtime log level or diagnostic flag type.');
    }
    const rawLevel = typeof input.level === 'string' ? input.level : 'Info';
    if (!Object.hasOwn(contract.levels, rawLevel)) throw new Error('Unknown runtime log level.');
    const mapping = contract.levels[rawLevel];
    const diagnostic = mapping.diagnostic || input.diagnostic === true;
    if ((diagnostic && (mode === 'Production' || !diagnostics)) || rank[mapping.level] < rank[minimumLevel]) return null;
    const code = typeof input.eventCode === 'string' ? input.eventCode : 'runtime.unclassified';
    const known = Object.hasOwn(contract.events, code);
    const eventCode = known ? code : 'runtime.unclassified';
    let redacted = !known || Object.keys(input).some((key) => !allowedInput.has(key));
    for (const key of ['eventCode', 'sourceCategory']) {
      if (Object.hasOwn(input, key) && typeof input[key] !== 'string') redacted = true;
    }
    let sourceCategory = typeof input.sourceCategory === 'string' ? input.sourceCategory : 'application';
    if (!contract.categories.includes(sourceCategory)) { sourceCategory = 'application'; redacted = true; }
    const properties = {};
    if (Object.hasOwn(input, 'properties')) {
      if (!isObject(input.properties)) redacted = true;
      else for (const key of Object.keys(input.properties).sort()) {
        if (!Object.hasOwn(contract.properties, key)) { redacted = true; continue; }
        const rule = contract.properties[key];
        const value = input.properties[key];
        if ((rule.type === 'number' && typeof value === 'number' && Number.isFinite(value) && value >= rule.min && value <= rule.max && (!rule.integer || Number.isInteger(value))) ||
            (rule.type === 'enum' && rule.values.includes(value))) properties[key] = value;
        else redacted = true;
      }
    }
    const correlation = {};
    for (const [key, pattern] of Object.entries(correlations)) {
      if (!Object.hasOwn(input, key)) continue;
      const value = input[key];
      if (typeof value === 'string' && value.length <= 36 && pattern.test(value) && !/[\r\n]/u.test(value) && /[1-9a-f]/u.test(value)) correlation[key] = value;
      else redacted = true;
    }
    const timestamp = now().toISOString();
    return Object.freeze({
      schemaVersion: contract.schemaVersion, eventId: randomUUID(), eventCode,
      occurredAtUtc: timestamp, observedAtUtc: timestamp,
      ...origin, mode, sourceCategory, level: mapping.level, diagnostic, isFatal: mapping.isFatal,
      messageTemplate: contract.events[eventCode], message: contract.events[eventCode],
      properties: Object.freeze(properties), ...correlation,
      normalizationStatus: known && eventCode !== 'runtime.unclassified' ? 'normalized' : 'unclassified', redacted, truncated: false,
    });
  };
}

/** 生产输出必须使用此序列化入口，在 Docker 长行拆分之前限制 UTF-8 字节数。 */
export function serializeRuntimeLogEvent(event) {
  const json = JSON.stringify(event);
  if (Buffer.byteLength(json, 'utf8') > contract.maxEventBytes) {
    throw new Error('Runtime log event exceeds the transport byte budget.');
  }
  return json;
}
