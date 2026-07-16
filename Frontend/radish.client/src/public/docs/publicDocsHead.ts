import type { WikiDocumentDetailVo } from '@/apps/wiki/types/wiki';
import { buildPublicDocsPath, type PublicDocsDetailRoute } from '../docsRouteState.ts';
import { buildPublicRouteHead, type PublicHeadDescriptor } from '../publicHead.ts';
import type { PublicHeadSnapshot } from '../publicHeadLifecycleContext.ts';
import { buildDocsArticleStructuredData } from '../publicStructuredData.ts';

interface BuildPublicDocsHeadSnapshotOptions {
  appName?: string;
  routeHead?: PublicHeadDescriptor;
}

export function buildPublicDocsHeadSnapshot(
  document: WikiDocumentDetailVo,
  anchor: string | undefined,
  options: BuildPublicDocsHeadSnapshotOptions = {},
): PublicHeadSnapshot {
  const canonicalRoute: PublicDocsDetailRoute = {
    kind: 'detail',
    slug: document.voSlug,
    anchor,
  };
  const canonicalPath = buildPublicDocsPath(canonicalRoute);
  const routeHead = options.routeHead ?? buildPublicRouteHead({ app: 'docs', route: canonicalRoute });
  const appName = options.appName?.trim() || 'Radish 文档';
  const documentTitle = document.voTitle.trim();

  return {
    head: {
      ...routeHead,
      title: documentTitle ? `${documentTitle} · ${appName}` : routeHead.title,
      description: document.voSummary?.trim() || routeHead.description,
      canonicalPath,
      type: 'article',
    },
    structuredData: buildDocsArticleStructuredData({
      document,
      canonicalPath,
      fallbackDescription: routeHead.description,
    }),
  };
}
