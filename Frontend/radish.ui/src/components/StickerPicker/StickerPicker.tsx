import { useEffect, useMemo, useRef, useState } from 'react';
import { Icon } from '../Icon/Icon';
import styles from './StickerPicker.module.css';

const DEFAULT_EMOJIS = [
  '😀', '😁', '😂', '🤣', '😊', '😍', '😘', '😎', '🤔', '😴',
  '🥳', '🤯', '😇', '🤗', '😏', '😤', '😭', '😱', '🤝', '🙏',
  '👍', '👎', '👏', '🙌', '💪', '❤️', '🔥', '✨', '🎉', '💯',
];

export type StickerPickerMode = 'insert' | 'reaction';

export interface StickerPickerSticker {
  code: string;
  name: string;
  imageUrl: string;
  thumbnailUrl?: string | null;
  isAnimated?: boolean;
  allowInline?: boolean;
  useCount?: number;
}

export interface StickerPickerGroup {
  code: string;
  name: string;
  coverImageUrl?: string | null;
  sort?: number;
  stickers: StickerPickerSticker[];
}

export interface StickerPickerSelection {
  type: 'unicode' | 'sticker';
  emoji?: string;
  groupCode?: string;
  stickerCode?: string;
  stickerName?: string;
  imageUrl?: string;
  thumbnailUrl?: string;
}

export interface StickerPickerProps {
  groups: StickerPickerGroup[];
  onSelect: (selection: StickerPickerSelection) => void;
  mode?: StickerPickerMode;
  theme?: 'dark' | 'light';
  disabled?: boolean;
  className?: string;
  triggerTitle?: string;
  emojis?: string[];
}

const normalizeCode = (value: string): string => value.trim().toLowerCase();

const bySortThenName = (a: StickerPickerGroup, b: StickerPickerGroup): number => {
  const aSort = a.sort ?? 0;
  const bSort = b.sort ?? 0;
  if (aSort !== bSort) {
    return aSort - bSort;
  }

  return a.name.localeCompare(b.name);
};

export const StickerPicker = ({
  groups,
  onSelect,
  mode = 'insert',
  theme = 'dark',
  disabled = false,
  className = '',
  triggerTitle = '插入表情包',
  emojis = DEFAULT_EMOJIS,
}: StickerPickerProps) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [activeTab, setActiveTab] = useState('emoji');
  const [keyword, setKeyword] = useState('');

  const normalizedGroups = useMemo(
    () =>
      (groups || [])
        .map((group) => ({
          ...group,
          code: normalizeCode(group.code),
          stickers: [...(group.stickers || [])].sort((a, b) => {
            const aWeight = a.useCount ?? 0;
            const bWeight = b.useCount ?? 0;
            if (aWeight !== bWeight) {
              return bWeight - aWeight;
            }
            return a.name.localeCompare(b.name);
          }),
        }))
        .filter((group) => Boolean(group.code))
        .sort(bySortThenName),
    [groups]
  );

  useEffect(() => {
    if (activeTab === 'emoji') {
      return;
    }

    const exists = normalizedGroups.some((group) => group.code === activeTab);
    if (!exists) {
      setActiveTab('emoji');
    }
  }, [activeTab, normalizedGroups]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const handleOutsideClick = (event: MouseEvent) => {
      if (!containerRef.current) {
        return;
      }
      if (!containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };

    const handleEsc = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    window.addEventListener('keydown', handleEsc);
    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
      window.removeEventListener('keydown', handleEsc);
    };
  }, [open]);

  const selectedGroup = useMemo(
    () => normalizedGroups.find((group) => group.code === activeTab),
    [activeTab, normalizedGroups]
  );

  const normalizedKeyword = keyword.trim().toLowerCase();

  const filteredEmojis = useMemo(() => {
    if (!normalizedKeyword) {
      return emojis;
    }
    return emojis.filter((emoji) => emoji.includes(normalizedKeyword));
  }, [emojis, normalizedKeyword]);

  const filteredStickers = useMemo(() => {
    if (!selectedGroup) {
      return [];
    }

    const byInlineMode = selectedGroup.stickers.filter((item) =>
      mode === 'reaction' ? true : item.allowInline !== false
    );
    if (!normalizedKeyword) {
      return byInlineMode;
    }

    return byInlineMode.filter((item) => {
      const code = item.code.toLowerCase();
      const name = item.name.toLowerCase();
      return code.includes(normalizedKeyword) || name.includes(normalizedKeyword);
    });
  }, [mode, normalizedKeyword, selectedGroup]);

  const handleSelectEmoji = (emoji: string) => {
    onSelect({ type: 'unicode', emoji });
    setOpen(false);
    setKeyword('');
  };

  const handleSelectSticker = (sticker: StickerPickerSticker) => {
    if (!selectedGroup) {
      return;
    }

    const disabledInInsert = mode === 'insert' && sticker.allowInline === false;
    if (disabledInInsert) {
      return;
    }

    onSelect({
      type: 'sticker',
      groupCode: selectedGroup.code,
      stickerCode: sticker.code,
      stickerName: sticker.name,
      imageUrl: sticker.imageUrl,
      thumbnailUrl: sticker.thumbnailUrl || undefined,
    });
    setOpen(false);
    setKeyword('');
  };

  return (
    <div
      className={`${styles.container} ${theme === 'light' ? styles.themeLight : ''} ${className}`}
      ref={containerRef}
    >
      <button
        type="button"
        className={styles.trigger}
        title={triggerTitle}
        aria-label={triggerTitle}
        onClick={() => setOpen((prev) => !prev)}
        disabled={disabled}
      >
        <Icon icon="mdi:sticker-emoji" size={18} />
      </button>

      {open && (
        <div className={styles.panel}>
          <div className={styles.tabs}>
            <button
              type="button"
              className={`${styles.tab} ${activeTab === 'emoji' ? styles.tabActive : ''}`}
              onClick={() => setActiveTab('emoji')}
            >
              <Icon icon="mdi:emoticon-happy-outline" size={16} />
            </button>
            {normalizedGroups.map((group) => (
              <button
                key={group.code}
                type="button"
                className={`${styles.tab} ${activeTab === group.code ? styles.tabActive : ''}`}
                onClick={() => setActiveTab(group.code)}
                title={group.name}
              >
                {group.coverImageUrl ? (
                  <img src={group.coverImageUrl} alt={group.name} className={styles.tabImage} />
                ) : (
                  <span className={styles.tabText}>{group.name.slice(0, 2)}</span>
                )}
              </button>
            ))}
          </div>

          <div className={styles.searchRow}>
            <Icon icon="mdi:magnify" size={14} />
            <input
              type="text"
              value={keyword}
              onChange={(event) => setKeyword(event.target.value)}
              className={styles.searchInput}
              placeholder="搜索表情"
            />
            {keyword && (
              <button
                type="button"
                className={styles.clearButton}
                onClick={() => setKeyword('')}
                aria-label="清空搜索"
              >
                <Icon icon="mdi:close" size={14} />
              </button>
            )}
          </div>

          {activeTab === 'emoji' ? (
            <div className={styles.grid}>
              {filteredEmojis.map((emoji) => (
                <button
                  key={emoji}
                  type="button"
                  className={styles.emojiItem}
                  onClick={() => handleSelectEmoji(emoji)}
                >
                  {emoji}
                </button>
              ))}
            </div>
          ) : (
            <div className={styles.grid}>
              {filteredStickers.map((sticker) => {
                const disabledInInsert = mode === 'insert' && sticker.allowInline === false;
                const preview = sticker.thumbnailUrl || sticker.imageUrl;

                return (
                  <button
                    key={`${sticker.code}-${sticker.name}`}
                    type="button"
                    className={`${styles.stickerItem} ${disabledInInsert ? styles.stickerItemDisabled : ''}`}
                    title={disabledInInsert ? `${sticker.name}（仅支持 Reaction）` : sticker.name}
                    onClick={() => handleSelectSticker(sticker)}
                    disabled={disabledInInsert}
                  >
                    <img src={preview} alt={sticker.name} className={styles.stickerImage} />
                    {sticker.isAnimated && <span className={styles.gifBadge}>GIF</span>}
                  </button>
                );
              })}
            </div>
          )}

          {activeTab === 'emoji' && filteredEmojis.length === 0 && (
            <div className={styles.empty}>未找到匹配的表情</div>
          )}
          {activeTab !== 'emoji' && filteredStickers.length === 0 && (
            <div className={styles.empty}>该分组暂无可插入表情</div>
          )}
        </div>
      )}
    </div>
  );
};
