import { useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { LongId } from '@/api/user';
import type { UserBenefit, UserInventoryItem } from '@/types/shop';
import { WebStateSlot } from '@/components/web-shell';
import { resolveMediaUrl } from '@/utils/media';
import styles from './Inventory.module.css';

interface InventoryProps {
  benefits: UserBenefit[];
  inventory: UserInventoryItem[];
  loading: boolean;
  onActivateBenefit: (benefitId: LongId) => void;
  onDeactivateBenefit: (benefitId: LongId) => void;
  onUseItem: (inventoryId: LongId, quantity?: number, targetId?: LongId) => Promise<boolean>;
  onUseRenameCard: (inventoryId: LongId, newNickname: string) => Promise<boolean>;
  backHref?: string;
  getSourceOrderHref?: (orderId: LongId) => string;
  getSourceProductHref?: (productId: LongId) => string;
  onSourceOrderClick: (orderId: LongId) => void;
  onSourceProductClick: (productId: LongId) => void;
  onBack: () => void;
}

type TabType = 'benefits' | 'consumables';

const normalizeBenefitType = (type?: string | null): string => {
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
};

const getBenefitTypeIcon = (type: string): string => {
  const icons: Record<string, string> = {
    'Badge': '🏅',
    'AvatarFrame': '🖼️',
    'Title': '🎖️',
    'Theme': '🎨',
    'Signature': '✍️',
    'NameColor': '🌈',
    'LikeEffect': '❤️'
  };
  return icons[normalizeBenefitType(type)] || '🎁';
};

const getConsumableTypeIcon = (type: string): string => {
  const icons: Record<string, string> = {
    'RenameCard': '📝',
    'PostPinCard': '📌',
    'PostHighlightCard': '✨',
    'ExpCard': '⭐',
    'CoinCard': '🥕',
    'DoubleExpCard': '🚀',
    'LotteryTicket': '🎫'
  };
  return icons[normalizeConsumableType(type)] || '📦';
};

const normalizeConsumableType = (type?: string | null): string => {
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
};

const isRenameCardItem = (item?: UserInventoryItem | null): boolean =>
  normalizeConsumableType(item?.voConsumableType) === 'RenameCard';

const isUnavailableBenefitItem = (benefit?: UserBenefit | null): boolean => {
  const normalizedType = normalizeBenefitType(benefit?.voBenefitType);
  return normalizedType === 'Badge'
    || normalizedType === 'AvatarFrame'
    || normalizedType === 'Title'
    || normalizedType === 'Theme'
    || normalizedType === 'Signature'
    || normalizedType === 'NameColor'
    || normalizedType === 'LikeEffect';
};

const isUnavailableConsumableItem = (item?: UserInventoryItem | null): boolean => {
  const normalizedType = normalizeConsumableType(item?.voConsumableType);
  return normalizedType === 'PostPinCard'
    || normalizedType === 'PostHighlightCard'
    || normalizedType === 'DoubleExpCard'
    || normalizedType === 'LotteryTicket';
};

const isPositiveLongId = (value?: LongId | null): value is LongId => {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0;
  }

  return typeof value === 'string' && /^[1-9]\d*$/.test(value.trim());
};

const isPurchaseSource = (benefit: UserBenefit): boolean =>
  benefit.voSourceType?.trim().toLowerCase() === 'purchase';

function shouldHandleInventoryLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handleInventoryLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandleInventoryLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

export const Inventory = ({
  benefits,
  inventory,
  loading,
  onActivateBenefit,
  onDeactivateBenefit,
  onUseItem,
  onUseRenameCard,
  backHref,
  getSourceOrderHref,
  getSourceProductHref,
  onSourceOrderClick,
  onSourceProductClick,
  onBack
}: InventoryProps) => {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<TabType>('benefits');
  const [selectedItem, setSelectedItem] = useState<UserInventoryItem | null>(null);
  const [useQuantity, setUseQuantity] = useState(1);
  const [showUseModal, setShowUseModal] = useState(false);
  const [renameValue, setRenameValue] = useState('');
  const selectedItemIconUrl = resolveMediaUrl(selectedItem?.voItemIcon);

  const formatTime = (timeStr?: string | null) => {
    if (!timeStr) return '-';
    return new Date(timeStr).toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const handleUseItemClick = (item: UserInventoryItem) => {
    setSelectedItem(item);
    setUseQuantity(1);
    setRenameValue('');
    setShowUseModal(true);
  };

  const handleConfirmUse = async () => {
    if (!selectedItem) {
      return;
    }

    const success = isRenameCardItem(selectedItem)
      ? await onUseRenameCard(selectedItem.voId, renameValue.trim())
      : await onUseItem(selectedItem.voId, useQuantity);

    if (!success) {
      return;
    }

    setShowUseModal(false);
    setSelectedItem(null);
    setRenameValue('');
  };

  const handleCloseModal = () => {
    setShowUseModal(false);
    setSelectedItem(null);
    setUseQuantity(1);
    setRenameValue('');
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <WebStateSlot
          tone="loading"
          title={t('shop.loading')}
          description={t('shop.inventory.title')}
        />
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        {backHref ? (
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handleInventoryLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={17} />
            <span>{t('shop.back')}</span>
          </a>
        ) : (
          <button type="button" className={styles.backButton} onClick={onBack}>
            <Icon icon="mdi:arrow-left" size={17} />
            <span>{t('shop.back')}</span>
          </button>
        )}
        <div className={styles.headerCopy}>
          <p className={styles.kicker}>{t('shop.title')}</p>
          <h1 className={styles.title}>{t('shop.inventory.title')}</h1>
          <p className={styles.description}>{t('shop.inventory.emptyBenefitsHint')}</p>
        </div>
      </div>

      <div className={styles.summaryGrid}>
        <article className={styles.summaryCard}>
          <span>{t('shop.inventory.tabBenefits', { count: benefits.length })}</span>
          <strong>{benefits.length}</strong>
        </article>
        <article className={styles.summaryCard}>
          <span>{t('shop.inventory.tabItems', { count: inventory.length })}</span>
          <strong>{inventory.length}</strong>
        </article>
      </div>

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === 'benefits' ? styles.active : ''}`}
          onClick={() => setActiveTab('benefits')}
        >
          {t('shop.inventory.tabBenefits', { count: benefits.length })}
        </button>
        <button
          className={`${styles.tab} ${activeTab === 'consumables' ? styles.active : ''}`}
          onClick={() => setActiveTab('consumables')}
        >
          {t('shop.inventory.tabItems', { count: inventory.length })}
        </button>
      </div>

      <div className={styles.content}>
        {activeTab === 'benefits' ? (
          <div className={styles.benefitList}>
            {benefits.length === 0 ? (
              <WebStateSlot
                tone="empty"
                icon="mdi:gift-outline"
                title={t('shop.inventory.emptyBenefits')}
                description={t('shop.inventory.emptyBenefitsHint')}
              />
            ) : (
              benefits.map((benefit) => {
                const benefitIconUrl = resolveMediaUrl(benefit.voBenefitIcon);
                const sourceOrderId = isPurchaseSource(benefit) && isPositiveLongId(benefit.voSourceOrderId)
                  ? benefit.voSourceOrderId
                  : undefined;
                const sourceProductId = isPurchaseSource(benefit) && isPositiveLongId(benefit.voSourceProductId)
                  ? benefit.voSourceProductId
                  : undefined;
                const sourceOrderHref = sourceOrderId ? getSourceOrderHref?.(sourceOrderId) : undefined;
                const sourceProductHref = sourceProductId ? getSourceProductHref?.(sourceProductId) : undefined;

                return (
                  <div
                    key={benefit.voId}
                    className={`${styles.benefitCard} ${benefit.voIsExpired ? styles.expired : ''}`}
                  >
                    <div className={styles.benefitIcon}>
                      {benefitIconUrl ? (
                        <img src={benefitIconUrl} alt={benefit.voBenefitName || ''} />
                      ) : (
                        <span>{getBenefitTypeIcon(benefit.voBenefitType)}</span>
                      )}
                    </div>
                    <div className={styles.benefitInfo}>
                      <div className={styles.benefitHeader}>
                        <span className={styles.benefitName}>
                          {benefit.voBenefitName || benefit.voBenefitTypeDisplay}
                        </span>
                        <span className={`${styles.benefitStatus} ${benefit.voIsActive ? styles.active : ''}`}>
                          {benefit.voIsExpired
                            ? t('shop.inventory.status.expired')
                            : benefit.voIsActive
                              ? t('shop.inventory.status.active')
                              : t('shop.inventory.status.inactive')}
                        </span>
                      </div>
                      <div className={styles.benefitType}>{benefit.voBenefitTypeDisplay ?? ''}</div>
                      <div className={styles.benefitMeta}>
                        <span>{t('shop.inventory.source', { value: benefit.voSourceTypeDisplay ?? '' })}</span>
                        <span>{t('shop.inventory.duration', { value: benefit.voDurationDisplay ?? '' })}</span>
                      </div>
                      {benefit.voExpiresAt && !benefit.voIsExpired && (
                        <div className={styles.benefitExpiry}>
                          {t('shop.inventory.expireAt', { value: formatTime(benefit.voExpiresAt) })}
                        </div>
                      )}
                      {(sourceOrderId || sourceProductId) && (
                        <div className={styles.benefitSourceActions}>
                          {sourceOrderId && (
                            sourceOrderHref ? (
                              <a
                                className={styles.sourceButton}
                                href={sourceOrderHref}
                                onClick={(event) => handleInventoryLinkClick(event, () => onSourceOrderClick(sourceOrderId))}
                              >
                                {t('shop.inventory.viewSourceOrder')}
                              </a>
                            ) : (
                              <button
                                type="button"
                                className={styles.sourceButton}
                                onClick={() => onSourceOrderClick(sourceOrderId)}
                              >
                                {t('shop.inventory.viewSourceOrder')}
                              </button>
                            )
                          )}
                          {sourceProductId && (
                            sourceProductHref ? (
                              <a
                                className={styles.sourceButton}
                                href={sourceProductHref}
                                onClick={(event) => handleInventoryLinkClick(event, () => onSourceProductClick(sourceProductId))}
                              >
                                {t('shop.inventory.viewSourceProduct')}
                              </a>
                            ) : (
                              <button
                                type="button"
                                className={styles.sourceButton}
                                onClick={() => onSourceProductClick(sourceProductId)}
                              >
                                {t('shop.inventory.viewSourceProduct')}
                              </button>
                            )
                          )}
                        </div>
                      )}
                    </div>
                    <div className={styles.benefitActions}>
                      {!benefit.voIsExpired && (
                        benefit.voIsActive ? (
                          <button
                            className={styles.deactivateButton}
                            onClick={() => onDeactivateBenefit(benefit.voId)}
                          >
                            {t('shop.inventory.deactivate')}
                          </button>
                        ) : isUnavailableBenefitItem(benefit) ? (
                          <button
                            className={styles.activateButton}
                            disabled
                          >
                            {t('shop.inventory.unavailable')}
                          </button>
                        ) : (
                          <button
                            className={styles.activateButton}
                            onClick={() => onActivateBenefit(benefit.voId)}
                          >
                            {t('shop.inventory.activate')}
                          </button>
                        )
                      )}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        ) : (
          <div className={styles.consumableList}>
            {inventory.length === 0 ? (
              <WebStateSlot
                tone="empty"
                icon="mdi:package-variant-closed"
                title={t('shop.inventory.emptyItems')}
                description={t('shop.inventory.emptyItemsHint')}
              />
            ) : (
              inventory.map((item) => {
                const itemIconUrl = resolveMediaUrl(item.voItemIcon);
                const sourceProductId = isPositiveLongId(item.voSourceProductId)
                  ? item.voSourceProductId
                  : undefined;
                const sourceProductHref = sourceProductId ? getSourceProductHref?.(sourceProductId) : undefined;

                return (
                  <div key={item.voId} className={styles.consumableCard}>
                    <div className={styles.consumableIcon}>
                      {itemIconUrl ? (
                        <img src={itemIconUrl} alt={item.voItemName || ''} />
                      ) : (
                        <span>{getConsumableTypeIcon(item.voConsumableType)}</span>
                      )}
                    </div>
                    <div className={styles.consumableInfo}>
                      <div className={styles.consumableName}>
                        {item.voItemName || item.voConsumableTypeDisplay}
                      </div>
                      <div className={styles.consumableType}>{item.voConsumableTypeDisplay ?? ''}</div>
                      <div className={styles.consumableQuantity}>
                        {t('shop.inventory.quantity')}<span className={styles.quantity}>{item.voQuantity}</span>
                      </div>
                      {sourceProductId && (
                        <div className={styles.benefitSourceActions}>
                          {sourceProductHref ? (
                            <a
                              className={styles.sourceButton}
                              href={sourceProductHref}
                              onClick={(event) => handleInventoryLinkClick(event, () => onSourceProductClick(sourceProductId))}
                            >
                              {t('shop.inventory.viewRelatedProduct')}
                            </a>
                          ) : (
                            <button
                              type="button"
                              className={styles.sourceButton}
                              onClick={() => onSourceProductClick(sourceProductId)}
                            >
                              {t('shop.inventory.viewRelatedProduct')}
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                    <div className={styles.consumableActions}>
                      <button
                        className={styles.useButton}
                        onClick={() => handleUseItemClick(item)}
                        disabled={item.voQuantity <= 0 || isUnavailableConsumableItem(item)}
                      >
                        {isUnavailableConsumableItem(item)
                          ? t('shop.inventory.unavailable')
                          : t('shop.inventory.use')}
                      </button>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        )}
      </div>

      {showUseModal && selectedItem && (
        <div className={styles.modalOverlay} onClick={handleCloseModal}>
          <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
            <div className={styles.modalHeader}>
              <h3>{t(isRenameCardItem(selectedItem) ? 'shop.inventory.renameTitle' : 'shop.inventory.useTitle')}</h3>
              <button className={styles.modalClose} onClick={handleCloseModal}>
                ✕
              </button>
            </div>
            <div className={styles.modalContent}>
              <div className={styles.modalItem}>
                <div className={styles.modalItemIcon}>
                  {selectedItemIconUrl ? (
                    <img src={selectedItemIconUrl} alt={selectedItem.voItemName || ''} />
                  ) : (
                    <span>{getConsumableTypeIcon(selectedItem.voConsumableType)}</span>
                  )}
                </div>
                <div className={styles.modalItemInfo}>
                  <div className={styles.modalItemName}>
                    {selectedItem.voItemName || selectedItem.voConsumableTypeDisplay}
                  </div>
                  <div className={styles.modalItemQuantity}>
                    {t('shop.inventory.availableQuantity', { count: selectedItem.voQuantity })}
                  </div>
                </div>
              </div>
              {isRenameCardItem(selectedItem) ? (
                <div className={styles.formField}>
                  <label htmlFor="shop-rename-input">{t('shop.inventory.renameInputLabel')}</label>
                  <input
                    id="shop-rename-input"
                    className={styles.textInput}
                    value={renameValue}
                    onChange={(e) => setRenameValue(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && renameValue.trim()) {
                        void handleConfirmUse();
                      }
                    }}
                    placeholder={t('shop.inventory.renamePlaceholder')}
                    autoFocus
                  />
                  <p className={styles.fieldHint}>{t('shop.inventory.renameHint')}</p>
                </div>
              ) : (
                <div className={styles.quantitySelector}>
                  <label>{t('shop.inventory.useQuantity')}</label>
                  <div className={styles.quantityControls}>
                    <button
                      onClick={() => setUseQuantity(Math.max(1, useQuantity - 1))}
                      disabled={useQuantity <= 1}
                    >
                      -
                    </button>
                    <span>{useQuantity}</span>
                    <button
                      onClick={() => setUseQuantity(Math.min(selectedItem.voQuantity, useQuantity + 1))}
                      disabled={useQuantity >= selectedItem.voQuantity}
                    >
                      +
                    </button>
                  </div>
                </div>
              )}
            </div>
            <div className={styles.modalFooter}>
              <button className={styles.cancelButton} onClick={handleCloseModal}>
                {t('common.cancel')}
              </button>
              <button
                className={styles.confirmButton}
                onClick={() => { void handleConfirmUse(); }}
                disabled={isRenameCardItem(selectedItem) && !renameValue.trim()}
              >
                {t('shop.inventory.confirmUse')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
