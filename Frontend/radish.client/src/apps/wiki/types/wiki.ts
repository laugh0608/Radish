import type { LongId } from '@/api/user';

export interface WikiDocumentVo {
  voId: LongId;
  voTitle: string;
  voSlug: string;
  voSummary?: string | null;
  voCoverAttachmentId?: LongId | null;
  voParentId?: LongId | null;
  voSort: number;
  voStatus: number;
  voVisibility: number;
  voAllowedRoles: string[];
  voAllowedPermissions: string[];
  voSourceType: string;
  voSourcePath?: string | null;
  voVersion: number;
  voPublishedAt?: string | null;
  voIsDeleted: boolean;
  voDeletedAt?: string | null;
  voDeletedBy?: string | null;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface WikiDocumentDetailVo extends WikiDocumentVo {
  voMarkdownContent: string;
}

export interface WikiDocumentRevisionItemVo {
  voId: LongId;
  voDocumentId: LongId;
  voVersion: number;
  voTitle: string;
  voChangeSummary?: string | null;
  voSourceType: string;
  voCreateTime: string;
  voCreateBy: string;
  voIsCurrent: boolean;
}

export interface WikiDocumentRevisionDetailVo extends WikiDocumentRevisionItemVo {
  voMarkdownContent: string;
  voCreateId: LongId;
}

export interface WikiDocumentTreeNodeVo {
  voId: LongId;
  voTitle: string;
  voSlug: string;
  voParentId?: LongId | null;
  voSort: number;
  voStatus: number;
  voVisibility: number;
  voChildren: WikiDocumentTreeNodeVo[];
}

export interface WikiPageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

export interface WikiListQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: number;
  parentId?: LongId;
  includeDeleted?: boolean;
  deletedOnly?: boolean;
}

export interface CreateWikiDocumentRequest {
  title: string;
  slug?: string;
  summary?: string;
  markdownContent: string;
  parentId?: LongId | null;
  sort?: number;
  coverAttachmentId?: LongId | null;
  visibility?: number;
  allowedRoles?: string[];
  allowedPermissions?: string[];
}

export interface UpdateWikiDocumentRequest extends CreateWikiDocumentRequest {
  changeSummary?: string;
}

export interface ImportWikiMarkdownRequest {
  file: File;
  slug?: string;
  summary?: string;
  parentId?: LongId | null;
  sort?: number;
  publishAfterImport?: boolean;
  visibility?: number;
  allowedRoles?: string[];
  allowedPermissions?: string[];
}

export const WikiDocumentStatus = {
  Draft: 0,
  Published: 1,
  Archived: 2,
} as const;

export type WikiDocumentStatusValue = typeof WikiDocumentStatus[keyof typeof WikiDocumentStatus];

export const WikiDocumentVisibility = {
  Public: 1,
  Authenticated: 2,
  Restricted: 3,
} as const;

export type WikiDocumentVisibilityValue = typeof WikiDocumentVisibility[keyof typeof WikiDocumentVisibility];
