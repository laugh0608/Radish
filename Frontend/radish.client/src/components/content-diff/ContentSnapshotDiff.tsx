import { useMemo, type ReactNode } from 'react';
import { Icon } from '@radish/ui/icon';
import { buildContentLineDiff, type ContentDiffCell, type ContentDiffRow } from './contentLineDiff';
import styles from './ContentSnapshotDiff.module.css';

export interface ContentSnapshotField {
  key: string;
  label: string;
  value: string;
}

export interface ContentSnapshot {
  content: string;
  fields: ContentSnapshotField[];
}

interface ContentSnapshotDiffProps {
  before: ContentSnapshot | null;
  after: ContentSnapshot | null;
  beforeLabel: string;
  afterLabel: string;
  ariaLabel: string;
  emptyText: string;
  beforeUnavailableText?: string;
  afterUnavailableText?: string;
  loadingBefore?: boolean;
  loadingAfter?: boolean;
  loadingText?: string;
  onRetryBefore?: () => void;
  onRetryAfter?: () => void;
  retryLabel?: string;
}

interface FieldComparison {
  key: string;
  label: string;
  before: string;
  after: string;
  changed: boolean;
}

function buildFieldComparisons(before: ContentSnapshot | null, after: ContentSnapshot | null): FieldComparison[] {
  const beforeFields = new Map(before?.fields.map((field) => [field.key, field]) ?? []);
  const afterFields = new Map(after?.fields.map((field) => [field.key, field]) ?? []);
  const keys = Array.from(new Set([...beforeFields.keys(), ...afterFields.keys()]));

  return keys.map((key) => {
    const beforeField = beforeFields.get(key);
    const afterField = afterFields.get(key);
    const beforeValue = beforeField?.value ?? '';
    const afterValue = afterField?.value ?? '';
    return {
      key,
      label: beforeField?.label ?? afterField?.label ?? key,
      before: beforeValue,
      after: afterValue,
      changed: beforeValue !== afterValue,
    };
  });
}

function SnapshotUnavailable({
  text,
  loading,
  loadingText,
  retryLabel,
  onRetry,
}: {
  text: string;
  loading: boolean;
  loadingText?: string;
  retryLabel?: string;
  onRetry?: () => void;
}) {
  return (
    <div className={styles.unavailable} role={loading ? 'status' : 'alert'}>
      <Icon icon={loading ? 'mdi:progress-clock' : 'mdi:alert-circle-outline'} size={20} />
      <span>{loading ? loadingText ?? text : text}</span>
      {!loading && onRetry && retryLabel ? (
        <button type="button" onClick={onRetry}>{retryLabel}</button>
      ) : null}
    </div>
  );
}

function renderLineCell(cell: ContentDiffCell | null, kind: ContentDiffRow['kind'], side: 'before' | 'after'): ReactNode {
  const sideChanged = side === 'before'
    ? kind === 'delete' || kind === 'change'
    : kind === 'insert' || kind === 'change';
  const className = sideChanged
    ? side === 'before' ? styles.lineDeleted : styles.lineInserted
    : styles.lineEqual;

  return (
    <div className={`${styles.lineCell} ${className}`}>
      <span className={styles.lineNumber}>{cell?.lineNumber ?? ''}</span>
      <code>{cell?.text || ' '}</code>
    </div>
  );
}

export function ContentSnapshotDiff({
  before,
  after,
  beforeLabel,
  afterLabel,
  ariaLabel,
  emptyText,
  beforeUnavailableText = emptyText,
  afterUnavailableText = emptyText,
  loadingBefore = false,
  loadingAfter = false,
  loadingText,
  onRetryBefore,
  onRetryAfter,
  retryLabel,
}: ContentSnapshotDiffProps) {
  const fieldComparisons = useMemo(() => buildFieldComparisons(before, after), [after, before]);
  const rows = useMemo(
    () => buildContentLineDiff(before?.content ?? '', after?.content ?? ''),
    [after?.content, before?.content],
  );

  if (!before && !after && !loadingBefore && !loadingAfter) {
    return <div className={styles.empty}>{emptyText}</div>;
  }

  return (
    <section className={styles.diff} aria-label={ariaLabel}>
      <div className={styles.sideBySide}>
        <div className={styles.sideHeader}>{beforeLabel}</div>
        <div className={styles.sideHeader}>{afterLabel}</div>

        <div className={styles.fieldPanel}>
          {!before ? (
            <SnapshotUnavailable
              text={beforeUnavailableText}
              loading={loadingBefore}
              loadingText={loadingText}
              retryLabel={retryLabel}
              onRetry={onRetryBefore}
            />
          ) : fieldComparisons.map((field) => (
            <div key={field.key} className={field.changed ? styles.fieldChangedBefore : styles.field}>
              <span>{field.label}</span>
              <strong>{field.before || '—'}</strong>
            </div>
          ))}
        </div>
        <div className={styles.fieldPanel}>
          {!after ? (
            <SnapshotUnavailable
              text={afterUnavailableText}
              loading={loadingAfter}
              loadingText={loadingText}
              retryLabel={retryLabel}
              onRetry={onRetryAfter}
            />
          ) : fieldComparisons.map((field) => (
            <div key={field.key} className={field.changed ? styles.fieldChangedAfter : styles.field}>
              <span>{field.label}</span>
              <strong>{field.after || '—'}</strong>
            </div>
          ))}
        </div>

        {before || after ? rows.map((row, index) => (
          <div className={styles.sideRow} key={`${index}-${row.before?.lineNumber ?? 'x'}-${row.after?.lineNumber ?? 'x'}`}>
            {renderLineCell(row.before, row.kind, 'before')}
            {renderLineCell(row.after, row.kind, 'after')}
          </div>
        )) : null}
      </div>

      <div className={styles.unified}>
        <div className={styles.unifiedVersionPair}>
          <span>{beforeLabel}</span>
          <Icon icon="mdi:arrow-right" size={16} />
          <span>{afterLabel}</span>
        </div>

        {!before ? (
          <SnapshotUnavailable
            text={beforeUnavailableText}
            loading={loadingBefore}
            loadingText={loadingText}
            retryLabel={retryLabel}
            onRetry={onRetryBefore}
          />
        ) : null}
        {!after ? (
          <SnapshotUnavailable
            text={afterUnavailableText}
            loading={loadingAfter}
            loadingText={loadingText}
            retryLabel={retryLabel}
            onRetry={onRetryAfter}
          />
        ) : null}

        {before || after ? (
          <>
            <div className={styles.unifiedFields}>
              {fieldComparisons.map((field) => field.changed ? (
                <div className={styles.unifiedField} key={field.key}>
                  <span>{field.label}</span>
                  <code className={styles.unifiedDeleted}>− {field.before || '—'}</code>
                  <code className={styles.unifiedInserted}>+ {field.after || '—'}</code>
                </div>
              ) : (
                <div className={styles.unifiedField} key={field.key}>
                  <span>{field.label}</span>
                  <code>{field.after || '—'}</code>
                </div>
              ))}
            </div>
            <div className={styles.unifiedLines}>
              {rows.flatMap((row, index) => {
                if (row.kind === 'equal') {
                  return (
                    <div className={`${styles.unifiedLine} ${styles.lineEqual}`} key={`equal-${index}`}>
                      <span className={styles.linePrefix}> </span>
                      <span className={styles.lineNumber}>{row.after?.lineNumber ?? ''}</span>
                      <code>{row.after?.text || ' '}</code>
                    </div>
                  );
                }

                const changedLines: ReactNode[] = [];
                if (row.before) {
                  changedLines.push(
                    <div className={`${styles.unifiedLine} ${styles.lineDeleted}`} key={`before-${index}`}>
                      <span className={styles.linePrefix}>−</span>
                      <span className={styles.lineNumber}>{row.before.lineNumber}</span>
                      <code>{row.before.text || ' '}</code>
                    </div>,
                  );
                }
                if (row.after) {
                  changedLines.push(
                    <div className={`${styles.unifiedLine} ${styles.lineInserted}`} key={`after-${index}`}>
                      <span className={styles.linePrefix}>+</span>
                      <span className={styles.lineNumber}>{row.after.lineNumber}</span>
                      <code>{row.after.text || ' '}</code>
                    </div>,
                  );
                }
                return changedLines;
              })}
            </div>
          </>
        ) : null}
      </div>
    </section>
  );
}
