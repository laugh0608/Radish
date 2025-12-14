/**
 * 论坛相关的 TypeScript 类型定义
 */

/**
 * 分类
 */
export interface Category {
  id: number;
  name: string;
  slug: string;
  description?: string | null;
  parentId?: number | null;
}

/**
 * 帖子列表项
 */
export interface PostItem {
  id: number;
  title: string;
  summary?: string | null;
  categoryId: number;
  authorName?: string | null;
  createTime?: string;
  viewCount?: number;
}

/**
 * 帖子详情
 */
export interface PostDetail extends PostItem {
  content: string;
  categoryName?: string;
  tagNames?: string[];
}

/**
 * 评论节点（树形结构）
 */
export interface CommentNode {
  id: number;
  postId: number;
  content: string;
  authorId: number;
  authorName: string;
  parentId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
  createTime?: string;
  children?: CommentNode[];
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
 * 创建评论请求
 */
export interface CreateCommentRequest {
  postId: number;
  content: string;
  parentId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
}
