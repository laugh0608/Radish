import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { UserBenefit, UserInventoryItem } from '@/types/shop';
import { resolveMediaUrl } from '@/utils/media';
import styles from './Inventory.module.css';

interface InventoryProps {
  benefits: UserBenefit[];
  inventory: UserInventoryItem[];
  loading: boolean;
  onActivateBenefit: (benefitId: number) => void;
  onDeactivateBenefit: (benefitId: number) => void;
  onUseItem: (inventoryId: number, quantity?: number, targetId?: number) => void;
  onBack: () => void;
}

type TabType = 'benefits' | 'consumables';

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
  return icons[type] || '🎁';
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
  return icons[type] || '📦';
};

export const Inventory = ({
  benefits,
  inventory,
  loading,
  onActivateBenefit,
  onDeactivateBenefit,
  onUseItem,
  onBack
}: InventoryProps) => {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<TabType>('benefits');
  const [selectedItem, setSelectedItem] = useState<UserInventoryItem | null>(null);
  const [useQuantity, setUseQuantity] = useState(1);
  const [showUseModal, setShowUseModal] = useState(false);
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
    setShowUseModal(true);
  };

  const handleConfirmUse = () => {
    if (selectedItem) {
      onUseItem(selectedItem.voId, useQuantity);
      setShowUseModal(false);
      setSelectedItem(null);
    }
  };

  const handleCloseModal = () => {
    setShowUseModal(false);
    setSelectedItem(null);
    setUseQuantity(1);
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.spinner}></div>
          <p>{t('shop.loading')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← {t('shop.back')}
        </button>
        <h1 className={styles.title}>{t('shop.inventory.title')}</h1>
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
              <div className={styles.empty}>
                <div className={styles.emptyIcon}>🎁</div>
                <p>{t('shop.inventory.emptyBenefits')}</p>
                <p className={styles.emptyHint}>{t('shop.inventory.emptyBenefitsHint')}</p>
              </div>
            ) : (
              benefits.map((benefit) => {
                const benefitIconUrl = resolveMediaUrl(benefit.voBenefitIcon);

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
              <div className={styles.empty}>
                <div className={styles.emptyIcon}>📦</div>
                <p>{t('shop.inventory.emptyItems')}</p>
                <p className={styles.emptyHint}>{t('shop.inventory.emptyItemsHint')}</p>
              </div>
            ) : (
              inventory.map((item) => {
                const itemIconUrl = resolveMediaUrl(item.voItemIcon);

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
                    </div>
                    <div className={styles.consumableActions}>
                      <button
                        className={styles.useButton}
                        onClick={() => handleUseItemClick(item)}
                        disabled={item.voQuantity <= 0}
                      >
                        {t('shop.inventory.use')}
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
              <h3>{t('shop.inventory.useTitle')}</h3>
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
            </div>
            <div className={styles.modalFooter}>
              <button className={styles.cancelButton} onClick={handleCloseModal}>
                {t('common.cancel')}
              </button>
              <button className={styles.confirmButton} onClick={handleConfirmUse}>
                {t('shop.inventory.confirmUse')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
