import type { Product } from '../../api/types';

interface ProductLike {
  voProductType?: string | number | null;
  voBenefitType?: string | number | null;
  voConsumableType?: string | number | null;
  voIsOnSale?: boolean;
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

export function getProductTypeDisplay(type?: string | number | null): string {
  switch (normalizeProductType(type)) {
    case 'Benefit':
      return '权益';
    case 'Consumable':
      return '消耗品';
    case 'Physical':
      return '实物';
    default:
      return '未知';
  }
}

export function getBenefitTypeDisplay(type?: string | number | null): string {
  switch (normalizeBenefitType(type)) {
    case 'Badge':
      return '徽章';
    case 'AvatarFrame':
      return '头像框';
    case 'Title':
      return '称号';
    case 'Theme':
      return '主题';
    case 'Signature':
      return '签名档';
    case 'NameColor':
      return '用户名颜色';
    case 'LikeEffect':
      return '点赞特效';
    default:
      return '-';
  }
}

export function getConsumableTypeDisplay(type?: string | number | null): string {
  switch (normalizeConsumableType(type)) {
    case 'RenameCard':
      return '改名卡';
    case 'PostPinCard':
      return '置顶卡';
    case 'PostHighlightCard':
      return '高亮卡';
    case 'ExpCard':
      return '经验卡';
    case 'CoinCard':
      return '萝卜币红包';
    case 'DoubleExpCard':
      return '双倍经验卡';
    case 'LotteryTicket':
      return '抽奖券';
    default:
      return '-';
  }
}

export function getUnsupportedSaleReason(product: ProductLike): string | null {
  const productType = normalizeProductType(product.voProductType);
  if (productType === 'Benefit') {
    const benefitType = normalizeBenefitType(product.voBenefitType);
    if (
      benefitType === 'Badge'
      || benefitType === 'AvatarFrame'
      || benefitType === 'Title'
      || benefitType === 'Theme'
      || benefitType === 'Signature'
      || benefitType === 'NameColor'
      || benefitType === 'LikeEffect'
    ) {
      return '当前权益效果未开放，不能上架销售';
    }
  }

  if (productType === 'Consumable') {
    const consumableType = normalizeConsumableType(product.voConsumableType);
    if (
      consumableType === 'PostPinCard'
      || consumableType === 'PostHighlightCard'
      || consumableType === 'DoubleExpCard'
      || consumableType === 'LotteryTicket'
    ) {
      return '当前道具未开放，不能上架销售';
    }
  }

  return null;
}

export function getUnsupportedSaleStatusLabel(product: ProductLike): string | null {
  if (!getUnsupportedSaleReason(product)) {
    return null;
  }

  return product.voIsOnSale ? '历史上架' : '未开放';
}

export function getProductConfigLabel(product: Product): string {
  if (normalizeProductType(product.voProductType) === 'Benefit') {
    return getBenefitTypeDisplay(product.voBenefitType);
  }

  if (normalizeProductType(product.voProductType) === 'Consumable') {
    return getConsumableTypeDisplay(product.voConsumableType);
  }

  return '商品配置';
}
