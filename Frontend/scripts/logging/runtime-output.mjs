import { writeSync } from 'node:fs';
import { hostname } from 'node:os';
import { performance } from 'node:perf_hooks';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from './runtime-event.mjs';

function writeLine(descriptor, line) {
  const buffer = Buffer.from(line, 'utf8');
  let offset = 0;
  while (offset < buffer.length) {
    const count = writeSync(descriptor, buffer, offset, buffer.length - offset);
    if (count <= 0) throw new Error('Log output made no progress.');
    offset += count;
  }
}

/** 同步单行输出避免应用层无界队列；底层流失败不递归记录原始异常。 */
export function createRuntimeLogOutput(source, options = {}, {
  write = (line) => writeLine(1, line),
  emergency = (line) => writeLine(2, line),
  monotonicNow = () => performance.now(),
} = {}) {
  const create = createRuntimeLogPolicy(source, options);
  const emergencyEvent = createRuntimeLogPolicy(source, { mode: options.mode ?? 'Production', minimumLevel: 'Error' });
  let lastEmergency = -Infinity;
  let failureCount = 0;
  let pendingFailures = 0;
  function reportFailure() {
    failureCount++;
    pendingFailures++;
    const now = monotonicNow();
    if (now - lastEmergency < 60000) return;
    lastEmergency = now;
    try {
      emergency(serializeRuntimeLogEvent(emergencyEvent({ eventCode: 'pipeline.output_failed', level: 'Error',
        sourceCategory: 'pipeline', properties: { count: Math.min(pendingFailures, Number.MAX_SAFE_INTEGER) } })) + '\n');
      pendingFailures = 0;
    } catch { /* 应急通道也可能关闭，只保留计数，不递归。 */ }
  }
  return {
    get failureCount() { return failureCount; },
    reportFailure,
    write(input) {
      try {
        const event = create(input);
        if (event) write(serializeRuntimeLogEvent(event) + '\n');
      } catch { reportFailure(); }
    },
  };
}

function boolean(value, fallback = false) {
  if (value === undefined) return fallback;
  if (value.trim().toLowerCase() === 'true') return true;
  if (value.trim().toLowerCase() === 'false') return false;
  throw new Error('Invalid runtime logging boolean configuration.');
}

/** 服务端环境变量沿用 .NET 的双下划线配置映射；镜像标签不决定模式。 */
export function createFrontendLogger(env = process.env, destination) {
  const enabled = boolean(env.RadishLogging__Enabled);
  if (!enabled) {
    // 迁移期间保留原终端目的地，但调用点已经只传固定事件码和受控元数据。
    const legacy = (descriptor, code) => {
      try { writeLine(descriptor, `[frontend] ${code}\n`); } catch { /* 旧终端关闭也不能破坏请求。 */ }
    };
    return { info: (code) => legacy(1, code), warn: (code) => legacy(2, code), error: (code) => legacy(2, code) };
  }
  const mode = env.RadishLogging__Mode ?? 'Production';
  if (mode === 'Development' && env.NODE_ENV !== 'development') throw new Error('Development logging requires a development host.');
  const output = createRuntimeLogOutput({ service: 'frontend', deploymentId: env.RadishLogging__DeploymentId ?? 'local',
    instanceId: env.RadishLogging__InstanceId ?? hostname(), release: env.RadishLogging__Release ?? 'unversioned' },
  { mode, minimumLevel: env.RadishLogging__MinimumLevel ?? 'Info', diagnostics: boolean(env.RadishLogging__Diagnostics) }, destination);
  const emit = (level, code, properties) => output.write({ eventCode: code, level,
    sourceCategory: code.startsWith('runtime.') ? 'lifecycle' : 'http', properties });
  return { info: (code, properties = {}) => emit('Info', code, properties),
    warn: (code, properties = {}) => emit('Warning', code, properties),
    error: (code, properties = {}) => emit('Error', code, properties) };
}
