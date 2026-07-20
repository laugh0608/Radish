import { useCallback, useEffect, useMemo, useState } from 'react';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import type { StickerPickerGroup, StickerPickerSelection } from '@radish/ui/sticker-picker';
import { getStickerGroups, recordStickerUse, type StickerGroupVo } from '@/api/sticker';
import { log } from '@/utils/logger';

const normalizeCode = (value: string): string => value.trim().toLowerCase();

const buildStickerKey = (groupCode: string, stickerCode: string): string =>
  `${normalizeCode(groupCode)}/${normalizeCode(stickerCode)}`;

const buildPickerGroups = (groups: StickerGroupVo[]): StickerPickerGroup[] => {
  return groups
    .map((group) => ({
      code: group.voCode,
      name: group.voName,
      coverImageUrl: group.voCoverImageUrl,
      sort: group.voSort,
      stickers: (group.voStickers || []).map((sticker) => ({
        code: sticker.voCode,
        name: sticker.voName,
        imageUrl: sticker.voImageUrl,
        thumbnailUrl: sticker.voThumbnailUrl,
        isAnimated: sticker.voIsAnimated,
        allowInline: sticker.voAllowInline,
        useCount: sticker.voUseCount,
      })),
    }))
    .sort((a, b) => (a.sort ?? 0) - (b.sort ?? 0));
};

const buildRenderMap = (groups: StickerGroupVo[]): MarkdownStickerMap => {
  const map: MarkdownStickerMap = {};
  for (const group of groups) {
    const groupCode = normalizeCode(group.voCode);
    if (!groupCode) {
      continue;
    }

    for (const sticker of group.voStickers || []) {
      const stickerCode = normalizeCode(sticker.voCode);
      if (!stickerCode || !sticker.voImageUrl) {
        continue;
      }

      map[buildStickerKey(groupCode, stickerCode)] = {
        imageUrl: sticker.voImageUrl,
        thumbnailUrl: sticker.voThumbnailUrl,
        name: sticker.voName,
      };
    }
  }

  return map;
};

export const useStickerCatalog = () => {
  const [rawGroups, setRawGroups] = useState<StickerGroupVo[]>([]);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);

  const loadGroups = useCallback(async (forceRefresh: boolean = false) => {
    try {
      setLoading(true);
      const groups = await getStickerGroups(forceRefresh);
      setRawGroups(groups);
      setLoaded(true);
    } catch (error) {
      log.warn('加载表情包分组失败:', error);
      setRawGroups([]);
      setLoaded(false);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGroups();
  }, [loadGroups]);

  const stickerGroups = useMemo(() => buildPickerGroups(rawGroups), [rawGroups]);
  const stickerMap = useMemo(() => buildRenderMap(rawGroups), [rawGroups]);

  const handleStickerSelect = useCallback(async (selection: StickerPickerSelection) => {
    try {
      if (selection.type === 'unicode') {
        if (!selection.emoji) {
          return;
        }

        await recordStickerUse({
          emojiType: 'unicode',
          emojiValue: selection.emoji,
        });
        return;
      }

      const groupCode = selection.groupCode?.trim();
      const stickerCode = selection.stickerCode?.trim();
      if (!groupCode || !stickerCode) {
        return;
      }

      await recordStickerUse({
        emojiType: 'sticker',
        emojiValue: `${groupCode}/${stickerCode}`,
      });
    } catch (error) {
      log.warn('记录表情使用失败（不影响编辑）:', error);
    }
  }, []);

  return {
    stickerGroups,
    stickerMap,
    loading,
    loaded,
    reload: () => loadGroups(true),
    handleStickerSelect,
  };
};
