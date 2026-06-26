import { useMemo, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import type { WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { getProductTypeDisplay, type ProductListItem } from '@/api/shop';
import type { PostItem } from '@/api/forum';
import { resolveMediaUrl } from '@/utils/media';
import { buildPublicDocsPath } from '../docsRouteState';
import { buildPublicForumPath } from '../forumRouteState';
import { getForumPostRouteIdentifier, resolvePublicProfileUserId } from '../forum/publicForumUtils';
import { buildPublicShopPath } from '../shopRouteState';
import {
  buildDocumentSummary,
  buildProductSummary,
  formatDocumentMeta,
  isRecentDocument,
} from './publicDiscoverFeedUtils';
import styles from './PublicDiscoverApp.module.css';

type DiscoverFeedItem =
  | {
      key: string;
      kind: 'post';
      post: PostItem;
    }
  | {
      key: string;
      kind: 'document';
      document: WikiDocumentVo;
    }
  | {
      key: string;
      kind: 'product';
      product: ProductListItem;
    };

interface PublicDiscoverFeedProps {
  forumPosts: PostItem[];
  documents: WikiDocumentVo[];
  products: ProductListItem[];
  loadingForum: boolean;
  loadingDocs: boolean;
  loadingShop: boolean;
  forumError: string | null;
  docsError: string | null;
  shopError: string | null;
  displayTimeZone: string;
  locale: string;
  onReload: () => void;
  onOpenPost: (post: PostItem) => void;
  onOpenTag: (tagSlug: string) => void;
  onOpenQuestion: () => void;
  onOpenPoll: () => void;
  onOpenLottery: () => void;
  onOpenDocument: (document: WikiDocumentVo) => void;
  onOpenProduct: (product: ProductListItem) => void;
  onOpenForum: () => void;
  onOpenDocs: () => void;
  onOpenShop: () => void;
}

interface DiscoverFeedItemCardProps {
  item: DiscoverFeedItem;
  displayTimeZone: string;
  locale: string;
  onOpenPost: (post: PostItem) => void;
  onOpenTag: (tagSlug: string) => void;
  onOpenQuestion: () => void;
  onOpenPoll: () => void;
  onOpenLottery: () => void;
  onOpenDocument: (document: WikiDocumentVo) => void;
  onOpenProduct: (product: ProductListItem) => void;
}

interface FeedStatusCardProps {
  tone: 'loading' | 'error' | 'empty';
  title: string;
  description: string;
  primaryAction?: {
    label: string;
    onClick: () => void;
  };
}

function FeedStatusCard({
  tone,
  title,
  description,
  primaryAction,
}: FeedStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : 'mdi:alert-circle-outline';

  return (
    <div className={styles.statusCard} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={20} />
      </div>
      <div className={styles.statusBody}>
        <h3 className={styles.statusTitle}>{title}</h3>
        <p className={styles.statusDescription}>{description}</p>
        {primaryAction && (
          <div className={styles.statusActions}>
            <button type="button" className={styles.secondaryButton} onClick={primaryAction.onClick}>
              {primaryAction.label}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function handleFeedLinkClick(
  event: MouseEvent<HTMLAnchorElement>,
  action: () => void
) {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
    return;
  }

  event.preventDefault();
  action();
}

function buildDiscoverFeedItems(
  posts: PostItem[],
  documents: WikiDocumentVo[],
  products: ProductListItem[]
): DiscoverFeedItem[] {
  const items: DiscoverFeedItem[] = [];
  let documentIndex = 0;
  let productIndex = 0;

  posts.forEach((post, index) => {
    items.push({
      key: `post-${post.voId}`,
      kind: 'post',
      post,
    });

    if ((index === 0 || index === 2 || index === 5) && documentIndex < documents.length) {
      const document = documents[documentIndex];
      items.push({
        key: `document-${document.voId}`,
        kind: 'document',
        document,
      });
      documentIndex += 1;
    }

    if ((index === 1 || index === 4) && productIndex < products.length) {
      const product = products[productIndex];
      items.push({
        key: `product-${product.voId}`,
        kind: 'product',
        product,
      });
      productIndex += 1;
    }
  });

  while (documentIndex < documents.length || productIndex < products.length) {
    if (documentIndex < documents.length) {
      const document = documents[documentIndex];
      items.push({
        key: `document-${document.voId}`,
        kind: 'document',
        document,
      });
      documentIndex += 1;
    }

    if (productIndex < products.length) {
      const product = products[productIndex];
      items.push({
        key: `product-${product.voId}`,
        kind: 'product',
        product,
      });
      productIndex += 1;
    }
  }

  return items.slice(0, 14);
}

function DiscoverFeedItemCard({
  item,
  displayTimeZone,
  locale,
  onOpenPost,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery,
  onOpenDocument,
  onOpenProduct,
}: DiscoverFeedItemCardProps) {
  const { t } = useTranslation();

  if (item.kind === 'post') {
    const postHref = buildPublicForumPath({ kind: 'detail', postId: getForumPostRouteIdentifier(item.post) });
    return (
      <article className={styles.feedPostItem}>
        <div className={styles.feedItemHeader}>
          <span className={styles.feedItemType}>
            <Icon icon="mdi:forum-outline" size={15} />
            <span>{t('discover.public.streamPostLabel')}</span>
          </span>
          <span className={styles.feedItemSource}>{t('discover.public.streamSourceForum')}</span>
        </div>
        <PostCard
          post={item.post}
          displayTimeZone={displayTimeZone}
          onClick={() => onOpenPost(item.post)}
          href={postHref}
          variant="publicCompact"
          resolveAuthorProfileId={resolvePublicProfileUserId}
          onTagClick={(_, tagSlug) => onOpenTag(tagSlug)}
          onQuestionClick={onOpenQuestion}
          onPollClick={onOpenPoll}
          onLotteryClick={onOpenLottery}
        />
      </article>
    );
  }

  if (item.kind === 'document') {
    const document = item.document;
    const documentHref = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });
    return (
      <article className={styles.feedContentItem}>
        <a
          className={styles.feedContentButton}
          href={documentHref}
          onClick={(event) => handleFeedLinkClick(event, () => onOpenDocument(document))}
        >
          <div className={styles.feedItemHeader}>
            <span className={styles.feedItemType}>
              <Icon icon="mdi:file-document-outline" size={15} />
              <span>{t('discover.public.streamDocLabel')}</span>
            </span>
            <span className={styles.feedItemSource}>{t('discover.public.streamSourceDocs')}</span>
          </div>
          <div className={styles.itemChipRow}>
            <span className={styles.itemChip}>{t('discover.public.docsItemReadable')}</span>
            <span className={styles.itemChip}>
              {isRecentDocument(document)
                ? t('discover.public.docsItemRecent')
                : t('discover.public.docsItemSearchable')}
            </span>
          </div>
          <h3 className={styles.feedContentTitle}>{document.voTitle}</h3>
          <p className={styles.feedContentSummary}>
            {buildDocumentSummary(document, locale, t)}
          </p>
          <span className={styles.feedMetaLine}>
            <Icon icon="mdi:calendar-blank-outline" size={16} />
            <span>{formatDocumentMeta(document, locale, t('discover.public.documentMetaFallback'))}</span>
          </span>
        </a>
      </article>
    );
  }

  const product = item.product;
  const imageUrl = resolveMediaUrl(product.voCoverImage || product.voIcon);
  const productHref = buildPublicShopPath({ kind: 'detail', productId: String(product.voId) });
  return (
    <article className={styles.feedContentItem}>
      <a
        className={styles.feedContentButton}
        href={productHref}
        onClick={(event) => handleFeedLinkClick(event, () => onOpenProduct(product))}
      >
        <div className={styles.feedItemHeader}>
          <span className={styles.feedItemType}>
            <Icon icon="mdi:storefront-outline" size={15} />
            <span>{t('discover.public.streamProductLabel')}</span>
          </span>
          <span className={styles.feedItemSource}>{t('discover.public.streamSourceShop')}</span>
        </div>
        <div className={styles.feedProductHead}>
          {imageUrl ? (
            <img className={styles.feedProductImage} src={imageUrl} alt={product.voName} />
          ) : (
            <span className={styles.feedProductImageFallback}>
              <Icon icon="mdi:gift-outline" size={24} />
            </span>
          )}
          <div className={styles.feedProductBody}>
            <div className={styles.itemChipRow}>
              <span className={styles.itemChip}>{getProductTypeDisplay(product.voProductType)}</span>
              {product.voHasDiscount ? (
                <span className={`${styles.itemChip} ${styles.itemChipAccent}`}>
                  {t('discover.public.shopItemDiscount')}
                </span>
              ) : null}
            </div>
            <h3 className={styles.feedContentTitle}>{product.voName}</h3>
          </div>
        </div>
        <p className={styles.feedContentSummary}>{buildProductSummary(product, t)}</p>
        <span className={styles.feedMetaLine}>
          <Icon icon="mdi:carrot" size={16} />
          <span>{product.voPrice.toLocaleString()} {t('shop.currency.carrot')}</span>
        </span>
      </a>
    </article>
  );
}

export function PublicDiscoverFeed({
  forumPosts,
  documents,
  products,
  loadingForum,
  loadingDocs,
  loadingShop,
  forumError,
  docsError,
  shopError,
  displayTimeZone,
  locale,
  onReload,
  onOpenPost,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery,
  onOpenDocument,
  onOpenProduct,
  onOpenForum,
  onOpenDocs,
  onOpenShop,
}: PublicDiscoverFeedProps) {
  const { t } = useTranslation();
  const feedItems = useMemo(
    () => buildDiscoverFeedItems(forumPosts, documents, products),
    [documents, forumPosts, products]
  );
  const feedErrorCount = [forumError, docsError, shopError].filter(Boolean).length;
  const feedIsLoading = loadingForum || loadingDocs || loadingShop;
  const feedState = feedIsLoading
    ? 'loading'
    : feedErrorCount > 0
      ? 'partial'
      : feedItems.length === 0
        ? 'empty'
        : 'ready';
  const feedStatusLabel = feedIsLoading
    ? t('discover.public.streamStateLoading')
    : feedErrorCount > 0
      ? t('discover.public.streamStatePartial')
      : feedItems.length === 0
        ? t('discover.public.streamStateEmpty')
        : t('discover.public.streamStateReady');
  const feedStatusDescription = feedIsLoading
    ? t('discover.public.streamStateLoadingDescription')
    : feedErrorCount > 0
      ? t('discover.public.streamStatePartialDescription')
      : feedItems.length === 0
        ? t('discover.public.streamEmptyDescription')
        : t('discover.public.streamStateReadyDescription', { count: feedItems.length });

  return (
    <section className={`${styles.sectionCard} ${styles.streamSection}`}>
      <div className={styles.streamHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.streamKicker}>{t('discover.public.streamKicker')}</p>
          <h2 className={styles.sectionTitle}>{t('discover.public.streamTitle')}</h2>
          <p className={styles.sectionDescription}>{t('discover.public.streamDescription')}</p>
        </div>
        <div className={styles.streamStatusPanel} data-state={feedState}>
          <span className={styles.streamStatusLabel}>{feedStatusLabel}</span>
          <span className={styles.streamStatusText}>{feedStatusDescription}</span>
        </div>
      </div>

      {feedItems.length > 0 ? (
        <>
          {feedErrorCount > 0 && (
            <div className={styles.inlineNotice} data-tone="warning">
              <Icon icon="mdi:alert-circle-outline" size={16} />
              <span>{t('discover.public.streamPartialNotice')}</span>
              <button type="button" className={styles.inlineNoticeAction} onClick={onReload}>
                {t('common.retry')}
              </button>
            </div>
          )}

          <div className={styles.feedGrid}>
            {feedItems.map((item) => (
              <DiscoverFeedItemCard
                key={item.key}
                item={item}
                displayTimeZone={displayTimeZone}
                locale={locale}
                onOpenPost={onOpenPost}
                onOpenTag={onOpenTag}
                onOpenQuestion={onOpenQuestion}
                onOpenPoll={onOpenPoll}
                onOpenLottery={onOpenLottery}
                onOpenDocument={onOpenDocument}
                onOpenProduct={onOpenProduct}
              />
            ))}
          </div>

          <div className={styles.streamFooter}>
            <div className={styles.streamBoundaryPanel}>
              <span className={styles.streamBoundaryIcon}>
                <Icon icon="mdi:message-heart-outline" size={20} />
              </span>
              <div className={styles.streamBoundaryCopy}>
                <h3 className={styles.streamBoundaryTitle}>{t('discover.public.streamInteractionTitle')}</h3>
                <p className={styles.streamBoundaryDescription}>{t('discover.public.streamInteractionDescription')}</p>
              </div>
            </div>
            <div className={styles.streamActionRow}>
              <a
                className={styles.secondaryButton}
                href={buildPublicForumPath({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
                onClick={(event) => handleFeedLinkClick(event, onOpenForum)}
              >
                <Icon icon="mdi:forum-outline" size={18} />
                <span>{t('discover.public.viewAllForum')}</span>
              </a>
              <a
                className={styles.secondaryButton}
                href={buildPublicDocsPath({ kind: 'list' })}
                onClick={(event) => handleFeedLinkClick(event, onOpenDocs)}
              >
                <Icon icon="mdi:file-document-outline" size={18} />
                <span>{t('discover.public.viewAllDocs')}</span>
              </a>
              <a
                className={styles.secondaryButton}
                href={buildPublicShopPath({ kind: 'home' })}
                onClick={(event) => handleFeedLinkClick(event, onOpenShop)}
              >
                <Icon icon="mdi:storefront-outline" size={18} />
                <span>{t('discover.public.viewAllShop')}</span>
              </a>
            </div>
          </div>
        </>
      ) : feedIsLoading ? (
        <FeedStatusCard
          tone="loading"
          title={t('discover.public.streamLoadingTitle')}
          description={t('discover.public.streamLoadingDescription')}
        />
      ) : feedErrorCount > 0 ? (
        <FeedStatusCard
          tone="error"
          title={t('discover.public.streamLoadFailedTitle')}
          description={t('discover.public.streamLoadFailedDescription')}
          primaryAction={{
            label: t('common.retry'),
            onClick: onReload
          }}
        />
      ) : (
        <FeedStatusCard
          tone="empty"
          title={t('discover.public.streamEmptyTitle')}
          description={t('discover.public.streamEmptyDescription')}
        />
      )}
    </section>
  );
}
