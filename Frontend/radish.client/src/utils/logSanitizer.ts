export const REDACTED_LOG_VALUE = '[REDACTED]';

const CIRCULAR_LOG_VALUE = '[Circular]';

const SENSITIVE_LOG_FIELD_NAMES = [
  'password',
  'pwd',
  'passcode',
  'paymentPassword',
  'paymentPasscode',
  'currentPassword',
  'newPassword',
  'confirmPassword',
  'oldPassword',
  'accessToken',
  'refreshToken',
  'idToken',
  'token',
  'secret',
  'apiKey',
  'api_key',
] as const;

const SENSITIVE_LOG_FIELD_KEYS = new Set(SENSITIVE_LOG_FIELD_NAMES.map(normalizeLogFieldName));

function normalizeLogFieldName(fieldName: string): string {
  return fieldName.toLowerCase().replace(/[^a-z0-9]/g, '');
}

function isRecordLike(value: unknown): value is Record<string, unknown> {
  if (value === null || typeof value !== 'object') {
    return false;
  }

  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

export function isSensitiveLogField(fieldName: string): boolean {
  return SENSITIVE_LOG_FIELD_KEYS.has(normalizeLogFieldName(fieldName));
}

export function sanitizeLogValue(value: unknown, seen = new WeakSet<object>()): unknown {
  if (Array.isArray(value)) {
    return sanitizeLogArray(value, seen);
  }

  if (value instanceof Error) {
    return sanitizeLogError(value, seen);
  }

  if (isRecordLike(value)) {
    return sanitizeLogRecord(value, seen);
  }

  return value;
}

export function sanitizeLogArgs(args: unknown[]): unknown[] {
  const seen = new WeakSet<object>();
  return args.map((arg) => sanitizeLogValue(arg, seen));
}

function sanitizeLogArray(values: unknown[], seen: WeakSet<object>): unknown[] {
  if (seen.has(values)) {
    return [CIRCULAR_LOG_VALUE];
  }

  seen.add(values);
  const sanitized = values.map((item) => sanitizeLogValue(item, seen));
  seen.delete(values);
  return sanitized;
}

function sanitizeLogRecord(record: Record<string, unknown>, seen: WeakSet<object>): Record<string, unknown> | string {
  if (seen.has(record)) {
    return CIRCULAR_LOG_VALUE;
  }

  seen.add(record);

  const sanitized: Record<string, unknown> = {};
  for (const [key, entryValue] of Object.entries(record)) {
    sanitized[key] = isSensitiveLogField(key) ? REDACTED_LOG_VALUE : sanitizeLogValue(entryValue, seen);
  }

  seen.delete(record);
  return sanitized;
}

function sanitizeLogError(error: Error, seen: WeakSet<object>): Record<string, unknown> | string {
  if (seen.has(error)) {
    return CIRCULAR_LOG_VALUE;
  }

  seen.add(error);

  const sanitized: Record<string, unknown> = {
    name: error.name,
    message: error.message,
    stack: error.stack,
  };

  for (const [key, entryValue] of Object.entries(error)) {
    sanitized[key] = isSensitiveLogField(key) ? REDACTED_LOG_VALUE : sanitizeLogValue(entryValue, seen);
  }

  seen.delete(error);
  return sanitized;
}
