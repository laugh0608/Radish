import {
  LeaderboardCategory,
  LeaderboardType,
  type LeaderboardCategory as LeaderboardCategoryValue,
  type LeaderboardType as LeaderboardTypeValue,
} from '../api/leaderboard.contract.ts';

export type PublicLeaderboardTypeSlug =
  | 'experience'
  | 'post-count'
  | 'comment-count'
  | 'popularity'
  | 'hot-product';

export interface PublicLeaderboardTypeRouteDefinition {
  slug: PublicLeaderboardTypeSlug;
  type: LeaderboardTypeValue;
  category: LeaderboardCategoryValue;
}

export interface PublicLeaderboardRoute {
  kind: 'list';
  typeSlug: PublicLeaderboardTypeSlug;
  page: number;
}

export const publicLeaderboardTypeRouteDefinitions: PublicLeaderboardTypeRouteDefinition[] = [
  { slug: 'experience', type: LeaderboardType.Experience, category: LeaderboardCategory.User },
  { slug: 'post-count', type: LeaderboardType.PostCount, category: LeaderboardCategory.User },
  { slug: 'comment-count', type: LeaderboardType.CommentCount, category: LeaderboardCategory.User },
  { slug: 'popularity', type: LeaderboardType.Popularity, category: LeaderboardCategory.User },
  { slug: 'hot-product', type: LeaderboardType.HotProduct, category: LeaderboardCategory.Product },
];

const defaultLeaderboardRouteDefinition = publicLeaderboardTypeRouteDefinitions[0];

export function createDefaultPublicLeaderboardRoute(): PublicLeaderboardRoute {
  return {
    kind: 'list',
    typeSlug: defaultLeaderboardRouteDefinition.slug,
    page: 1,
  };
}

function normalizePositiveInteger(value: string | undefined): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return undefined;
  }

  return parsed;
}

function normalizeTypeSlug(value: string | undefined): PublicLeaderboardTypeSlug | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim().toLowerCase() as PublicLeaderboardTypeSlug;
  return publicLeaderboardTypeRouteDefinitions.some((item) => item.slug === normalized)
    ? normalized
    : undefined;
}

export function isPublicLeaderboardTypeValue(type: number): type is LeaderboardTypeValue {
  return publicLeaderboardTypeRouteDefinitions.some((item) => item.type === type);
}

export function filterPublicLeaderboardTypes<T extends { voType: number; voCategory: number }>(
  types: readonly T[]
): T[] {
  const seenTypes = new Set<number>();
  return types.filter((item) => {
    if (!isPublicLeaderboardTypeValue(item.voType) || seenTypes.has(item.voType)) {
      return false;
    }

    const routeDefinition = getPublicLeaderboardRouteDefinitionByType(item.voType);
    if (item.voCategory !== routeDefinition.category) {
      return false;
    }

    seenTypes.add(item.voType);
    return true;
  });
}

export function parsePublicLeaderboardRoute(pathname: string, search: string): PublicLeaderboardRoute {
  const matched = pathname.match(/^\/leaderboard(?:\/([a-z-]+))?\/?$/);
  const params = new URLSearchParams(search);

  return {
    kind: 'list',
    typeSlug: normalizeTypeSlug(matched?.[1]) ?? defaultLeaderboardRouteDefinition.slug,
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1,
  };
}

export function buildPublicLeaderboardPath(route: PublicLeaderboardRoute): string {
  const search = new URLSearchParams();
  if (route.page > 1) {
    search.set('page', String(route.page));
  }

  const typeSegment = route.typeSlug === defaultLeaderboardRouteDefinition.slug
    ? ''
    : `/${route.typeSlug}`;
  const queryString = search.toString();
  return queryString ? `/leaderboard${typeSegment}?${queryString}` : `/leaderboard${typeSegment}`;
}

export function getPublicLeaderboardRouteDefinitionBySlug(
  typeSlug: PublicLeaderboardTypeSlug
): PublicLeaderboardTypeRouteDefinition {
  return publicLeaderboardTypeRouteDefinitions.find((item) => item.slug === typeSlug)
    ?? defaultLeaderboardRouteDefinition;
}

export function getPublicLeaderboardRouteDefinitionByType(
  type: LeaderboardTypeValue
): PublicLeaderboardTypeRouteDefinition {
  return publicLeaderboardTypeRouteDefinitions.find((item) => item.type === type)
    ?? defaultLeaderboardRouteDefinition;
}
