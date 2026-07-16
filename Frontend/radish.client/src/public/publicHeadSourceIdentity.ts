import type { PostDetail } from '@/api/forum';
import type { PublicUserProfile } from '@/api/user';
import type { WikiDocumentDetailVo } from '@/apps/wiki/types/wiki';
import type { PublicDocsRoute } from './docsRouteState';
import type { PublicProfileRoute } from './profileRouteState';
import type { PublicShopRoute } from './shopRouteState';

function normalizeRouteIdentity(value: string | number | null | undefined): string {
  return String(value ?? '').trim().toLowerCase();
}

export function isCurrentShopProductHeadSource(
  route: PublicShopRoute,
  productId: string | number | null | undefined,
): boolean {
  return route.kind === 'detail'
    && normalizeRouteIdentity(productId) === normalizeRouteIdentity(route.productId);
}

export function isCurrentDocsHeadSource(
  route: PublicDocsRoute,
  document: Pick<WikiDocumentDetailVo, 'voId' | 'voSlug'>,
): boolean {
  if (route.kind !== 'detail') {
    return false;
  }

  const routeIdentifier = normalizeRouteIdentity(route.slug);
  return [document.voId, document.voSlug]
    .some((identifier) => normalizeRouteIdentity(identifier) === routeIdentifier);
}

export function isCurrentForumPostHeadSource(
  routePostId: string,
  post: Pick<PostDetail, 'voId' | 'voPublicId'>,
): boolean {
  const routeIdentifier = normalizeRouteIdentity(routePostId);
  return [post.voId, post.voPublicId]
    .some((identifier) => normalizeRouteIdentity(identifier) === routeIdentifier);
}

export function isCurrentProfileHeadSource(
  route: PublicProfileRoute,
  profile: PublicUserProfile,
): boolean {
  if (route.kind !== 'detail') {
    return false;
  }

  const routeIdentifier = normalizeRouteIdentity(route.userId);
  return [profile.voUserId, profile.voPublicId]
    .some((identifier) => normalizeRouteIdentity(identifier) === routeIdentifier);
}
