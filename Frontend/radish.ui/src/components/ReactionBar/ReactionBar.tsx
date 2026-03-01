import { useEffect, useMemo, useRef, useState } from 'react';
import {
  StickerPicker,
  type StickerPickerGroup,
  type StickerPickerSelection,
} from '../StickerPicker/StickerPicker';
import styles from './ReactionBar.module.css';

const QUICK_EMOJIS = ['😀', '😂', '❤️', '👍', '😢', '😮'];
const MAX_VISIBLE_BUBBLES = 8;
const MAX_USER_REACTIONS = 10;

export type ReactionTargetType = 'Post' | 'Comment' | 'ChatMessage';

export interface ReactionSummaryItem {
  voEmojiType: 'unicode' | 'sticker';
  voEmojiValue: string;
  voCount: number;
  voIsReacted: boolean;
  voThumbnailUrl?: string | null;
}

export interface ReactionTogglePayload {
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;
}

export interface ReactionBarProps {
  targetType: ReactionTargetType;
  targetId: number;
  items: ReactionSummaryItem[];
  isLoggedIn: boolean;
  loading?: boolean;
  stickerGroups?: StickerPickerGroup[];
  className?: string;
  onToggle: (payload: ReactionTogglePayload) => Promise<void>;
  onRequireLogin?: () => void;
}

const buildKey = (item: Pick<ReactionSummaryItem, 'voEmojiType' | 'voEmojiValue'>): string =>
  `${item.voEmojiType}:${item.voEmojiValue}`;

const parseStickerCode = (emojiValue: string): string => {
  const parts = emojiValue.split('/');
  if (parts.length < 2) {
    return emojiValue;
  }

  return parts[1];
};

export const ReactionBar = ({
  targetType,
  targetId,
  items,
  isLoggedIn,
  loading = false,
  stickerGroups = [],
  className = '',
  onToggle,
  onRequireLogin,
}: ReactionBarProps) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const [expanded, setExpanded] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pendingKey, setPendingKey] = useState<string | null>(null);

  const normalizedItems = useMemo(
    () => (items || []).filter((item) => item.voCount > 0),
    [items]
  );

  const reactedCount = useMemo(
    () => normalizedItems.filter((item) => item.voIsReacted).length,
    [normalizedItems]
  );

  const reachedReactionLimit = reactedCount >= MAX_USER_REACTIONS;

  const displayItems = useMemo(() => {
    if (expanded) {
      return normalizedItems;
    }

    return normalizedItems.slice(0, MAX_VISIBLE_BUBBLES);
  }, [expanded, normalizedItems]);

  const hiddenCount = Math.max(0, normalizedItems.length - MAX_VISIBLE_BUBBLES);

  useEffect(() => {
    if (!pickerOpen) {
      return;
    }

    const handleOutside = (event: PointerEvent) => {
      if (!containerRef.current) {
        return;
      }
      if (!containerRef.current.contains(event.target as Node)) {
        setPickerOpen(false);
      }
    };

    const handleEsc = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setPickerOpen(false);
      }
    };

    document.addEventListener('pointerdown', handleOutside, true);
    window.addEventListener('keydown', handleEsc);
    return () => {
      document.removeEventListener('pointerdown', handleOutside, true);
      window.removeEventListener('keydown', handleEsc);
    };
  }, [pickerOpen]);

  const runToggle = async (payload: ReactionTogglePayload, isAlreadyReacted: boolean) => {
    if (loading) {
      return;
    }

    if (!isLoggedIn) {
      onRequireLogin?.();
      return;
    }

    if (reachedReactionLimit && !isAlreadyReacted) {
      return;
    }

    const key = `${payload.emojiType}:${payload.emojiValue}`;
    if (pendingKey === key) {
      return;
    }

    setPendingKey(key);
    try {
      await onToggle(payload);
    } finally {
      setPendingKey(null);
    }
  };

  const handleBubbleClick = async (item: ReactionSummaryItem) => {
    await runToggle(
      {
        emojiType: item.voEmojiType,
        emojiValue: item.voEmojiValue,
      },
      item.voIsReacted
    );
  };

  const handleQuickEmoji = async (emoji: string) => {
    const existing = normalizedItems.find(
      (item) => item.voEmojiType === 'unicode' && item.voEmojiValue === emoji
    );

    try {
      await runToggle(
        {
          emojiType: 'unicode',
          emojiValue: emoji,
        },
        existing?.voIsReacted ?? false
      );
    } finally {
      setPickerOpen(false);
    }
  };

  const handlePickerSelect = async (selection: StickerPickerSelection) => {
    if (selection.type === 'unicode') {
      if (!selection.emoji) {
        return;
      }

      const existing = normalizedItems.find(
        (item) => item.voEmojiType === 'unicode' && item.voEmojiValue === selection.emoji
      );

      try {
        await runToggle(
          {
            emojiType: 'unicode',
            emojiValue: selection.emoji,
          },
          existing?.voIsReacted ?? false
        );
      } finally {
        setPickerOpen(false);
      }
      return;
    }

    const groupCode = selection.groupCode?.trim();
    const stickerCode = selection.stickerCode?.trim();
    if (!groupCode || !stickerCode) {
      return;
    }

    const emojiValue = `${groupCode}/${stickerCode}`;
    const existing = normalizedItems.find(
      (item) => item.voEmojiType === 'sticker' && item.voEmojiValue === emojiValue
    );

    try {
      await runToggle(
        {
          emojiType: 'sticker',
          emojiValue,
        },
        existing?.voIsReacted ?? false
      );
    } finally {
      setPickerOpen(false);
    }
  };

  const loginHint = '登录后可添加回应';
  const limitHint = `你已对该内容添加了 ${MAX_USER_REACTIONS} 种回应（上限）`;

  return (
    <div
      className={`${styles.container} ${className}`}
      ref={containerRef}
      data-target-type={targetType}
      data-target-id={targetId}
    >
      <div className={styles.bubbles}>
        {displayItems.map((item) => {
          const isPending = pendingKey === buildKey(item);
          const isDisabled = loading || isPending || (!item.voIsReacted && reachedReactionLimit) || !isLoggedIn;
          const label = `${item.voEmojiValue}，${item.voCount} 人回应，${item.voIsReacted ? '已选择' : '未选择'}`;

          return (
            <button
              key={buildKey(item)}
              type="button"
              className={`${styles.bubble} ${item.voIsReacted ? styles.bubbleActive : ''}`}
              onClick={() => {
                void handleBubbleClick(item);
              }}
              disabled={isDisabled}
              title={!isLoggedIn ? loginHint : !item.voIsReacted && reachedReactionLimit ? limitHint : ''}
              aria-label={label}
            >
              {item.voEmojiType === 'unicode' ? (
                <span className={styles.emoji}>{item.voEmojiValue}</span>
              ) : item.voThumbnailUrl ? (
                <img
                  src={item.voThumbnailUrl}
                  alt={item.voEmojiValue}
                  className={styles.sticker}
                  loading="lazy"
                  draggable={false}
                />
              ) : (
                <span className={styles.stickerFallback}>{parseStickerCode(item.voEmojiValue)}</span>
              )}
              <span className={styles.count}>{item.voCount}</span>
            </button>
          );
        })}

        {!expanded && hiddenCount > 0 && (
          <button
            type="button"
            className={`${styles.bubble} ${styles.expandBubble}`}
            onClick={() => setExpanded(true)}
            aria-label={`展开更多回应，剩余 ${hiddenCount} 个`}
            title="展开更多"
          >
            +{hiddenCount}
          </button>
        )}

        {expanded && normalizedItems.length > MAX_VISIBLE_BUBBLES && (
          <button
            type="button"
            className={styles.collapseButton}
            onClick={() => setExpanded(false)}
          >
            收起
          </button>
        )}

        <button
          type="button"
          className={styles.plusButton}
          onClick={() => {
            if (!isLoggedIn) {
              onRequireLogin?.();
              return;
            }
            setPickerOpen((prev) => !prev);
          }}
          title={!isLoggedIn ? loginHint : reachedReactionLimit ? limitHint : '添加回应'}
          aria-label="添加回应"
          disabled={loading}
        >
          <span className={styles.plusText}>+</span>
        </button>
      </div>

      {pickerOpen && isLoggedIn && (
        <div className={styles.quickPanel}>
          {QUICK_EMOJIS.map((emoji) => {
            const existing = normalizedItems.find(
              (item) => item.voEmojiType === 'unicode' && item.voEmojiValue === emoji
            );
            const disabled = loading || (!existing?.voIsReacted && reachedReactionLimit);
            return (
              <button
                key={emoji}
                type="button"
                className={styles.quickEmoji}
                onClick={() => {
                  void handleQuickEmoji(emoji);
                }}
                disabled={disabled}
                title={disabled ? limitHint : `添加 ${emoji}`}
              >
                {emoji}
              </button>
            );
          })}

          <div className={styles.morePicker}>
            <StickerPicker
              groups={stickerGroups}
              onSelect={(selection) => {
                void handlePickerSelect(selection);
              }}
              mode="reaction"
              triggerTitle="更多表情"
              disabled={loading || reachedReactionLimit}
              panelPlacement="left"
              className={styles.pickerTrigger}
            />
          </div>
        </div>
      )}
    </div>
  );
};
