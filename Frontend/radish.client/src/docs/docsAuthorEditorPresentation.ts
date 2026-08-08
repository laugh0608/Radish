import type { WikiAuthorDocumentVo } from '@radish/http';
import type { EditorDraft } from '@/apps/wiki/wikiApp.helpers';

export interface DocsAuthorOutlineItem {
  id: string;
  level: number;
  text: string;
}

const DRAFT_FIELDS: Array<keyof EditorDraft> = [
  'title',
  'slug',
  'summary',
  'markdownContent',
  'parentId',
  'sort',
  'coverAttachmentId',
  'changeSummary',
  'visibility',
  'allowedRoles',
  'allowedPermissions',
];

export function areDocsAuthorDraftsEqual(left: EditorDraft, right: EditorDraft): boolean {
  return DRAFT_FIELDS.every((field) => left[field] === right[field]);
}

export function getDocsAuthorOutline(markdown: string): DocsAuthorOutlineItem[] {
  const items: DocsAuthorOutlineItem[] = [];
  const occurrenceBySlug = new Map<string, number>();
  const headingPattern = /^(#{1,3})\s+(.+?)\s*#*$/gm;

  for (const match of markdown.matchAll(headingPattern)) {
    const text = match[2]
      .replace(/\[([^\]]+)]\([^)]*\)/g, '$1')
      .replace(/[*_`~]/g, '')
      .trim();
    if (!text) {
      continue;
    }

    const baseSlug = text
      .toLocaleLowerCase()
      .replace(/[^\p{Letter}\p{Number}]+/gu, '-')
      .replace(/^-+|-+$/g, '') || `heading-${items.length + 1}`;
    const occurrence = (occurrenceBySlug.get(baseSlug) ?? 0) + 1;
    occurrenceBySlug.set(baseSlug, occurrence);
    items.push({
      id: occurrence === 1 ? baseSlug : `${baseSlug}-${occurrence}`,
      level: match[1].length,
      text,
    });
  }

  return items;
}

export function getDocsAuthorInitial(value: string | null | undefined, fallback: string): string {
  const normalized = value?.trim();
  return normalized ? Array.from(normalized)[0] : fallback;
}

export function countDocsAuthorMarkdownCharacters(markdown: string): number {
  return Array.from(markdown.replace(/\s+/g, '')).length;
}

export function countOwnedDocsAuthorDocuments(documents: WikiAuthorDocumentVo[]): number {
  return documents.filter((document) => document.voAuthorRole.toLowerCase() === 'owner').length;
}

export function countCollaboratingDocsAuthorDocuments(documents: WikiAuthorDocumentVo[]): number {
  return documents.filter((document) => document.voAuthorRole.toLowerCase() !== 'owner').length;
}

export function pickDocsAuthorPreviewDocument(documents: WikiAuthorDocumentVo[]): WikiAuthorDocumentVo | null {
  return documents[0] ?? null;
}
