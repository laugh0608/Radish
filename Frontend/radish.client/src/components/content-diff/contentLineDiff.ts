export type ContentDiffLineKind = 'equal' | 'insert' | 'delete' | 'change';

export interface ContentDiffCell {
  lineNumber: number;
  text: string;
}

export interface ContentDiffRow {
  kind: ContentDiffLineKind;
  before: ContentDiffCell | null;
  after: ContentDiffCell | null;
}

type DiffOperation = {
  kind: 'equal' | 'insert' | 'delete';
  text: string;
};

const MAX_DIFF_MATRIX_CELLS = 250_000;

function splitLines(value: string): string[] {
  if (!value) {
    return [];
  }

  return value.replace(/\r\n?/g, '\n').split('\n');
}

function buildMiddleOperations(before: string[], after: string[]): DiffOperation[] {
  if (before.length === 0) {
    return after.map((text) => ({ kind: 'insert', text }));
  }

  if (after.length === 0) {
    return before.map((text) => ({ kind: 'delete', text }));
  }

  if (before.length * after.length > MAX_DIFF_MATRIX_CELLS) {
    return [
      ...before.map((text): DiffOperation => ({ kind: 'delete', text })),
      ...after.map((text): DiffOperation => ({ kind: 'insert', text })),
    ];
  }

  const lengths = Array.from(
    { length: before.length + 1 },
    () => new Uint32Array(after.length + 1),
  );

  for (let beforeIndex = before.length - 1; beforeIndex >= 0; beforeIndex -= 1) {
    for (let afterIndex = after.length - 1; afterIndex >= 0; afterIndex -= 1) {
      lengths[beforeIndex][afterIndex] = before[beforeIndex] === after[afterIndex]
        ? lengths[beforeIndex + 1][afterIndex + 1] + 1
        : Math.max(lengths[beforeIndex + 1][afterIndex], lengths[beforeIndex][afterIndex + 1]);
    }
  }

  const operations: DiffOperation[] = [];
  let beforeIndex = 0;
  let afterIndex = 0;

  while (beforeIndex < before.length && afterIndex < after.length) {
    if (before[beforeIndex] === after[afterIndex]) {
      operations.push({ kind: 'equal', text: before[beforeIndex] });
      beforeIndex += 1;
      afterIndex += 1;
      continue;
    }

    if (lengths[beforeIndex + 1][afterIndex] >= lengths[beforeIndex][afterIndex + 1]) {
      operations.push({ kind: 'delete', text: before[beforeIndex] });
      beforeIndex += 1;
    } else {
      operations.push({ kind: 'insert', text: after[afterIndex] });
      afterIndex += 1;
    }
  }

  while (beforeIndex < before.length) {
    operations.push({ kind: 'delete', text: before[beforeIndex] });
    beforeIndex += 1;
  }

  while (afterIndex < after.length) {
    operations.push({ kind: 'insert', text: after[afterIndex] });
    afterIndex += 1;
  }

  return operations;
}

function buildOperations(before: string[], after: string[]): DiffOperation[] {
  let prefixLength = 0;
  const commonLength = Math.min(before.length, after.length);
  while (prefixLength < commonLength && before[prefixLength] === after[prefixLength]) {
    prefixLength += 1;
  }

  let suffixLength = 0;
  while (
    suffixLength < commonLength - prefixLength
    && before[before.length - suffixLength - 1] === after[after.length - suffixLength - 1]
  ) {
    suffixLength += 1;
  }

  const prefix = before.slice(0, prefixLength).map((text): DiffOperation => ({ kind: 'equal', text }));
  const middleBefore = before.slice(prefixLength, before.length - suffixLength);
  const middleAfter = after.slice(prefixLength, after.length - suffixLength);
  const suffix = before.slice(before.length - suffixLength).map((text): DiffOperation => ({ kind: 'equal', text }));

  return [...prefix, ...buildMiddleOperations(middleBefore, middleAfter), ...suffix];
}

function appendChangedRows(
  rows: ContentDiffRow[],
  deleted: ContentDiffCell[],
  inserted: ContentDiffCell[],
): void {
  const changedCount = Math.max(deleted.length, inserted.length);
  for (let index = 0; index < changedCount; index += 1) {
    const before = deleted[index] ?? null;
    const after = inserted[index] ?? null;
    rows.push({
      kind: before && after ? 'change' : before ? 'delete' : 'insert',
      before,
      after,
    });
  }
}

export function buildContentLineDiff(beforeValue: string, afterValue: string): ContentDiffRow[] {
  const operations = buildOperations(splitLines(beforeValue), splitLines(afterValue));
  const rows: ContentDiffRow[] = [];
  let beforeLineNumber = 1;
  let afterLineNumber = 1;
  let deleted: ContentDiffCell[] = [];
  let inserted: ContentDiffCell[] = [];

  const flushChanges = () => {
    appendChangedRows(rows, deleted, inserted);
    deleted = [];
    inserted = [];
  };

  for (const operation of operations) {
    if (operation.kind === 'equal') {
      flushChanges();
      rows.push({
        kind: 'equal',
        before: { lineNumber: beforeLineNumber, text: operation.text },
        after: { lineNumber: afterLineNumber, text: operation.text },
      });
      beforeLineNumber += 1;
      afterLineNumber += 1;
      continue;
    }

    if (operation.kind === 'delete') {
      deleted.push({ lineNumber: beforeLineNumber, text: operation.text });
      beforeLineNumber += 1;
    } else {
      inserted.push({ lineNumber: afterLineNumber, text: operation.text });
      afterLineNumber += 1;
    }
  }

  flushChanges();
  return rows;
}
