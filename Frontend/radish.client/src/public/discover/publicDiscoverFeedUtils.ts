import type { WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { getProductTypeDisplay, type ProductListItem } from '@/api/shop';

export function formatDocumentMeta(document: WikiDocumentVo, locale: string, fallback: string): string {
  const source = document.voPublishedAt || document.voModifyTime || document.voCreateTime;
  if (!source) {
    return fallback;
  }

  const parsed = new Date(source);
  if (Number.isNaN(parsed.getTime())) {
    return fallback;
  }

  return parsed.toLocaleDateString(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
}

export function isRecentDocument(document: WikiDocumentVo): boolean {
  const source = document.voPublishedAt || document.voModifyTime || document.voCreateTime;
  if (!source) {
    return false;
  }

  const parsed = new Date(source);
  if (Number.isNaN(parsed.getTime())) {
    return false;
  }

  const ageMs = Date.now() - parsed.getTime();
  return ageMs >= 0 && ageMs <= 1000 * 60 * 60 * 24 * 30;
}

export function buildDocumentSummary(
  document: WikiDocumentVo,
  locale: string,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
  const summary = document.voSummary?.trim();
  if (summary) {
    return summary;
  }

  const source = document.voPublishedAt || document.voModifyTime || document.voCreateTime;
  if (source) {
    const parsed = new Date(source);
    if (!Number.isNaN(parsed.getTime())) {
      const formattedDate = parsed.toLocaleDateString(locale, {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
      });
      return t('discover.public.documentFallbackSummaryWithDate', { date: formattedDate });
    }
  }

  return t('discover.public.documentFallbackSummary');
}

export function buildProductSummary(
  product: ProductListItem,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
  const productType = getProductTypeDisplay(product.voProductType);
  const duration = product.voDurationDisplay?.trim();
  const soldCount = product.voSoldCount ?? 0;

  if (duration) {
    return t('discover.public.productDurationSummary', { type: productType, duration });
  }

  if (product.voHasDiscount) {
    return t('discover.public.productDiscountSummary', { type: productType });
  }

  if (soldCount > 0) {
    return t('discover.public.productSoldSummary', { type: productType, count: soldCount });
  }

  return t('discover.public.productFallbackSummary');
}
