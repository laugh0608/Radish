import type { TFunction } from 'i18next';
import type { Product, ShopProductCapability } from '../../api/types';

interface ProductLike {
  voProductType?: string | number | null;
  voBenefitType?: string | number | null;
  voConsumableType?: string | number | null;
  voIsOnSale?: boolean;
}

const productTypeNumbers: Record<string, number> = {
  Benefit: 1,
  Consumable: 2,
  Physical: 99,
};

const benefitTypeNumbers: Record<string, number> = {
  Badge: 1,
  AvatarFrame: 2,
  Title: 3,
  Theme: 4,
  Signature: 5,
  NameColor: 6,
  LikeEffect: 7,
};

const consumableTypeNumbers: Record<string, number> = {
  RenameCard: 1,
  PostPinCard: 2,
  PostHighlightCard: 3,
  ExpCard: 4,
  CoinCard: 5,
  DoubleExpCard: 6,
  LotteryTicket: 99,
};

export function normalizeEnumNumber(value: unknown, mapping: Record<string, number>): number | undefined {
  const normalized = String(value ?? '').trim();
  if (!normalized) {
    return undefined;
  }

  if (/^\d+$/.test(normalized)) {
    return Number.parseInt(normalized, 10);
  }

  return mapping[normalized];
}

export function normalizeProductType(type?: string | number | null): string {
  switch (String(type ?? '')) {
    case '1':
      return 'Benefit';
    case '2':
      return 'Consumable';
    case '99':
      return 'Physical';
    default:
      return String(type ?? '');
  }
}

export function normalizeBenefitType(type?: string | number | null): string {
  switch (String(type ?? '')) {
    case '1':
      return 'Badge';
    case '2':
      return 'AvatarFrame';
    case '3':
      return 'Title';
    case '4':
      return 'Theme';
    case '5':
      return 'Signature';
    case '6':
      return 'NameColor';
    case '7':
      return 'LikeEffect';
    default:
      return String(type ?? '');
  }
}

export function normalizeConsumableType(type?: string | number | null): string {
  switch (String(type ?? '')) {
    case '1':
      return 'RenameCard';
    case '2':
      return 'PostPinCard';
    case '3':
      return 'PostHighlightCard';
    case '4':
      return 'ExpCard';
    case '5':
      return 'CoinCard';
    case '6':
      return 'DoubleExpCard';
    case '7':
    case '99':
      return 'LotteryTicket';
    default:
      return String(type ?? '');
  }
}

export function findProductCapability(
  capabilities: ShopProductCapability[],
  productType?: unknown,
  benefitType?: unknown,
  consumableType?: unknown,
): ShopProductCapability | undefined {
  const normalizedProductType = normalizeEnumNumber(productType, productTypeNumbers);
  const normalizedBenefitType = normalizeEnumNumber(benefitType, benefitTypeNumbers);
  const normalizedConsumableType = normalizeEnumNumber(consumableType, consumableTypeNumbers);

  return capabilities.find((capability) => {
    const capabilityProductType = normalizeEnumNumber(capability.voProductType, productTypeNumbers);
    const capabilityBenefitType = normalizeEnumNumber(capability.voBenefitType, benefitTypeNumbers);
    const capabilityConsumableType = normalizeEnumNumber(capability.voConsumableType, consumableTypeNumbers);

    return capabilityProductType === normalizedProductType
      && (normalizedProductType !== 1 || capabilityBenefitType === normalizedBenefitType)
      && (normalizedProductType !== 2 || capabilityConsumableType === normalizedConsumableType);
  });
}

export function getProductTypeDisplay(type: string | number | null | undefined, t: TFunction): string {
  switch (normalizeProductType(type)) {
    case 'Benefit':
      return t('products.type.benefit');
    case 'Consumable':
      return t('products.type.consumable');
    case 'Physical':
      return t('products.type.physical');
    default:
      return t('products.common.unknown');
  }
}

export function getBenefitTypeDisplay(type: string | number | null | undefined, t: TFunction): string {
  switch (normalizeBenefitType(type)) {
    case 'Badge':
      return t('products.benefitType.badge');
    case 'AvatarFrame':
      return t('products.benefitType.avatarFrame');
    case 'Title':
      return t('products.benefitType.title');
    case 'Theme':
      return t('products.benefitType.theme');
    case 'Signature':
      return t('products.benefitType.signature');
    case 'NameColor':
      return t('products.benefitType.nameColor');
    case 'LikeEffect':
      return t('products.benefitType.likeEffect');
    default:
      return '-';
  }
}

export function getConsumableTypeDisplay(type: string | number | null | undefined, t: TFunction): string {
  switch (normalizeConsumableType(type)) {
    case 'RenameCard':
      return t('products.consumableType.renameCard');
    case 'PostPinCard':
      return t('products.consumableType.postPinCard');
    case 'PostHighlightCard':
      return t('products.consumableType.postHighlightCard');
    case 'ExpCard':
      return t('products.consumableType.expCard');
    case 'CoinCard':
      return t('products.consumableType.coinCard');
    case 'DoubleExpCard':
      return t('products.consumableType.doubleExpCard');
    case 'LotteryTicket':
      return t('products.consumableType.lotteryTicket');
    default:
      return '-';
  }
}

function getCapabilityProductDisplay(capability: ShopProductCapability, t: TFunction): string {
  const productType = normalizeProductType(capability.voProductType);
  if (productType === 'Benefit') {
    return getBenefitTypeDisplay(capability.voBenefitType, t);
  }

  if (productType === 'Consumable') {
    return getConsumableTypeDisplay(capability.voConsumableType, t);
  }

  return getProductTypeDisplay(capability.voProductType, t);
}

export function getCapabilityDescription(
  capability: ShopProductCapability | undefined,
  t: TFunction,
): string {
  if (!capability) {
    return t('products.capability.metadataUnavailable');
  }

  const display = getCapabilityProductDisplay(capability, t);
  if (!capability.voCanSell) {
    const reasonKey = capability.voUnavailableReasonKey?.trim();
    if (reasonKey) {
      return t(reasonKey, { type: display });
    }

    return capability.voUnavailableReason || t('products.capability.unavailable.unknown');
  }

  const requirementKeys = capability.voConfigurationRequirementKeys ?? [];
  if (requirementKeys.length > 0) {
    return requirementKeys
      .map((key, index) => t(key, { defaultValue: capability.voConfigurationRequirements[index] ?? key }))
      .join(t('products.common.listSeparator'));
  }

  if (capability.voConfigurationRequirements.length > 0) {
    return capability.voConfigurationRequirements.join(t('products.common.listSeparator'));
  }

  return t('products.capability.available');
}

export function getUnsupportedSaleReason(
  product: ProductLike,
  capabilities: ShopProductCapability[],
  t: TFunction,
): string | null {
  const capability = findProductCapability(
    capabilities,
    product.voProductType,
    product.voBenefitType,
    product.voConsumableType,
  );

  return capability?.voCanSell === true ? null : getCapabilityDescription(capability, t);
}

export function getUnsupportedSaleStatusLabel(
  product: ProductLike,
  capabilities: ShopProductCapability[],
  t: TFunction,
): string | null {
  if (!getUnsupportedSaleReason(product, capabilities, t)) {
    return null;
  }

  return product.voIsOnSale
    ? t('products.status.historicalOnSale')
    : t('products.status.unavailable');
}

export function getProductConfigLabel(product: Product, t: TFunction): string {
  if (normalizeProductType(product.voProductType) === 'Benefit') {
    return t('products.detail.field.benefitType');
  }

  if (normalizeProductType(product.voProductType) === 'Consumable') {
    return t('products.detail.field.consumableType');
  }

  return t('products.detail.field.configuration');
}

export function getProductDurationDisplay(
  product: Product,
  t: TFunction,
  formatTime: (value: string) => string,
): string {
  switch (String(product.voDurationType ?? '')) {
    case '0':
    case 'Permanent':
      return t('products.duration.permanent');
    case '1':
    case 'Days':
      return typeof product.voDurationDays === 'number'
        ? t('products.duration.days', { count: product.voDurationDays })
        : t('products.duration.unknown');
    case '2':
    case 'FixedDate':
      return product.voExpiresAt
        ? t('products.duration.until', { time: formatTime(product.voExpiresAt) })
        : t('products.duration.unknown');
    default:
      return t('products.duration.unknown');
  }
}
