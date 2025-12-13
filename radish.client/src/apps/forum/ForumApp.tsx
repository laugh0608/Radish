import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { parseApiResponse, type ApiResponse } from '@/api/client';
import { useUserStore } from '@/stores/userStore';

interface Category {
  id: number;
  name: string;
  slug: string;
  description?: string | null;
  parentId?: number | null;
}

interface PostItem {
  id: number;
  title: string;
  summary?: string | null;
  categoryId: number;
  authorName?: string | null;
  createTime?: string;
  viewCount?: number;
}

interface PostDetail extends PostItem {
  content: string;
  categoryName?: string;
  tagNames?: string[];
}

interface CommentNode {
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

interface PublishPostRequest {
  title: string;
  content: string;
  categoryId: number;
  tagNames?: string[];
}

interface CreateCommentRequest {
  postId: number;
  content: string;
  parentId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
}

interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

const defaultApiBase = 'https://localhost:5000';

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userName } = useUserStore();

  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [selectedPost, setSelectedPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);

  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);

  const [error, setError] = useState<string | null>(null);

  const [newPostTitle, setNewPostTitle] = useState('');
  const [newPostContent, setNewPostContent] = useState('');
  const [newCommentContent, setNewCommentContent] = useState('');

  const apiBaseUrl = useMemo(() => {
    const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
    return (configured ?? defaultApiBase).replace(/\/$/, '');
  }, []);

  const loggedIn = isAuthenticated();

  useEffect(() => {
    void loadCategories();
  }, [apiBaseUrl]);

  useEffect(() => {
    void loadPosts();
  }, [apiBaseUrl, selectedCategoryId]);

  async function loadCategories() {
    setLoadingCategories(true);
    setError(null);
    try {
      const url = `${apiBaseUrl}/api/v1/Category/GetTopCategories`;
      const response = await apiFetch(url);
      const json = await response.json() as ApiResponse<Category[]>;
      const parsed = parseApiResponse<Category[]>(json, t);
      if (!parsed.ok || !parsed.data) {
        throw new Error(parsed.message || '加载分类失败');
      }
      setCategories(parsed.data);
      if (parsed.data.length > 0 && selectedCategoryId == null) {
        setSelectedCategoryId(parsed.data[0].id);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingCategories(false);
    }
  }

  async function loadPosts() {
    setLoadingPosts(true);
    setError(null);
    try {
      const baseUrl = `${apiBaseUrl}/api/v1/Post/GetList`;
      const url = selectedCategoryId ? `${baseUrl}?categoryId=${selectedCategoryId}` : baseUrl;
      const response = await apiFetch(url);
      const json = await response.json() as ApiResponse<PostItem[]>;
      const parsed = parseApiResponse<PostItem[]>(json, t);
      if (!parsed.ok || !parsed.data) {
        throw new Error(parsed.message || '加载帖子失败');
      }
      setPosts(parsed.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingPosts(false);
    }
  }

  async function loadPostDetail(postId: number) {
    setLoadingPostDetail(true);
    setError(null);
    try {
      const url = `${apiBaseUrl}/api/v1/Post/GetById/${postId}`;
      const response = await apiFetch(url);
      const json = await response.json() as ApiResponse<PostDetail>;
      const parsed = parseApiResponse<PostDetail>(json, t);
      if (!parsed.ok || !parsed.data) {
        // 针对帖子不存在（例如返回 404）的情况给出友好提示
        if (!parsed.ok && (json.statusCode === 404 || json.statusCode === 410)) {
          setSelectedPost(null);
          setComments([]);
          setError(parsed.message || '帖子不存在或已被删除');
          return;
        }

        throw new Error(parsed.message || '加载帖子详情失败');
      }
      setSelectedPost(parsed.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingPostDetail(false);
    }
  }

  async function loadComments(postId: number) {
    setLoadingComments(true);
    setError(null);
    try {
      const url = `${apiBaseUrl}/api/v1/Comment/GetCommentTree?postId=${postId}`;
      const response = await apiFetch(url);
      const json = await response.json() as ApiResponse<CommentNode[]>;
      const parsed = parseApiResponse<CommentNode[]>(json, t);
      if (!parsed.ok || !parsed.data) {
        throw new Error(parsed.message || '加载评论失败');
      }
      setComments(parsed.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingComments(false);
    }
  }

  async function handleSelectPost(postId: number) {
    await loadPostDetail(postId);
    await loadComments(postId);
  }

  async function handlePublishPost() {
    if (!loggedIn) {
      setError('请先登录再发帖');
      return;
    }
    if (!selectedCategoryId) {
      setError('请先选择分类');
      return;
    }
    if (!newPostTitle.trim() || !newPostContent.trim()) {
      setError('帖子标题和内容不能为空');
      return;
    }

    setError(null);
    try {
      const url = `${apiBaseUrl}/api/v1/Post/Publish`;
      const body: PublishPostRequest = {
        title: newPostTitle.trim(),
        content: newPostContent.trim(),
        categoryId: selectedCategoryId,
        tagNames: []
      };
      const response = await apiFetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(body),
        withAuth: true
      });
      const json = await response.json() as ApiResponse<number>;
      const parsed = parseApiResponse<number>(json, t);
      if (!parsed.ok) {
        throw new Error(parsed.message || '发布帖子失败');
      }
      // 发布成功后重新加载帖子列表
      setNewPostTitle('');
      setNewPostContent('');
      await loadPosts();
      if (parsed.data) {
        await handleSelectPost(parsed.data);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  async function handleCreateComment() {
    if (!loggedIn) {
      setError('请先登录再发表评论');
      return;
    }
    if (!selectedPost) {
      setError('请先选择要评论的帖子');
      return;
    }
    if (!newCommentContent.trim()) {
      setError('评论内容不能为空');
      return;
    }

    setError(null);
    try {
      const url = `${apiBaseUrl}/api/v1/Comment/Create`;
      const body: CreateCommentRequest = {
        postId: selectedPost.id,
        content: newCommentContent.trim(),
        parentId: null,
        replyToUserId: null,
        replyToUserName: null
      };
      const response = await apiFetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(body),
        withAuth: true
      });
      const json = await response.json() as ApiResponse<number>;
      const parsed = parseApiResponse<number>(json, t);
      if (!parsed.ok) {
        throw new Error(parsed.message || '发表评论失败');
      }
      setNewCommentContent('');
      await loadComments(selectedPost.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  const handleLoginClick = () => {
    if (typeof window === 'undefined') return;
    const currentOrigin = window.location.origin;
    const redirectUri = `${currentOrigin}/oidc/callback`;
    const authorizeUrl = new URL(`${apiBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', 'radish-client');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    authorizeUrl.searchParams.set('scope', 'radish-api');
    window.location.href = authorizeUrl.toString();
  };

  return (
    <div style={{ display: 'flex', height: '100%', padding: '8px', boxSizing: 'border-box', gap: '8px' }}>
      {/* 分类列表 */}
      <div style={{ width: '220px', borderRight: '1px solid #333', paddingRight: '8px', overflowY: 'auto' }}>
        <h3 style={{ marginTop: 0 }}>分类</h3>
        {loadingCategories && <p>加载分类中...</p>}
        {!loadingCategories && categories.length === 0 && <p>暂无分类</p>}
        <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
          {categories.map(category => (
            <li key={category.id}>
              <button
                type="button"
                onClick={() => setSelectedCategoryId(category.id)}
                style={{
                  width: '100%',
                  textAlign: 'left',
                  padding: '4px 6px',
                  marginBottom: '2px',
                  backgroundColor: selectedCategoryId === category.id ? '#2d6cdf' : 'transparent',
                  color: selectedCategoryId === category.id ? '#fff' : 'inherit',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              >
                {category.name}
              </button>
            </li>
          ))}
        </ul>
      </div>

      {/* 帖子列表 */}
      <div style={{ flex: 1, borderRight: '1px solid #333', padding: '0 8px', overflowY: 'auto' }}>
        <h3 style={{ marginTop: 0 }}>帖子</h3>
        {loadingPosts && <p>加载帖子中...</p>}
        {!loadingPosts && posts.length === 0 && <p>该分类下暂无帖子</p>}
        <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
          {posts.map(post => (
            <li key={post.id}>
              <button
                type="button"
                onClick={() => void handleSelectPost(post.id)}
                style={{
                  width: '100%',
                  textAlign: 'left',
                  padding: '6px 8px',
                  marginBottom: '4px',
                  backgroundColor: selectedPost?.id === post.id ? '#1f2937' : 'transparent',
                  borderRadius: '4px',
                  border: '1px solid #444',
                  cursor: 'pointer'
                }}
              >
                <div style={{ fontWeight: 600 }}>{post.title}</div>
                {post.authorName && (
                  <div style={{ fontSize: '12px', opacity: 0.8 }}>
                    作者：{post.authorName}
                  </div>
                )}
              </button>
            </li>
          ))}
        </ul>

        {/* 发帖区域 */}
        <div style={{ marginTop: '12px', borderTop: '1px solid #333', paddingTop: '8px' }}>
          <h4>发帖</h4>
          {!loggedIn && (
            <div style={{ marginBottom: '8px', color: '#f97373' }}>
              当前未登录，无法发帖。
              <button type="button" onClick={handleLoginClick} style={{ marginLeft: '8px' }}>
                去登录
              </button>
            </div>
          )}
          {loggedIn && (
            <div style={{ fontSize: '12px', marginBottom: '4px' }}>
              当前用户：{userName || '已登录用户'}
            </div>
          )}
          <input
            type="text"
            placeholder="帖子标题"
            value={newPostTitle}
            onChange={e => setNewPostTitle(e.target.value)}
            style={{ width: '100%', marginBottom: '4px', padding: '4px' }}
            disabled={!loggedIn}
          />
          <textarea
            placeholder="帖子内容（支持 Markdown）"
            value={newPostContent}
            onChange={e => setNewPostContent(e.target.value)}
            rows={4}
            style={{ width: '100%', marginBottom: '4px', padding: '4px', resize: 'vertical' }}
            disabled={!loggedIn}
          />
          <button type="button" onClick={() => void handlePublishPost()} disabled={!loggedIn}>
            发布帖子
          </button>
        </div>
      </div>

      {/* 帖子详情 + 评论 */}
      <div style={{ flex: 1.2, paddingLeft: '8px', overflowY: 'auto' }}>
        <h3 style={{ marginTop: 0 }}>帖子详情</h3>
        {loadingPostDetail && <p>加载帖子详情中...</p>}
        {!loadingPostDetail && !selectedPost && <p>帖子不存在或已被删除</p>}
        {selectedPost && (
          <div>
            <h4>{selectedPost.title}</h4>
            <div style={{ fontSize: '12px', opacity: 0.8, marginBottom: '4px' }}>
              {selectedPost.authorName && <span>作者：{selectedPost.authorName} </span>}
              {selectedPost.createTime && <span>时间：{selectedPost.createTime}</span>}
            </div>
            <div style={{ whiteSpace: 'pre-wrap', marginBottom: '8px' }}>
              {selectedPost.content}
            </div>
            {selectedPost.tagNames && selectedPost.tagNames.length > 0 && (
              <div style={{ fontSize: '12px', marginBottom: '8px' }}>
                标签：{selectedPost.tagNames.join(', ')}
              </div>
            )}
          </div>
        )}

        <h4>评论</h4>
        {loadingComments && <p>加载评论中...</p>}
        {!loadingComments && comments.length === 0 && selectedPost && <p>还没有评论，快来抢沙发吧！</p>}
        <div>
          {comments.map(comment => (
            <CommentNodeView key={comment.id} node={comment} level={0} />
          ))}
        </div>

        {/* 发表评论区域 */}
        <div style={{ marginTop: '12px', borderTop: '1px solid #333', paddingTop: '8px' }}>
          <h5>发表评论</h5>
          {!loggedIn && (
            <div style={{ marginBottom: '8px', color: '#f97373' }}>
              当前未登录，无法发表评论。
              <button type="button" onClick={handleLoginClick} style={{ marginLeft: '8px' }}>
                去登录
              </button>
            </div>
          )}
          <textarea
            placeholder="评论内容"
            value={newCommentContent}
            onChange={e => setNewCommentContent(e.target.value)}
            rows={3}
            style={{ width: '100%', marginBottom: '4px', padding: '4px', resize: 'vertical' }}
            disabled={!loggedIn || !selectedPost}
          />
          <button type="button" onClick={() => void handleCreateComment()} disabled={!loggedIn || !selectedPost}>
            发表评论
          </button>
        </div>

        {error && (
          <p style={{ color: '#f97373', marginTop: '8px' }}>错误：{error}</p>
        )}
      </div>
    </div>
  );
};

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

interface CommentNodeViewProps {
  node: CommentNode;
  level: number;
}

const CommentNodeView = ({ node, level }: CommentNodeViewProps) => {
  return (
    <div style={{ marginLeft: level * 16, marginBottom: '4px', padding: '4px 6px', borderLeft: '1px solid #444' }}>
      <div style={{ fontSize: '12px', opacity: 0.8 }}>
        {node.authorName} {node.createTime && <span> · {node.createTime}</span>}
      </div>
      <div style={{ whiteSpace: 'pre-wrap' }}>{node.content}</div>
      {node.children && node.children.length > 0 && (
        <div style={{ marginTop: '4px' }}>
          {node.children.map(child => (
            <CommentNodeView key={child.id} node={child} level={level + 1} />
          ))}
        </div>
      )}
    </div>
  );
};
