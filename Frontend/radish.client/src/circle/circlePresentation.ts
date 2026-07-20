import { getIntlLocale } from '../locales/language.ts';
import { formatDateTimeByTimeZone } from '../utils/dateTime.ts';

export function formatCircleNumber(value: number, language?: string): string {
  return new Intl.NumberFormat(getIntlLocale(language)).format(value);
}

export function formatCircleDateTime(
  value: string | Date | number | null | undefined,
  timeZoneId: string,
  language?: string,
  fallback: string = '-',
): string {
  return formatDateTimeByTimeZone(value, timeZoneId, fallback, getIntlLocale(language));
}
