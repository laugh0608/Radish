import { getIntlLocale } from '../locales/language.ts';

export type IntegerValue = string | number | bigint;

function parseConsoleDateValue(value: string): Date {
  const naturalDate = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(value.trim());
  if (naturalDate) {
    return new Date(
      Number(naturalDate[1]),
      Number(naturalDate[2]) - 1,
      Number(naturalDate[3]),
      12,
    );
  }

  return new Date(value);
}

export function formatConsoleDateTime(
  value: string | null | undefined,
  language?: string,
): string {
  if (!value) {
    return '-';
  }

  const parsed = parseConsoleDateValue(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(parsed);
}

export function formatConsoleDate(
  value: string | null | undefined,
  language?: string,
): string {
  if (!value) {
    return '-';
  }

  const parsed = parseConsoleDateValue(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(parsed);
}

export function formatConsoleMonthDay(
  value: string | null | undefined,
  language?: string,
): string {
  if (!value) {
    return '-';
  }

  const parsed = parseConsoleDateValue(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), {
    month: '2-digit',
    day: '2-digit',
  }).format(parsed);
}

export function formatConsoleWeekday(
  value: string | null | undefined,
  language?: string,
): string {
  if (!value) {
    return '-';
  }

  const parsed = parseConsoleDateValue(value);
  if (Number.isNaN(parsed.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), {
    weekday: 'short',
  }).format(parsed);
}

export function formatConsoleNumber(value: number, language?: string): string {
  return new Intl.NumberFormat(getIntlLocale(language)).format(value);
}

export function formatConsoleInteger(value: IntegerValue, language?: string): string {
  const normalized = String(value).trim();
  if (!/^-?\d+$/u.test(normalized)) {
    return normalized || '-';
  }

  return new Intl.NumberFormat(getIntlLocale(language), {
    maximumFractionDigits: 0,
  }).format(BigInt(normalized));
}

export function formatConsoleSignedInteger(value: IntegerValue, language?: string): string {
  const normalized = String(value).trim();
  if (!/^-?\d+$/u.test(normalized)) {
    return normalized || '-';
  }

  const integer = BigInt(normalized);
  const sign = integer > 0n ? '+' : '';
  return `${sign}${formatConsoleInteger(integer, language)}`;
}
