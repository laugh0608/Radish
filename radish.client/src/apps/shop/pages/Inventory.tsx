import { useState } from 'react';
import type { UserBenefit, UserInventoryItem } from '@/types/shop';
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
  const [activeTab, setActiveTab] = useState<TabType>('benefits');
  const [selectedItem, setSelectedItem] = useState<UserInventoryItem | null>(null);
  const [useQuantity, setUseQuantity] = useState(1);
  const [showUseModal, setShowUseModal] = useState(false);

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
          <p>加载中...</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <button className={styles.backButton} onClick={onBack}>
          ← 返回
        </button>
        <h1 className={styles.title}>我的背包</h1>
      </div>

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === 'benefits' ? styles.active : ''}`}
          onClick={() => setActiveTab('benefits')}
        >
          权益 ({benefits.length})
        </button>
        <button
          className={`${styles.tab} ${activeTab === 'consumables' ? styles.active : ''}`}
          onClick={() => setActiveTab('consumables')}
        >
          道具 ({inventory.length})
        </button>
      </div>

      <div className={styles.content}>
        {activeTab === 'benefits' ? (
          <div className={styles.benefitList}>
            {benefits.length === 0 ? (
              <div className={styles.empty}>
                <div className={styles.emptyIcon}>🎁</div>
                <p>暂无权益</p>
                <p className={styles.emptyHint}>去商城购买权益商品吧</p>
              </div>
            ) : (
              benefits.map((benefit) => (
                <div
                  key={benefit.voId}
                  className={`${styles.benefitCard} ${benefit.voIsExpired ? styles.expired : ''}`}
                >
                  <div className={styles.benefitIcon}>
                    {benefit.voBenefitIcon ? (
                      <img src={benefit.voBenefitIcon} alt={benefit.voBenefitName || ''} />
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
                        {benefit.voIsExpired ? '已过期' : benefit.voIsActive ? '已激活' : '未激活'}
                      </span>
                    </div>
                    <div className={styles.benefitType}>{benefit.voBenefitTypeDisplay ?? ''}</div>
                    <div className={styles.benefitMeta}>
                      <span>来源：{benefit.voSourceTypeDisplay ?? ''}</span>
                      <span>有效期：{benefit.voDurationDisplay ?? ''}</span>
                    </div>
                    {benefit.voExpiresAt && !benefit.voIsExpired && (
                      <div className={styles.benefitExpiry}>
                        到期时间：{formatTime(benefit.voExpiresAt)}
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
                          取消激活
                        </button>
                      ) : (
                        <button
                          className={styles.activateButton}
                          onClick={() => onActivateBenefit(benefit.voId)}
                        >
                          激活
                        </button>
                      )
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        ) : (
          <div className={styles.consumableList}>
            {inventory.length === 0 ? (
              <div className={styles.empty}>
                <div className={styles.emptyIcon}>📦</div>
                <p>暂无道具</p>
                <p className={styles.emptyHint}>去商城购买道具吧</p>
              </div>
            ) : (
              inventory.map((item) => (
                <div key={item.voId} className={styles.consumableCard}>
                  <div className={styles.consumableIcon}>
                    {item.voItemIcon ? (
                      <img src={item.voItemIcon} alt={item.voItemName || ''} />
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
                      数量：<span className={styles.quantity}>{item.voQuantity}</span>
                    </div>
                  </div>
                  <div className={styles.consumableActions}>
                    <button
                      className={styles.useButton}
                      onClick={() => handleUseItemClick(item)}
                      disabled={item.voQuantity <= 0}
                    >
                      使用
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </div>

      {showUseModal && selectedItem && (
        <div className={styles.modalOverlay} onClick={handleCloseModal}>
          <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
            <div className={styles.modalHeader}>
              <h3>使用道具</h3>
              <button className={styles.modalClose} onClick={handleCloseModal}>
                ✕
              </button>
            </div>
            <div className={styles.modalContent}>
              <div className={styles.modalItem}>
                <div className={styles.modalItemIcon}>
                  {selectedItem.voItemIcon ? (
                    <img src={selectedItem.voItemIcon} alt={selectedItem.voItemName || ''} />
                  ) : (
                    <span>{getConsumableTypeIcon(selectedItem.voConsumableType)}</span>
                  )}
                </div>
                <div className={styles.modalItemInfo}>
                  <div className={styles.modalItemName}>
                    {selectedItem.voItemName || selectedItem.voConsumableTypeDisplay}
                  </div>
                  <div className={styles.modalItemQuantity}>
                    可用数量：{selectedItem.voQuantity}
                  </div>
                </div>
              </div>
              <div className={styles.quantitySelector}>
                <label>使用数量：</label>
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
                取消
              </button>
              <button className={styles.confirmButton} onClick={handleConfirmUse}>
                确认使用
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
