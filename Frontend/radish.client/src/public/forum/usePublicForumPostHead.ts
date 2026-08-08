import { useMemo } from 'react';
import type { TFunction } from 'i18next';
import type { PostDetail } from '@/api/forum';
import { resolveMediaUrl } from '@/utils/media';
import { buildLocalizedPublicRouteHead } from '../publicHead';
import { buildForumPostStructuredData } from '../publicStructuredData';
import { usePublicHeadSnapshot } from '../publicHeadLifecycleContext';
import { isCurrentForumPostHeadSource } from '../publicHeadSourceIdentity';
import { buildForumPostPublicHead } from './publicForumUtils';

interface UsePublicForumPostHeadOptions {
  post: PostDetail | null;
  postId: string;
  commentId?: string;
  t: TFunction;
}

export const usePublicForumPostHead = ({
  post,
  postId,
  commentId,
  t,
}: UsePublicForumPostHeadOptions) => {
  const publicHeadSnapshot = useMemo(() => {
    if (!post || !isCurrentForumPostHeadSource(postId, post)) {
      return null;
    }
    const coverImageUrl = resolveMediaUrl(post.voCoverImage);
    const routeHead = buildLocalizedPublicRouteHead({
      app: 'forum',
      route: commentId
        ? { kind: 'detail', postId, commentId }
        : { kind: 'detail', postId },
    }, t);
    const postHead = buildForumPostPublicHead(post, commentId, coverImageUrl, {
      appName: t('desktop.apps.forum.name'),
      routeHead,
    });
    return {
      head: postHead,
      structuredData: buildForumPostStructuredData({
        post: { ...post, voCoverImage: coverImageUrl },
        canonicalPath: postHead.canonicalPath,
        fallbackDescription: routeHead.description,
      }),
    };
  }, [commentId, post, postId, t]);

  usePublicHeadSnapshot(publicHeadSnapshot);
};
