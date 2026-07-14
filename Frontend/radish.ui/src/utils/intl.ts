export type RadishIntlLocale = 'zh-CN' | 'en-US';

export function resolveIntlLocale(language: string | null | undefined): RadishIntlLocale {
  return language?.trim().toLowerCase().startsWith('en') ? 'en-US' : 'zh-CN';
}

export function formatLocalizedNumber(
  value: number,
  language: string | null | undefined,
  options?: Intl.NumberFormatOptions
): string {
  return new Intl.NumberFormat(resolveIntlLocale(language), options).format(value);
}

export function formatLocalizedDateTime(
  value: string | number | Date,
  language: string | null | undefined,
  options?: Intl.DateTimeFormatOptions
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat(resolveIntlLocale(language), options ?? {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(date);
}

export function formatLocalizedRelativeTime(
  value: string | number | Date,
  language: string | null | undefined,
  now: number = Date.now()
): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  const deltaSeconds = (date.getTime() - now) / 1000;
  const ranges: Array<{ unit: Intl.RelativeTimeFormatUnit; seconds: number }> = [
    { unit: 'year', seconds: 365 * 24 * 60 * 60 },
    { unit: 'month', seconds: 30 * 24 * 60 * 60 },
    { unit: 'week', seconds: 7 * 24 * 60 * 60 },
    { unit: 'day', seconds: 24 * 60 * 60 },
    { unit: 'hour', seconds: 60 * 60 },
    { unit: 'minute', seconds: 60 },
    { unit: 'second', seconds: 1 },
  ];
  const range = ranges.find((candidate) => Math.abs(deltaSeconds) >= candidate.seconds)
    ?? ranges[ranges.length - 1];
  const amount = Math.round(deltaSeconds / range.seconds);

  return new Intl.RelativeTimeFormat(resolveIntlLocale(language), { numeric: 'auto' })
    .format(amount, range.unit);
}
