import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import {
  getTopCategories,
  getPostList,
  getPostById,
  getCommentTree,
  publishPost,
  createComment
} from '@/api/forum';
import type {
  Category,
  PostItem,
  PostDetail,
  CommentNode
} from '@/types/forum';
import { CategoryList } from './components/CategoryList';
import { PostList } from './components/PostList';
import { PublishPostForm } from './components/PublishPostForm';
import { PostDetail as PostDetailView } from './components/PostDetail';
import { CommentTree } from './components/CommentTree';
import { CreateCommentForm } from './components/CreateCommentForm';
import styles from './ForumApp.module.css';

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userName } = useUserStore();

  // 数据状态
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [selectedPost, setSelectedPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);

  // 加载状态
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);

  // 错误状态
  const [error, setError] = useState<string | null>(null);

  const loggedIn = isAuthenticated();

  // 加载分类列表
  useEffect(() => {
    void loadCategories();
  }, []);

  // 当选择分类时加载帖子列表
  useEffect(() => {
    void loadPosts();
  }, [selectedCategoryId]);

  async function loadCategories() {
    setLoadingCategories(true);
    setError(null);
    try {
      const data = await getTopCategories(t);
      setCategories(data);
      if (data.length > 0 && selectedCategoryId == null) {
        setSelectedCategoryId(data[0].id);
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
      const data = await getPostList(selectedCategoryId, t);
      setPosts(data);
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
      const data = await getPostById(postId, t);
      setSelectedPost(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setSelectedPost(null);
      setComments([]);
    } finally {
      setLoadingPostDetail(false);
    }
  }

  async function loadComments(postId: number) {
    setLoadingComments(true);
    setError(null);
    try {
      const data = await getCommentTree(postId, t);
      setComments(data);
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

  async function handlePublishPost(title: string, content: string) {
    if (!selectedCategoryId) {
      setError('请先选择分类');
      return;
    }

    setError(null);
    try {
      const postId = await publishPost(
        {
          title,
          content,
          categoryId: selectedCategoryId,
          tagNames: []
        },
        t
      );
      // 发布成功后重新加载帖子列表
      await loadPosts();
      // 自动选择新发布的帖子
      await handleSelectPost(postId);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  async function handleCreateComment(content: string) {
    if (!selectedPost) {
      setError('请先选择要评论的帖子');
      return;
    }

    setError(null);
    try {
      await createComment(
        {
          postId: selectedPost.id,
          content,
          parentId: null,
          replyToUserId: null,
          replyToUserName: null
        },
        t
      );
      // 发表成功后重新加载评论列表
      await loadComments(selectedPost.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  return (
    <div className={styles.container}>
      {/* 分类列表 */}
      <CategoryList
        categories={categories}
        selectedCategoryId={selectedCategoryId}
        onSelectCategory={setSelectedCategoryId}
        loading={loadingCategories}
      />

      {/* 帖子列表 + 发帖表单 */}
      <div className={styles.middleColumn}>
        <PostList
          posts={posts}
          selectedPostId={selectedPost?.id || null}
          onSelectPost={handleSelectPost}
          loading={loadingPosts}
        />
        <PublishPostForm
          isAuthenticated={loggedIn}
          userName={userName}
          onPublish={handlePublishPost}
        />
      </div>

      {/* 帖子详情 + 评论区 */}
      <div className={styles.rightColumn}>
        <PostDetailView post={selectedPost} loading={loadingPostDetail} />
        <CommentTree
          comments={comments}
          loading={loadingComments}
          hasPost={selectedPost !== null}
        />
        <CreateCommentForm
          isAuthenticated={loggedIn}
          hasPost={selectedPost !== null}
          onSubmit={handleCreateComment}
        />

        {/* 错误提示 */}
        {error && <p className={styles.errorText}>错误：{error}</p>}
      </div>
    </div>
  );
};
