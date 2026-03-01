export const DEFAULT_TIME_ZONE = 'Asia/Shanghai';
export const DEFAULT_TIME_FORMAT = 'yyyy-MM-dd HH:mm:ss';

/**
 * 校验时区 ID 是否有效
 */
export function isValidTimeZoneId(timeZoneId: string): boolean {
  if (!timeZoneId || !timeZoneId.trim()) {
    return false;
  }

  try {
    new Intl.DateTimeFormat('zh-CN', { timeZone: timeZoneId });
    return true;
  } catch {
    return false;
  }
}

/**
 * 解析有效时区，失败回退到 fallback
 */
export function resolveTimeZoneId(timeZoneId: string | null | undefined, fallback: string = DEFAULT_TIME_ZONE): string {
  if (timeZoneId && isValidTimeZoneId(timeZoneId)) {
    return timeZoneId;
  }

  if (isValidTimeZoneId(fallback)) {
    return fallback;
  }

  return DEFAULT_TIME_ZONE;
}

/**
 * 获取浏览器时区（无效时返回 fallback）
 */
export function getBrowserTimeZoneId(fallback: string = DEFAULT_TIME_ZONE): string {
  if (typeof window === 'undefined') {
    return resolveTimeZoneId(null, fallback);
  }

  const browserTimeZone = window.Intl.DateTimeFormat().resolvedOptions().timeZone;
  return resolveTimeZoneId(browserTimeZone, fallback);
}

/**
 * 格式化为 yyyy-MM-dd HH:mm:ss
 */
export function formatDateTimeByTimeZone(
  value: string | Date | number | null | undefined,
  timeZoneId: string,
  fallback: string = '-'
): string {
  if (value === null || value === undefined || value === '') {
    return fallback;
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  const safeTimeZone = resolveTimeZoneId(timeZoneId);
  const formatter = new Intl.DateTimeFormat('zh-CN', {
    timeZone: safeTimeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
    hourCycle: 'h23'
  });

  const parts = formatter.formatToParts(date);
  const partMap: Record<string, string> = {};
  parts.forEach((part) => {
    if (part.type !== 'literal') {
      partMap[part.type] = part.value;
    }
  });

  return `${partMap.year}-${partMap.month}-${partMap.day} ${partMap.hour}:${partMap.minute}:${partMap.second}`;
}

export interface TimeZoneOption {
  value: string;
  label: string;
}

/**
 * 生成时区下拉选项（含系统默认、浏览器时区和常用时区）
 */
export function buildTimeZoneOptions(
  systemTimeZoneId: string,
  currentTimeZoneId?: string | null
): TimeZoneOption[] {
  const browserTimeZoneId = getBrowserTimeZoneId(systemTimeZoneId);
  const normalizedSystemTimeZone = resolveTimeZoneId(systemTimeZoneId, DEFAULT_TIME_ZONE);
  const supportedTimeZones = typeof Intl !== 'undefined' && typeof Intl.supportedValuesOf === 'function'
    ? Intl.supportedValuesOf('timeZone')
    : [];

  const candidates = [
    currentTimeZoneId,
    normalizedSystemTimeZone,
    browserTimeZoneId,
    'UTC',
    'Asia/Shanghai',
    'Asia/Tokyo',
    'Asia/Singapore',
    'Europe/London',
    'Europe/Berlin',
    'America/New_York',
    'America/Los_Angeles',
    ...supportedTimeZones
  ]
    .filter((item): item is string => !!item)
    .map(item => resolveTimeZoneId(item, normalizedSystemTimeZone));

  return Array.from(new Set(candidates)).map((timeZoneId) => {
    if (timeZoneId === normalizedSystemTimeZone) {
      return { value: timeZoneId, label: `系统默认 (${timeZoneId})` };
    }

    if (timeZoneId === browserTimeZoneId) {
      return { value: timeZoneId, label: `浏览器时区 (${timeZoneId})` };
    }

    return { value: timeZoneId, label: timeZoneId };
  });
}
