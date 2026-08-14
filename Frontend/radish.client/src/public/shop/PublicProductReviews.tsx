import { useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { redirectToLogin } from '@/services/auth';
import { buildShopProductReviewReturnPath } from '@/services/authReturnPath';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import type { PublicProductReviewController } from './usePublicProductReviews';
import styles from './PublicProductReviews.module.css';

interface ProductReviewSummaryProps {
  controller: PublicProductReviewController;
}

interface PublicProductReviewsProps {
  productId: string;
  loggedIn: boolean;
  authReady: boolean;
  reviewIntent: boolean;
  controller: PublicProductReviewController;
  onReportReview: (reviewId: string) => void;
}

function RatingStars({ rating, label }: { rating: number; label: string }) {
  return (
    <span className={styles.ratingStars} aria-label={label}>
      {[1, 2, 3, 4, 5].map((value) => (
        <Icon
          key={value}
          icon={value <= Math.round(rating) ? 'mdi:star' : 'mdi:star-outline'}
          size={17}
        />
      ))}
    </span>
  );
}

function formatReviewTime(value: string, timeZone: string): string {
  return formatDateTimeByTimeZone(value, timeZone);
}

export function ProductReviewCompactSummary({ controller }: ProductReviewSummaryProps) {
  const { t } = useTranslation();
  const summary = controller.summary;

  if (!summary && controller.reviewsLoading) {
    return (
      <div className={styles.compactSummary} data-state="loading">
        <Icon icon="mdi:progress-clock" size={18} />
        <span>{t('shop.review.summaryLoading')}</span>
      </div>
    );
  }

  if (!summary) {
    return (
      <div className={styles.compactSummary} data-state="unavailable">
        <Icon icon="mdi:star-off-outline" size={18} />
        <span>{t('shop.review.summaryUnavailable')}</span>
      </div>
    );
  }

  if (summary.voReviewCount === 0) {
    return (
      <div className={styles.compactSummary} data-state="empty">
        <RatingStars rating={0} label={t('shop.review.zeroRatingLabel')} />
        <span>{t('shop.review.zeroSummary')}</span>
      </div>
    );
  }

  return (
    <div className={styles.compactSummary} data-state="ready">
      <strong>{summary.voAverageRating.toFixed(1)}</strong>
      <RatingStars
        rating={summary.voAverageRating}
        label={t('shop.review.ratingLabel', { rating: summary.voAverageRating.toFixed(1) })}
      />
      <span>{t('shop.review.verifiedCount', { count: summary.voReviewCount })}</span>
    </div>
  );
}

function RatingDistribution({ controller }: ProductReviewSummaryProps) {
  const { t } = useTranslation();
  const summary = controller.summary;
  if (!summary || summary.voReviewCount === 0) {
    return null;
  }

  const rows = [
    [5, summary.voFiveStarCount],
    [4, summary.voFourStarCount],
    [3, summary.voThreeStarCount],
    [2, summary.voTwoStarCount],
    [1, summary.voOneStarCount],
  ] as const;

  return (
    <div className={styles.distribution} aria-label={t('shop.review.distributionLabel')}>
      {rows.map(([rating, count]) => {
        const percentage = Math.round((count / summary.voReviewCount) * 100);
        return (
          <div className={styles.distributionRow} key={rating}>
            <span>{t('shop.review.starCount', { count: rating })}</span>
            <span className={styles.distributionTrack} aria-hidden="true">
              <span style={{ width: `${percentage}%` }} />
            </span>
            <strong>{count}</strong>
          </div>
        );
      })}
    </div>
  );
}

function ProductReviewEditor({ controller }: ProductReviewSummaryProps) {
  const { t } = useTranslation();

  return (
    <section className={styles.editor} aria-label={t('shop.review.editorTitle')}>
      <div className={styles.editorHeader}>
        <div>
          <p className={styles.eyebrow}>{t('shop.review.verifiedPurchase')}</p>
          <h3>{controller.myReviewState?.voReview ? t('shop.review.editTitle') : t('shop.review.createTitle')}</h3>
        </div>
        <span className={controller.dirty ? styles.dirtyBadge : styles.savedBadge}>
          {controller.dirty ? t('shop.review.unsaved') : t('shop.review.draftClean')}
        </span>
      </div>

      <fieldset className={styles.ratingFieldset} disabled={controller.mutationBusy || controller.myReviewLoading}>
        <legend>{t('shop.review.ratingField')}</legend>
        <div className={styles.ratingButtons}>
          {[1, 2, 3, 4, 5].map((rating) => (
            <button
              type="button"
              key={rating}
              className={styles.ratingButton}
              data-selected={rating <= controller.draft.rating}
              aria-label={t('shop.review.selectRating', { count: rating })}
              onClick={() => controller.setDraftRating(rating)}
            >
              <Icon icon={rating <= controller.draft.rating ? 'mdi:star' : 'mdi:star-outline'} size={24} />
            </button>
          ))}
        </div>
      </fieldset>

      <label className={styles.commentField}>
        <span>{t('shop.review.commentField')}</span>
        <textarea
          value={controller.draft.comment}
          maxLength={500}
          rows={4}
          disabled={controller.mutationBusy || controller.myReviewLoading}
          placeholder={t('shop.review.commentPlaceholder')}
          onChange={(event) => controller.setDraftComment(event.target.value)}
        />
        <span className={styles.characterCount}>{controller.draft.comment.length} / 500</span>
      </label>

      {controller.versionConflict ? (
        <div className={styles.conflictNotice} role="alert">
          <div>
            <strong>{t('shop.review.conflictTitle')}</strong>
            <p>{t('shop.review.conflictDescription')}</p>
          </div>
          <button type="button" onClick={controller.refreshAfterConflict} disabled={controller.myReviewLoading}>
            {t('shop.review.refreshAuthority')}
          </button>
        </div>
      ) : null}

      {controller.mutationError ? (
        <p className={styles.editorError} role="alert">{controller.mutationError}</p>
      ) : null}

      <div className={styles.editorActions}>
        {controller.myReviewState?.voReview ? (
          <button
            type="button"
            className={styles.dangerButton}
            onClick={() => void controller.removeReview()}
            disabled={controller.mutationBusy || controller.myReviewLoading}
          >
            {t('shop.review.deleteAction')}
          </button>
        ) : null}
        <button type="button" className={styles.secondaryButton} onClick={controller.closeEditor} disabled={controller.mutationBusy}>
          {t('common.cancel')}
        </button>
        <button
          type="button"
          className={styles.primaryButton}
          onClick={() => void controller.submitReview()}
          disabled={controller.mutationBusy || controller.myReviewLoading || controller.draft.rating === 0 || controller.myReviewState?.voCanReview !== true}
        >
          <Icon icon={controller.mutationBusy ? 'mdi:progress-clock' : 'mdi:check'} size={18} />
          {controller.mutationBusy ? t('shop.review.saving') : t('shop.review.saveAction')}
        </button>
      </div>
    </section>
  );
}

export function PublicProductReviews({
  productId,
  loggedIn,
  authReady,
  reviewIntent,
  controller,
  onReportReview,
}: PublicProductReviewsProps) {
  const { t } = useTranslation();
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const handledReviewIntentRef = useRef<string | null>(null);
  const summary = controller.summary;
  useEffect(() => {
    if (!reviewIntent) {
      handledReviewIntentRef.current = null;
      return;
    }

    const intentKey = `review:${productId}`;
    if (
      handledReviewIntentRef.current === intentKey
      || !authReady
      || controller.myReviewLoading
      || (loggedIn && !controller.myReviewState && !controller.myReviewError)
    ) {
      return;
    }

    handledReviewIntentRef.current = intentKey;
    document.getElementById('reviews')?.scrollIntoView({ block: 'start' });
    if (loggedIn && controller.myReviewState?.voCanReview) {
      controller.openEditor();
    }
  }, [
    authReady,
    controller,
    loggedIn,
    productId,
    reviewIntent,
  ]);

  const handleStartReview = () => {
    if (!loggedIn) {
      redirectToLogin({
        returnPath: buildShopProductReviewReturnPath(productId),
      });
      return;
    }

    controller.openEditor();
  };

  return (
    <section className={styles.reviewSection} id="reviews" aria-labelledby="product-review-title">
      <div className={styles.sectionHeader}>
        <div>
          <p className={styles.eyebrow}>{t('shop.review.kicker')}</p>
          <h2 id="product-review-title">{t('shop.review.title')}</h2>
          <p>{t('shop.review.description')}</p>
        </div>
        <div className={styles.reviewActionArea}>
          {!authReady || controller.myReviewLoading ? (
            <span className={styles.eligibilityNotice}>{t('shop.review.eligibilityLoading')}</span>
          ) : controller.myReviewError ? (
            <button type="button" className={styles.inlineButton} onClick={controller.retryMyReview}>
              {t('shop.review.retryEligibility')}
            </button>
          ) : loggedIn && controller.myReviewState && !controller.myReviewState.voCanReview ? (
            <span className={styles.eligibilityNotice}>
              {controller.myReviewUnavailableMessage || t('shop.review.purchaseRequired')}
            </span>
          ) : (
            <button type="button" className={styles.secondaryButton} onClick={handleStartReview}>
              <Icon icon="mdi:star-edit-outline" size={18} />
              {loggedIn && controller.myReviewState?.voReview
                ? t('shop.review.editAction')
                : loggedIn
                  ? t('shop.review.createAction')
                  : t('shop.review.loginAction')}
            </button>
          )}
        </div>
      </div>

      {controller.editorOpen ? <ProductReviewEditor controller={controller} /> : null}

      <div className={styles.summaryGrid}>
        <div className={styles.scorePanel}>
          {summary && summary.voReviewCount > 0 ? (
            <>
              <strong>{summary.voAverageRating.toFixed(1)}</strong>
              <RatingStars
                rating={summary.voAverageRating}
                label={t('shop.review.ratingLabel', { rating: summary.voAverageRating.toFixed(1) })}
              />
              <span>{t('shop.review.verifiedCount', { count: summary.voReviewCount })}</span>
            </>
          ) : summary ? (
            <>
              <Icon icon="mdi:star-outline" size={30} />
              <strong className={styles.emptyScore}>{t('shop.review.zeroSummary')}</strong>
              <span>{t('shop.review.zeroDescription')}</span>
            </>
          ) : controller.reviewsLoading ? (
            <>
              <Icon icon="mdi:progress-clock" size={30} />
              <strong className={styles.emptyScore}>{t('shop.review.summaryLoading')}</strong>
            </>
          ) : (
            <>
              <Icon icon="mdi:star-off-outline" size={30} />
              <strong className={styles.emptyScore}>{t('shop.review.summaryUnavailable')}</strong>
            </>
          )}
        </div>
        <RatingDistribution controller={controller} />
      </div>

      {controller.reviewsError ? (
        <div className={styles.reviewFeedback} data-state={controller.reviewsStale ? 'stale' : 'unavailable'} role="status">
          <div>
            <strong>{controller.reviewsStale ? t('shop.review.staleTitle') : t('shop.review.unavailableTitle')}</strong>
            <p>{controller.reviewsStale ? t('shop.review.staleDescription') : t('shop.review.loadFailed')}</p>
          </div>
          <button type="button" onClick={controller.retryReviews}>{t('common.retry')}</button>
        </div>
      ) : null}

      {controller.reviewsLoading && controller.reviews.length === 0 ? (
        <div className={styles.loadingState} role="status">
          <Icon icon="mdi:progress-clock" size={22} />
          <span>{t('shop.review.loading')}</span>
        </div>
      ) : controller.reviewsError && !controller.reviewsStale ? null : controller.reviews.length === 0 ? (
        <div className={styles.emptyState}>
          <Icon icon="mdi:comment-text-outline" size={28} />
          <strong>{t('shop.review.emptyTitle')}</strong>
          <span>{t('shop.review.emptyDescription')}</span>
        </div>
      ) : (
        <div className={styles.reviewList} aria-busy={controller.reviewsLoading}>
          {controller.reviews.map((review) => (
            <article className={styles.reviewItem} key={review.voId}>
              <div className={styles.reviewItemHeader}>
                <div>
                  <strong>{review.voAuthorDisplayName}</strong>
                  {review.voAuthorDisplayHandle?.trim() ? (
                    <span>{review.voAuthorDisplayHandle}</span>
                  ) : null}
                </div>
                <div className={styles.reviewMeta}>
                  {review.voVerifiedPurchase ? (
                    <span className={styles.verifiedBadge}>
                      <Icon icon="mdi:check-decagram-outline" size={15} />
                      {t('shop.review.verifiedPurchase')}
                    </span>
                  ) : null}
                  <time dateTime={review.voModifyTime || review.voCreateTime}>
                    {formatReviewTime(review.voModifyTime || review.voCreateTime, displayTimeZone)}
                  </time>
                </div>
              </div>
              <RatingStars
                rating={review.voRating}
                label={t('shop.review.ratingLabel', { rating: review.voRating })}
              />
              {review.voComment?.trim() ? (
                <p className={styles.reviewComment}>{review.voComment}</p>
              ) : (
                <p className={styles.reviewCommentEmpty}>{t('shop.review.commentEmpty')}</p>
              )}
              <button type="button" className={styles.reportButton} onClick={() => onReportReview(review.voId)}>
                <Icon icon="mdi:flag-outline" size={15} />
                {t('report.action')}
              </button>
            </article>
          ))}
        </div>
      )}

      {controller.totalPages > 1 ? (
        <nav className={styles.pagination} aria-label={t('shop.review.paginationLabel')}>
          <button
            type="button"
            disabled={controller.pageIndex <= 1 || controller.reviewsLoading}
            onClick={() => controller.setPageIndex(controller.pageIndex - 1)}
          >
            {t('shop.orders.previousPage')}
          </button>
          <span>{t('shop.review.pageInfo', { page: controller.pageIndex, total: controller.totalPages })}</span>
          <button
            type="button"
            disabled={controller.pageIndex >= controller.totalPages || controller.reviewsLoading}
            onClick={() => controller.setPageIndex(controller.pageIndex + 1)}
          >
            {t('shop.orders.nextPage')}
          </button>
        </nav>
      ) : null}
    </section>
  );
}
