export interface WikiDocumentVo {
  voId: number;
  voTitle: string;
  voSlug: string;
  voSummary?: string | null;
  voCoverAttachmentId?: number | null;
  voParentId?: number | null;
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
  voId: number;
  voDocumentId: number;
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
  voCreateId: number;
}

export interface WikiDocumentTreeNodeVo {
  voId: number;
  voTitle: string;
  voSlug: string;
  voParentId?: number | null;
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
  parentId?: number;
  includeDeleted?: boolean;
  deletedOnly?: boolean;
}

export interface CreateWikiDocumentRequest {
  title: string;
  slug?: string;
  summary?: string;
  markdownContent: string;
  parentId?: number | null;
  sort?: number;
  coverAttachmentId?: number | null;
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
  parentId?: number | null;
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
