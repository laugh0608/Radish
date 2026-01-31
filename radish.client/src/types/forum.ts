/**
 * 论坛相关的 TypeScript 类型定义
 * 直接使用后端 Vo 字段名，不进行映射
 */

/**
 * 分页模型
 */
export interface PageModel<T> {
  /** 当前页码 */
  page: number;
  /** 每页数量 */
  pageSize: number;
  /** 总数据量 */
  dataCount: number;
  /** 总页数 */
  pageCount: number;
  /** 数据列表 */
  data: T[];
}

/**
 * 分类 Vo
 */
export interface Category {
  voId: number;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voParentId?: number | null;
  voLevel?: number;
  voOrderSort?: number;
  voPostCount?: number;
  voIsEnabled?: boolean;
  voCreateTime?: string;
  voCreateBy?: string | null;
}

/**
 * 帖子列表项 Vo
 */
export interface PostItem {
  voId: number;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: number;
  voAuthorName?: string | null;
  voViewCount?: number;
  voLikeCount?: number;
  voCommentCount?: number;
  voIsTop?: boolean;
  voIsEssence?: boolean;
  voIsLocked?: boolean;
  voCreateTime?: string;
  voUpdateTime?: string;
}

/**
 * 帖子详情 Vo
 */
export interface PostDetail {
  voId: number;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voContent: string;
  voContentType?: string;
  voCoverImage?: string | null;
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: number;
  voAuthorName?: string | null;
  voTags?: string;
  voTagNames?: string[];
  voViewCount?: number;
  voLikeCount?: number;
  voCommentCount?: number;
  voIsTop?: boolean;
  voIsEssence?: boolean;
  voIsLocked?: boolean;
  voCreateTime?: string;
  voUpdateTime?: string;
}

/**
 * 评论节点 Vo（树形结构）
 */
export interface CommentNode {
  voId: number;
  voPostId: number;
  voContent: string;
  voAuthorId: number;
  voAuthorName: string;
  voParentId?: number | null;
  voRootId?: number | null;
  voReplyToUserId?: number | null;
  voReplyToUserName?: string | null;
  voLevel?: number;
  voLikeCount?: number;
  voReplyCount?: number;
  voIsTop?: boolean;
  voIsLiked?: boolean;
  voCreateTime?: string;
  voUpdateTime?: string;
  voChildren?: CommentNode[];
  voChildrenTotal?: number;
  voIsGodComment?: boolean;
  voIsSofa?: boolean;
  voHighlightRank?: number;
}

/**
 * 神评/沙发高亮记录 Vo
 */
export interface CommentHighlight {
  voId: number;
  voPostId: number;
  voCommentId: number;
  voParentCommentId: number | null;
  voHighlightType: number;
  voStatDate: string;
  voLikeCount: number;
  voRank: number;
  voContentSnapshot: string | null;
  voAuthorId: number;
  voAuthorName: string;
  voIsCurrent: boolean;
  voCreateTime: string;
}

/**
 * 评论点赞操作结果
 * 注意：后端返回的字段带 vo 前缀
 */
export interface CommentLikeResult {
  voIsLiked: boolean;
  voLikeCount: number;
}

/**
 * 帖子点赞操作结果
 * 注意：后端返回的字段带 vo 前缀
 */
export interface PostLikeResult {
  voIsLiked: boolean;
  voLikeCount: number;
}

/**
 * 发布帖子请求
 */
export interface PublishPostRequest {
  title: string;
  content: string;
  categoryId: number;
  tagNames?: string[];
}

/**
 * 编辑帖子请求
 */
export interface UpdatePostRequest {
  postId: number;
  title: string;
  content: string;
  categoryId?: number;
}

/**
 * 创建评论请求
 */
export interface CreateCommentRequest {
  postId: number;
  content: string;
  parentId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
}
