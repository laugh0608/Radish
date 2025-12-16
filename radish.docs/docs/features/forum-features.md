# 论坛应用功能说明

> Radish 论坛应用核心功能实现文档
>
> **版本**: v0.1.1
>
> **最后更新**: 2025.12.16

---

## 概述

Radish 论坛应用是一个基于 WebOS 架构的现代化社区讨论平台，提供完整的帖子浏览、发布、评论和互动功能。本文档详细说明了当前已实现的核心功能。

## 架构概览

### 技术栈

**前端**：
- React 19 + TypeScript
- Vite (Rolldown bundler)
- @radish/ui 共享组件库
- CSS Modules (局部样式隔离)

**后端**：
- ASP.NET Core 10
- SqlSugar ORM
- PostgreSQL/SQLite
- JWT 认证

### 目录结构

```
radish.client/src/apps/forum/
├── ForumApp.tsx                 # 主应用入口，状态管理
├── ForumApp.module.css          # 主应用样式
├── components/
│   ├── CategoryList.tsx         # 分类列表组件
│   ├── PostList.tsx             # 帖子列表组件（含分页、排序、搜索）
│   ├── PostDetail.tsx           # 帖子详情组件（含点赞）
│   ├── CommentTree.tsx          # 评论树组件
│   ├── PublishPostForm.tsx      # 发帖表单组件
│   └── CreateCommentForm.tsx    # 评论表单组件
```

---

## 已实现功能

### 1. 组件架构优化

**实现时间**: 2025.12.15

**目标**: 将通用组件从 `radish.client/src/shared` 迁移到 `@radish/ui` 共享组件库，提升代码复用性和可维护性。

**实现内容**:

#### 1.1 MarkdownRenderer（Markdown 渲染组件）

**位置**: `radish.ui/src/components/MarkdownRenderer/`

**功能特性**:
- 基于 `react-markdown` v9 + `remark-gfm` + `rehype-highlight`
- 支持 GitHub Flavored Markdown (GFM)
- 自动语法高亮（代码块）
- 外链自动添加 `target="_blank"` 和 `rel="noopener noreferrer"`
- 表格自动添加滚动容器（横向溢出时可滚动）
- 响应式设计，适配移动端

**使用示例**:
```typescript
import { MarkdownRenderer } from '@radish/ui';

<MarkdownRenderer
  content={post.content}
  className={styles.postBody}
/>
```

**样式定制**:
- 代码块: 深色主题 + 滚动条优化
- 链接: 蓝色高亮 + hover 下划线
- 引用块: 左侧蓝色边框
- 表格: 深色主题 + 边框分隔

#### 1.2 GlassPanel（毛玻璃面板组件）

**位置**: `radish.ui/src/components/GlassPanel/`

**功能特性**:
- 毛玻璃效果（backdrop-filter: blur）
- 可配置透明度和模糊度
- 支持自定义内边距和圆角
- 性能优化（GPU 加速）

**使用示例**:
```typescript
import { GlassPanel } from '@radish/ui';

<GlassPanel opacity={0.8} blur={10}>
  <YourContent />
</GlassPanel>
```

**迁移影响**:
- ✅ 所有导入路径已更新
- ✅ npm workspaces 提供即时 HMR
- ✅ 不需要重启开发服务器

---

### 2. 帖子列表分页

**实现时间**: 2025.12.15

**目标**: 优化大量帖子的加载性能，提供流畅的翻页体验。

**后端实现**:

**API 端点**: `GET /api/v1/Post/GetList`

**请求参数**:
```typescript
{
  categoryId?: number,  // 分类ID（可选）
  pageIndex: number,    // 页码（从1开始）
  pageSize: number,     // 每页数量（默认20，最大100）
  sortBy?: string,      // 排序方式
  keyword?: string      // 搜索关键词
}
```

**响应格式**:
```typescript
{
  page: number,         // 当前页码
  pageSize: number,     // 每页数量
  dataCount: number,    // 总数据量
  pageCount: number,    // 总页数
  data: PostItem[]      // 帖子列表
}
```

**实现细节**:
- 使用 `BaseService.QueryPageAsync()` 实现分页
- 参数验证：pageIndex ≥ 1, pageSize ∈ [1, 100]
- 性能优化：只查询当前页数据，避免全表扫描

**前端实现**:

**状态管理**:
```typescript
const [currentPage, setCurrentPage] = useState(1);
const [pageSize] = useState(20);
const [totalPages, setTotalPages] = useState(0);
```

**智能分页控件**:

**显示逻辑**:
- 总页数 ≤ 5: 显示所有页码
- 当前页 ≤ 3: `1 2 3 4 ... N`
- 当前页 ≥ N-2: `1 ... N-3 N-2 N-1 N`
- 中间位置: `1 ... P-1 P P+1 ... N`

**交互特性**:
- 上一页/下一页按钮
- 快速跳转到首页/尾页
- 当前页高亮显示
- 边界禁用（第一页禁用"上一页"，最后一页禁用"下一页"）
- 键盘导航支持

**样式设计**:
- 响应式按钮（hover 高亮、active 按压）
- 当前页使用主题色（#2d6cdf）
- 禁用状态半透明
- 省略号不可点击

---

### 3. 多维度排序

**实现时间**: 2025.12.15

**目标**: 提供多种帖子排序方式，满足不同用户的浏览需求。

**排序方式**:

#### 3.1 最新 (newest)

**排序规则**:
```sql
ORDER BY IsTop DESC, CreateTime DESC
```

**说明**:
- 置顶帖子优先显示
- 非置顶帖子按发布时间倒序

**适用场景**: 查看最新讨论

#### 3.2 最热 (hottest)

**排序规则**:
```sql
ORDER BY IsTop DESC, (ViewCount + LikeCount*2 + CommentCount*3) DESC
```

**热度算法**:
```typescript
热度 = 浏览数 + 点赞数×2 + 评论数×3
```

**权重设计**:
- 浏览数: 1倍（基础权重）
- 点赞数: 2倍（用户主动认可）
- 评论数: 3倍（深度互动）

**实现方式**:
- 先查询所有帖子
- 在内存中计算热度并排序
- 再进行分页截取

**说明**: 评论互动的价值 > 点赞认可 > 被动浏览

**适用场景**: 发现热门讨论

#### 3.3 精华 (essence)

**排序规则**:
```sql
ORDER BY IsTop DESC, IsEssence DESC, CreateTime DESC
```

**说明**:
- 置顶帖子优先
- 精华帖子优先
- 相同条件下按时间倒序

**适用场景**: 查看高质量内容

**前端实现**:

**排序按钮组**:
```tsx
<div className={styles.sortButtons}>
  <button
    className={`${styles.sortButton} ${sortBy === 'newest' ? styles.sortActive : ''}`}
    onClick={() => onSortChange('newest')}
  >
    最新
  </button>
  <button
    className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.sortActive : ''}`}
    onClick={() => onSortChange('hottest')}
  >
    最热
  </button>
  <button
    className={`${styles.sortButton} ${sortBy === 'essence' ? styles.sortActive : ''}`}
    onClick={() => onSortChange('essence')}
  >
    精华
  </button>
</div>
```

**交互特性**:
- 当前排序方式高亮显示
- 切换排序时自动重置到第一页
- 平滑过渡动画

---

### 4. 全文搜索

**实现时间**: 2025.12.15

**目标**: 允许用户快速找到感兴趣的帖子。

**后端实现**:

**搜索字段**:
- 帖子标题 (`Title`)
- 帖子内容 (`Content`)

**SQL 实现**:
```csharp
if (!string.IsNullOrWhiteSpace(keyword))
{
    baseCondition = p => p.CategoryId == categoryId.Value
        && p.IsPublished
        && !p.IsDeleted
        && (p.Title.Contains(keyword) || p.Content.Contains(keyword));
}
```

**性能考虑**:
- 使用 `LIKE` 查询（`%keyword%`）
- 对于大数据量场景，建议：
  - 添加全文索引（PostgreSQL: `GIN` 索引）
  - 或集成 Elasticsearch 实现高性能搜索

**前端实现**:

**防抖优化**:

**问题**: 用户每输入一个字符就触发一次 API 请求，造成性能浪费和服务器压力。

**解决方案**: 使用 500ms 防抖延迟

**实现代码**:
```typescript
const [localSearch, setLocalSearch] = useState(searchKeyword);

useEffect(() => {
  const timer = setTimeout(() => {
    onSearchChange(localSearch);
  }, 500); // 500ms 延迟

  return () => clearTimeout(timer); // 清理定时器
}, [localSearch, onSearchChange]);
```

**工作原理**:
1. 用户输入触发 `setLocalSearch`（UI 立即更新）
2. 启动 500ms 定时器
3. 如果用户继续输入，清除旧定时器，重新计时
4. 用户停止输入 500ms 后，触发实际搜索

**搜索框设计**:

**功能特性**:
- 实时输入（本地状态）
- 延迟搜索（500ms 防抖）
- 清除按钮（快速清空）
- 占位提示文本

**组件代码**:
```tsx
<div className={styles.searchBox}>
  <input
    type="text"
    placeholder="搜索帖子标题或内容..."
    value={localSearch}
    onChange={(e) => setLocalSearch(e.target.value)}
    className={styles.searchInput}
  />
  {localSearch && (
    <button
      type="button"
      onClick={() => setLocalSearch('')}
      className={styles.clearButton}
      title="清除搜索"
    >
      ×
    </button>
  )}
</div>
```

**样式特性**:
- 深色主题输入框
- focus 状态高亮（蓝色边框）
- 清除按钮圆形设计
- 响应式宽度

**交互逻辑**:
- 搜索时自动重置到第一页
- 清空搜索时恢复原列表
- 支持键盘操作（Enter 立即搜索、Escape 清空）

---

### 5. 点赞功能

**实现时间**: 2025.12.15

**目标**: 允许用户对喜欢的帖子进行点赞，提升社区互动性。

**后端实现**:

**API 端点**: `POST /api/v1/Post/Like`

**请求参数**:
```typescript
{
  postId: number,   // 帖子ID
  isLike: boolean   // true=点赞, false=取消点赞
}
```

**实现逻辑**:
```csharp
[HttpPost]
public async Task<MessageModel> Like(long postId, bool isLike = true)
{
    await _postService.UpdateLikeCountAsync(postId, isLike ? 1 : -1);
    return new MessageModel
    {
        IsSuccess = true,
        StatusCode = (int)HttpStatusCodeEnum.Success,
        MessageInfo = isLike ? "点赞成功" : "取消点赞成功"
    };
}
```

**数据库操作**:
- 增量更新：`LikeCount = LikeCount + delta`
- 原子操作：使用 `UPDATE ... SET LikeCount = LikeCount + 1`
- 并发安全：数据库层面处理

**前端实现**:

#### 5.1 状态持久化

**问题**: 刷新页面后丢失用户的点赞记录。

**解决方案**: 使用 localStorage 持久化

**实现代码**:
```typescript
const [likedPosts, setLikedPosts] = useState<Set<number>>(() => {
  try {
    const stored = localStorage.getItem('forum_liked_posts');
    return stored ? new Set(JSON.parse(stored)) : new Set();
  } catch {
    return new Set();
  }
});

// 更新时同步到 localStorage
const newLikedPosts = new Set(likedPosts);
if (isLike) {
  newLikedPosts.add(postId);
} else {
  newLikedPosts.delete(postId);
}
setLikedPosts(newLikedPosts);
localStorage.setItem('forum_liked_posts', JSON.stringify([...newLikedPosts]));
```

**注意事项**:
- localStorage 仅存储在客户端
- 切换浏览器或设备后不同步
- 后续可升级为服务端存储（用户点赞记录表）

#### 5.2 乐观更新 (Optimistic Update)

**问题**: 等待 API 响应后才更新 UI，体验不流畅。

**解决方案**: 先更新 UI，后调用 API，失败时回滚

**工作流程**:
```
用户点击 → 立即更新UI → 调用API
                ↓           ↓
            显示新状态   成功:刷新
                           ↓
                        失败:回滚
```

**实现代码**:
```typescript
async function handleLikePost(postId: number) {
  const isCurrentlyLiked = likedPosts.has(postId);
  const isLike = !isCurrentlyLiked;

  try {
    // 1. 乐观更新：立即更新UI
    const newLikedPosts = new Set(likedPosts);
    if (isLike) {
      newLikedPosts.add(postId);
    } else {
      newLikedPosts.delete(postId);
    }
    setLikedPosts(newLikedPosts);
    localStorage.setItem('forum_liked_posts', JSON.stringify([...newLikedPosts]));

    // 2. 更新当前帖子的点赞数
    if (selectedPost && selectedPost.id === postId) {
      setSelectedPost({
        ...selectedPost,
        likeCount: (selectedPost.likeCount || 0) + (isLike ? 1 : -1)
      });
    }

    // 3. 调用API
    await likePost(postId, isLike, t);

    // 4. 刷新帖子详情（确保同步）
    await loadPostDetail(postId);
  } catch (err) {
    // 5. 失败时回滚
    setLikedPosts(new Set(likedPosts));
    const message = err instanceof Error ? err.message : String(err);
    setError(message);
  }
}
```

**优势**:
- ✅ 即时响应，无等待感
- ✅ 网络延迟不影响体验
- ✅ 失败时自动回滚，用户感知明确

#### 5.3 点赞按钮 UI

**组件结构**:
```tsx
<div className={styles.actions}>
  <button
    type="button"
    onClick={() => onLike?.(post.id)}
    className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
    disabled={!isAuthenticated}
    title={!isAuthenticated ? '请先登录' : isLiked ? '取消点赞' : '点赞'}
  >
    <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
    <span className={styles.likeCount}>{post.likeCount || 0}</span>
  </button>
  <span className={styles.commentCount}>
    💬 {post.commentCount || 0} 条评论
  </span>
</div>
```

**视觉状态**:

| 状态 | emoji | 样式 | 说明 |
|------|-------|------|------|
| 未点赞 | 🤍 | 灰色边框 | 默认状态 |
| 已点赞 | ❤️ | 红色边框+背景 | 高亮状态 |
| 禁用 | 🤍 | 半透明 | 未登录时 |

**交互动画**:

**Hover 效果**:
- 背景变浅
- 边框变亮
- 轻微上移（translateY(-2px)）
- 图标放大（scale(1.2)）

**点击动画** (已点赞时):
```css
@keyframes heartbeat {
  0% { transform: scale(1); }
  50% { transform: scale(1.3); }
  100% { transform: scale(1); }
}
```

**CSS 实现**:
```css
.likeButton {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background-color: transparent;
  border: 1px solid #444;
  border-radius: 6px;
  color: #fff;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.likeButton:hover:not(:disabled) {
  background-color: #1f2937;
  border-color: #555;
  transform: translateY(-2px);
}

.likeButton.liked {
  background-color: rgba(239, 68, 68, 0.1);
  border-color: #ef4444;
  color: #ef4444;
}

.likeButton:hover:not(:disabled) .likeIcon {
  transform: scale(1.2);
}

.likeButton.liked .likeIcon {
  animation: heartbeat 0.3s ease-in-out;
}
```

**权限控制**:
- 未登录: 按钮禁用，hover 显示"请先登录"
- 已登录: 按钮可用，hover 显示"点赞"或"取消点赞"

**数据显示**:
- 点赞数量：实时更新
- 评论数量：只读显示

---

### 6. 内容管理

**实现时间**: 2025.12.16

**目标**: 允许用户编辑和删除自己发布的内容，提供草稿自动保存功能，提升内容管理体验。

**后端实现**:

#### 6.1 帖子编辑

**API 端点**: `PUT /api/v1/Post/Update`

**请求参数**:
```typescript
{
  postId: number,      // 帖子ID
  title: string,       // 帖子标题
  content: string,     // 帖子内容
  categoryId?: number  // 分类ID（可选，不传则保持原分类）
}
```

**权限验证**:
- 查询帖子是否存在且未删除
- 验证 `post.AuthorId == currentUserId`
- 不是作者返回 403 Forbidden

**实现逻辑**:
```csharp
[HttpPut]
public async Task<MessageModel> Update([FromBody] UpdatePostRequest request)
{
    // 查询帖子
    var post = await _postService.QueryFirstAsync(p => p.Id == request.PostId && !p.IsDeleted);
    if (post == null) return NotFound();

    // 权限验证：只有作者本人可以编辑
    if (post.AuthorId != _httpContextUser.UserId)
        return new MessageModel { StatusCode = 403, MessageInfo = "无权编辑此帖子" };

    // 更新帖子
    await _postService.UpdateColumnsAsync(
        p => new Post {
            Title = request.Title,
            Content = request.Content,
            ModifyTime = DateTime.Now,
            ModifyBy = _httpContextUser.UserName
        },
        p => p.Id == request.PostId
    );

    return new MessageModel { IsSuccess = true, MessageInfo = "编辑成功" };
}
```

**审计字段**:
- `ModifyTime`: 修改时间
- `ModifyBy`: 修改者用户名
- `ModifyId`: 修改者用户ID

#### 6.2 帖子删除

**API 端点**: `DELETE /api/v1/Post/Delete?postId={id}`

**权限验证**:
- 作者本人或管理员（Admin/System 角色）可以删除
- 使用软删除（`IsDeleted = true`）

**实现逻辑**:
```csharp
[HttpDelete]
public async Task<MessageModel> Delete(long postId)
{
    var post = await _postService.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted);
    if (post == null) return NotFound();

    // 权限验证
    var roles = _httpContextUser.GetClaimValueByType("role");
    var isAdmin = roles.Contains("Admin") || roles.Contains("System");
    if (post.AuthorId != _httpContextUser.UserId && !isAdmin)
        return Forbidden();

    // 软删除
    await _postService.UpdateColumnsAsync(
        p => new Post {
            IsDeleted = true,
            ModifyTime = DateTime.Now,
            ModifyBy = _httpContextUser.UserName
        },
        p => p.Id == postId
    );

    return Success();
}
```

**软删除优势**:
- 数据可恢复（管理后台可查看已删除内容）
- 保留审计记录
- 避免外键约束问题

#### 6.3 评论删除

**API 端点**: `DELETE /api/v1/Comment/Delete?commentId={id}`

**权限验证**: 与帖子删除相同（作者或管理员）

**实现逻辑**: 软删除 + 审计字段更新

**前端实现**:

#### 6.4 EditPostModal（编辑帖子对话框）

**新增组件**: `radish.client/src/apps/forum/components/EditPostModal.tsx`

**功能特性**:
- 使用 `Modal` 组件实现对话框（@radish/ui）
- 表单字段：标题输入框、内容文本域
- 自动填充当前帖子内容
- 实时表单验证（标题和内容不能为空）
- 保存按钮 loading 状态
- 完整的错误处理和提示

**组件代码**:
```tsx
<Modal
  isOpen={isOpen}
  onClose={onClose}
  title="编辑帖子"
  size="large"
  footer={
    <div className={styles.footer}>
      <Button variant="secondary" onClick={onClose} disabled={saving}>
        取消
      </Button>
      <Button variant="primary" onClick={handleSave} disabled={saving}>
        {saving ? '保存中...' : '保存'}
      </Button>
    </div>
  }
>
  <div className={styles.container}>
    {error && <div className={styles.error}>{error}</div>}
    <div className={styles.formGroup}>
      <label>标题</label>
      <input
        type="text"
        value={title}
        onChange={e => setTitle(e.target.value)}
        disabled={saving}
      />
    </div>
    <div className={styles.formGroup}>
      <label>内容（支持 Markdown）</label>
      <textarea
        value={content}
        onChange={e => setContent(e.target.value)}
        rows={15}
        disabled={saving}
      />
    </div>
  </div>
</Modal>
```

**交互流程**:
1. 用户点击"编辑"按钮 → 打开对话框
2. 自动填充当前帖子内容
3. 用户修改标题或内容
4. 点击"保存" → 调用 API
5. 成功后关闭对话框 + 刷新帖子详情和列表
6. 失败时显示错误提示（不关闭对话框）

#### 6.5 权限控制按钮

**PostDetail 组件更新**:

**编辑和删除按钮**:
```tsx
{isAuthor && (
  <div className={styles.authorActions}>
    <button onClick={() => onEdit?.(post.id)} className={styles.editButton}>
      <Icon icon="mdi:pencil" size={18} />
      编辑
    </button>
    <button onClick={() => onDelete?.(post.id)} className={styles.deleteButton}>
      <Icon icon="mdi:delete" size={18} />
      删除
    </button>
  </div>
)}
```

**权限判断**:
```typescript
const isAuthor = post && currentUserId > 0 && post.authorId === currentUserId;
```

**样式设计**:
- 编辑按钮：蓝色高亮（hover 时边框和文字变蓝）
- 删除按钮：红色高亮（hover 时边框和文字变红）
- 使用 Icon 组件显示图标（mdi:pencil, mdi:delete）
- flex 布局，自动右对齐

**CommentNode 组件更新**:

**删除按钮**:
```tsx
{isAuthor && onDelete && (
  <button onClick={() => onDelete(node.id)} className={styles.deleteButton}>
    <Icon icon="mdi:delete" size={14} />
  </button>
)}
```

**特性**:
- 仅在评论右侧显示（使用 flex + margin-left: auto）
- 小尺寸图标（14px）
- hover 时背景变红
- 递归传递给所有子评论节点

#### 6.6 删除确认对话框

**使用 ConfirmDialog 组件**:
```tsx
// 删除帖子确认
<ConfirmDialog
  isOpen={isDeleteDialogOpen}
  title="确认删除"
  message="确定要删除这篇帖子吗？删除后无法恢复。"
  confirmText="删除"
  cancelText="取消"
  danger={true}
  onConfirm={confirmDeletePost}
  onCancel={cancelDeletePost}
/>

// 删除评论确认
<ConfirmDialog
  isOpen={isDeleteCommentDialogOpen}
  title="确认删除"
  message="确定要删除这条评论吗？删除后无法恢复。"
  confirmText="删除"
  cancelText="取消"
  danger={true}
  onConfirm={confirmDeleteComment}
  onCancel={cancelDeleteComment}
/>
```

**特性**:
- 危险操作样式（红色按钮）
- 清晰的警告文案
- 二次确认机制（防止误操作）
- ESC 键快速取消
- 点击遮罩层关闭

#### 6.7 草稿自动保存

**功能目标**: 防止用户意外丢失编辑中的内容。

**实现位置**: `PublishPostForm` 组件

**localStorage 存储**:
```typescript
const DRAFT_STORAGE_KEY = 'forum_post_draft';

// 存储结构
{
  title: string,
  content: string,
  savedAt: number  // 时间戳（用于后续扩展：过期清理）
}
```

**自动保存逻辑**:
```typescript
// 监听标题和内容变化
useEffect(() => {
  if (title || content) {
    try {
      localStorage.setItem(
        DRAFT_STORAGE_KEY,
        JSON.stringify({ title, content, savedAt: Date.now() })
      );
    } catch (err) {
      console.error('Failed to save draft:', err);
    }
  }
}, [title, content]);
```

**特性**:
- 任一字段变化时自动保存
- 使用 try-catch 处理存储异常（如 localStorage 已满）
- 仅在有内容时才保存（避免空白覆盖）

**草稿恢复逻辑**:
```typescript
// 组件加载时恢复草稿
useEffect(() => {
  try {
    const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
    if (savedDraft) {
      const draft = JSON.parse(savedDraft);
      if (draft.title || draft.content) {
        setTitle(draft.title || '');
        setContent(draft.content || '');
      }
    }
  } catch (err) {
    console.error('Failed to load draft:', err);
  }
}, []);
```

**草稿清理**:
```typescript
// 发布成功后清空草稿
const handleSubmit = () => {
  if (!title.trim() || !content.trim()) return;

  onPublish(title, content);

  // 清空表单和草稿
  setTitle('');
  setContent('');
  try {
    localStorage.removeItem(DRAFT_STORAGE_KEY);
  } catch (err) {
    console.error('Failed to clear draft:', err);
  }
};
```

**用户体验**:
- ✅ 无需手动保存，自动持久化
- ✅ 页面刷新后内容不丢失
- ✅ 关闭浏览器后下次打开仍然保留
- ✅ 发布成功后自动清空（不会保留已发布的内容）

**后续优化方向**:
- 多个草稿支持（使用唯一 key）
- 过期草稿自动清理（基于 savedAt 时间戳）
- 草稿列表管理界面
- 云端同步（需要后端支持）

#### 6.8 数据同步

**编辑成功后**:
- 同时刷新帖子详情和帖子列表
- 确保所有展示位置的数据一致性
- 使用 `Promise.all` 并行刷新

**删除成功后**:
- 帖子删除：清空选中状态 + 刷新列表 + 清空评论树
- 评论删除：重新加载评论树（保持当前帖子状态）

**类型定义更新**:
- `PostDetail` 接口新增 `authorId: number` 字段
- 新增 `UpdatePostRequest` 接口

**Bug 修复**:
- 修复 `ConfirmDialog` 组件导入路径问题：
  - 从相对路径 `'../Modal'` 改为完整路径 `'../Modal/Modal'`
  - 解决 Vite 导入分析错误

---

## 功能协同

以上功能并非独立工作，而是协同配合：

### 场景 1: 用户浏览帖子

```
1. 选择分类 → 加载该分类帖子列表
2. 选择"最热"排序 → 按热度重新排序
3. 输入搜索关键词 → 在热门帖子中搜索
4. 翻页查看更多 → 保持排序和搜索条件
5. 点击帖子 → 查看详情，Markdown 渲染内容
6. 点赞帖子 → 乐观更新，实时反馈
```

### 场景 2: 状态一致性

**切换分类时**:
- ✅ 重置页码到第 1 页
- ✅ 保持当前排序方式
- ✅ 保持当前搜索关键词

**切换排序时**:
- ✅ 重置页码到第 1 页
- ✅ 保持当前分类
- ✅ 保持当前搜索关键词

**搜索时**:
- ✅ 重置页码到第 1 页
- ✅ 保持当前分类
- ✅ 保持当前排序方式

**翻页时**:
- ✅ 保持当前分类
- ✅ 保持当前排序方式
- ✅ 保持当前搜索关键词

---

## 技术亮点

### 1. 代码架构

**分层清晰**:
- API 层: `radish.client/src/api/forum.ts`
- 类型定义: `radish.client/src/types/forum.ts`
- 状态管理: `ForumApp.tsx` (主应用)
- UI 组件: `components/` (子组件)

**职责分离**:
- ForumApp: 状态管理、业务逻辑
- 子组件: UI 渲染、用户交互
- API 层: 网络请求、错误处理

### 2. 性能优化

**防抖**:
- 搜索输入 500ms 防抖
- 减少不必要的 API 请求

**乐观更新**:
- 点赞立即响应
- 避免等待网络延迟

**分页**:
- 只加载当前页数据
- 避免全表扫描

**局部样式**:
- CSS Modules 避免样式冲突
- 减少全局样式污染

### 3. 用户体验

**即时反馈**:
- 按钮 hover/active 状态
- 加载状态提示
- 错误提示

**平滑动画**:
- 点赞心跳动画
- 按钮过渡效果
- 页面切换平滑

**无障碍**:
- 语义化 HTML
- 键盘导航支持
- title 属性提示

### 4. 可维护性

**TypeScript**:
- 完整的类型定义
- 编译时错误检查

**代码复用**:
- 共享组件库 @radish/ui
- 通用工具函数

**文档完善**:
- 代码注释
- API 文档
- 功能文档

---

## 已知限制与后续计划

### 当前限制

1. **搜索性能**:
   - 使用 `LIKE` 查询，大数据量时性能有限
   - 不支持高级搜索（标签、作者、日期范围等）

2. **点赞持久化**:
   - 仅存储在 localStorage
   - 不支持跨设备同步
   - 后续需要服务端存储

3. **热度算法**:
   - 静态权重，未考虑时间衰减
   - 未考虑用户质量（如：版主点赞权重更高）

### 后续规划

1. **富文本编辑器**:
   - Markdown 编辑器
   - 实时预览
   - 图片上传

2. **评论互动**:
   - 评论点赞
   - 评论回复
   - @提及用户

3. **个人中心**:
   - 我的帖子
   - 我的点赞
   - 我的收藏

4. **高级搜索**:
   - 标签筛选
   - 作者筛选
   - 日期范围
   - 全文索引（Elasticsearch）

5. **社交功能**:
   - 关注作者
   - 私信功能
   - 用户主页

---

## 相关文档

- [论坛架构评估](./forum-assessment.md)
- [论坛重构方案](./forum-refactoring.md)
- [开放平台规划](./open-platform.md)
- [前端设计文档](../frontend/FrontendDesign.md)
- [12月开发日志](../changelog/2025-12.md)

---

**文档维护**: 当添加新功能或修改现有功能时，请及时更新本文档。
