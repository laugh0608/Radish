import type { TFunction } from 'i18next';
import type {
  Category,
  CommentNode,
  PostDetail,
  PostItem,
  Tag,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import type {
  PublicForumBrowseRoute,
  PublicForumListRoute,
  PublicForumSearchRoute,
  PublicForumTagRoute,
  PublicForumTypeRoute,
} from '../forumRouteState';
import { buildPublicForumPath } from '../forumRouteState.ts';
import type { PublicReadingGuideItem } from '../components/PublicReadingGuide';
import { publicDefaultDescription, type PublicHeadDescriptor } from '../publicHead.ts';

interface PublicGuideDefinition {
  titleKey: string;
  descriptionKey: string;
  readingValueKey: string;
  nextValueKey: string;
  boundaryValueKey: string;
}

export const listGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.listGuide.title',
  descriptionKey: 'forum.public.listGuide.description',
  readingValueKey: 'forum.public.listGuide.readingValue',
  nextValueKey: 'forum.public.listGuide.nextValue',
  boundaryValueKey: 'forum.public.listGuide.boundaryValue'
};

export const categoryGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.categoryGuide.title',
  descriptionKey: 'forum.public.categoryGuide.description',
  readingValueKey: 'forum.public.categoryGuide.readingValue',
  nextValueKey: 'forum.public.categoryGuide.nextValue',
  boundaryValueKey: 'forum.public.categoryGuide.boundaryValue'
};

export const tagGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.tagGuide.title',
  descriptionKey: 'forum.public.tagGuide.description',
  readingValueKey: 'forum.public.tagGuide.readingValue',
  nextValueKey: 'forum.public.tagGuide.nextValue',
  boundaryValueKey: 'forum.public.tagGuide.boundaryValue'
};

export const questionGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.questionGuide.title',
  descriptionKey: 'forum.public.questionGuide.description',
  readingValueKey: 'forum.public.questionGuide.readingValue',
  nextValueKey: 'forum.public.questionGuide.nextValue',
  boundaryValueKey: 'forum.public.questionGuide.boundaryValue'
};

export const pollGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.pollGuide.title',
  descriptionKey: 'forum.public.pollGuide.description',
  readingValueKey: 'forum.public.pollGuide.readingValue',
  nextValueKey: 'forum.public.pollGuide.nextValue',
  boundaryValueKey: 'forum.public.pollGuide.boundaryValue'
};

export const lotteryGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.lotteryGuide.title',
  descriptionKey: 'forum.public.lotteryGuide.description',
  readingValueKey: 'forum.public.lotteryGuide.readingValue',
  nextValueKey: 'forum.public.lotteryGuide.nextValue',
  boundaryValueKey: 'forum.public.lotteryGuide.boundaryValue'
};

export const searchGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.searchGuide.title',
  descriptionKey: 'forum.public.searchGuide.description',
  readingValueKey: 'forum.public.searchGuide.readingValue',
  nextValueKey: 'forum.public.searchGuide.nextValue',
  boundaryValueKey: 'forum.public.searchGuide.boundaryValue'
};

export const detailGuideDefinition: PublicGuideDefinition = {
  titleKey: 'forum.public.detailGuide.title',
  descriptionKey: 'forum.public.detailGuide.description',
  readingValueKey: 'forum.public.detailGuide.readingValue',
  nextValueKey: 'forum.public.detailGuide.nextValue',
  boundaryValueKey: 'forum.public.detailGuide.boundaryValue'
};

export function isSameLongId(left: LongId | null | undefined, right: LongId | null | undefined): boolean {
  if (left == null || right == null) {
    return false;
  }

  return String(left) === String(right);
}

export function getForumPostRouteIdentifier(
  post: Pick<PostItem, 'voId' | 'voPublicId'> | Pick<PostDetail, 'voId' | 'voPublicId'>
): string {
  return post.voPublicId?.trim() || String(post.voId);
}

function normalizePublicHeadText(value: string | number | null | undefined): string | undefined {
  const normalized = String(value ?? '').replace(/\s+/g, ' ').trim();
  return normalized || undefined;
}

function stripMarkdownForPublicHead(value: string | null | undefined): string | undefined {
  const normalized = normalizePublicHeadText(
    value
      ?.replace(/```[\s\S]*?```/g, ' ')
      .replace(/`([^`]+)`/g, '$1')
      .replace(/!\[[^\]]*]\([^)]*\)/g, ' ')
      .replace(/\[([^\]]+)]\([^)]*\)/g, '$1')
      .replace(/[#>*_~-]+/g, ' ')
  );
  return normalized;
}

function truncatePublicHeadText(value: string | undefined, maxLength: number): string | undefined {
  if (!value || value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength - 1).trimEnd()}…`;
}

export function buildForumPostPublicHead(
  post: Pick<PostDetail, 'voId' | 'voPublicId' | 'voTitle' | 'voSummary' | 'voContent'>,
  commentId?: string,
  imageUrl?: string | null
): PublicHeadDescriptor {
  const postIdentifier = getForumPostRouteIdentifier(post);
  const canonicalPath = buildPublicForumPath(commentId
    ? { kind: 'detail', postId: postIdentifier, commentId }
    : { kind: 'detail', postId: postIdentifier });
  const title = normalizePublicHeadText(post.voTitle) ?? '论坛帖子';
  const description = truncatePublicHeadText(
    normalizePublicHeadText(post.voSummary) ?? stripMarkdownForPublicHead(post.voContent),
    160
  ) ?? publicDefaultDescription;

  return {
    title: `${title} - Radish 论坛`,
    description,
    canonicalPath,
    type: 'article',
    ...(imageUrl ? { imageUrl } : {}),
  };
}

export function createForumReadingGuide(
  t: TFunction,
  definition: PublicGuideDefinition
): {
  label: string;
  title: string;
  description: string;
  items: PublicReadingGuideItem[];
} {
  return {
    label: t('forum.public.guide.label'),
    title: t(definition.titleKey),
    description: t(definition.descriptionKey),
    items: [
      {
        label: t('forum.public.guide.readingLabel'),
        value: t(definition.readingValueKey)
      },
      {
        label: t('forum.public.guide.nextLabel'),
        value: t(definition.nextValueKey)
      },
      {
        label: t('forum.public.guide.boundaryLabel'),
        value: t(definition.boundaryValueKey)
      }
    ]
  };
}

export function mergeCommentChildren(
  comments: CommentNode[],
  parentCommentId: LongId,
  children: CommentNode[],
  totalChildren: number
): CommentNode[] {
  return comments.map((comment) => {
    if (!isSameLongId(comment.voId, parentCommentId)) {
      return comment;
    }

    return {
      ...comment,
      voChildren: children,
      voChildrenTotal: totalChildren
    };
  });
}

export function buildActiveSectionTitle(categories: Category[], selectedCategoryId: string | null, fallback: string): string {
  if (!selectedCategoryId) {
    return fallback;
  }

  return categories.find((item) => item.voId === selectedCategoryId)?.voName || fallback;
}

export function buildCategoryIntro(category: Category | null, fallback: string): string {
  if (!category) {
    return fallback;
  }

  const description = category.voDescription?.trim();
  if (description) {
    return description;
  }

  return fallback;
}

export function formatCategoryPostCount(category: Category | null, t: TFunction): string | null {
  if (!category || typeof category.voPostCount !== 'number') {
    return null;
  }

  return t('forum.public.categoryPostCount', { count: category.voPostCount });
}

export function formatTagPostCount(tag: Tag | null, t: TFunction): string | null {
  if (!tag || typeof tag.voPostCount !== 'number') {
    return null;
  }

  return t('forum.public.tagPostCount', { count: tag.voPostCount });
}

export function buildVisiblePages(currentPage: number, totalPages: number, maxVisible: number): number[] {
  if (totalPages <= maxVisible) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  const half = Math.floor(maxVisible / 2);
  if (currentPage <= half + 1) {
    return Array.from({ length: maxVisible }, (_, index) => index + 1);
  }

  if (currentPage >= totalPages - half) {
    return Array.from({ length: maxVisible }, (_, index) => totalPages - maxVisible + 1 + index);
  }

  return Array.from({ length: maxVisible }, (_, index) => currentPage - half + index);
}

export function buildListRouteKey(route: PublicForumListRoute): string {
  return `${route.categoryId ?? 'all'}:${route.sortBy}:${route.page}`;
}

export function buildTagRouteKey(route: PublicForumTagRoute): string {
  return `${route.tagSlug}:${route.sortBy}:${route.page}`;
}

export function buildSearchRouteKey(route: PublicForumSearchRoute): string {
  return `${route.keyword}:${route.sortBy}:${route.timeRange}:${route.startDate ?? ''}:${route.endDate ?? ''}:${route.page}`;
}

export function buildTypeRouteKey(route: PublicForumTypeRoute): string {
  return `${route.kind}:${route.sortBy}:${route.page}`;
}

export function buildBrowseRouteKey(route: PublicForumBrowseRoute): string {
  return route.kind === 'list'
    ? buildListRouteKey(route)
    : route.kind === 'tag'
      ? buildTagRouteKey(route)
      : route.kind === 'search'
        ? buildSearchRouteKey(route)
        : buildTypeRouteKey(route);
}

export function buildSearchTimeRange(route: PublicForumSearchRoute): { startTime?: string; endTime?: string } {
  const now = new Date();

  switch (route.timeRange) {
    case '24h':
      return {
        startTime: new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case '7d':
      return {
        startTime: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case '30d':
      return {
        startTime: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case 'custom': {
      if (!route.startDate && !route.endDate) {
        return {};
      }

      const parsedStart = route.startDate ? new Date(`${route.startDate}T00:00:00`) : null;
      const parsedEnd = route.endDate ? new Date(`${route.endDate}T23:59:59.999`) : null;

      if ((parsedStart && Number.isNaN(parsedStart.getTime())) || (parsedEnd && Number.isNaN(parsedEnd.getTime()))) {
        return {};
      }

      if (parsedStart && parsedEnd && parsedStart > parsedEnd) {
        return {
          startTime: parsedEnd.toISOString(),
          endTime: parsedStart.toISOString()
        };
      }

      return {
        startTime: parsedStart ? parsedStart.toISOString() : undefined,
        endTime: parsedEnd ? parsedEnd.toISOString() : undefined
      };
    }
    case 'all':
    default:
      return {};
  }
}
