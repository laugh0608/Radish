# 论坛帖子分类与标签（专题）

> 本文档聚焦论坛「分类 + 标签」能力，统一记录当前实现状态、差距和后续规划。
>
> 为避免重复，其他论坛文档只保留摘要并链接到本文。

---

## 1. 文档范围

- **分类（Category）**：用于论坛内容的主维度归档（如技术、公告、反馈）。
- **标签（Tag）**：用于帖子内容的横向主题标记（如 C#、React、性能优化）。
- **不在本文范围**：评论标签、商城标签、SEO 细节实现。

---

## 2. 当前实现状态（代码核对）

## 2.1 后端状态

### 已实现

- **实体与关联已具备**
  - `Category`、`Tag`、`PostTag` 三类模型已存在。
- **分类接口已具备**
  - `GET /api/v1/Category/GetTopCategories`
  - `GET /api/v1/Category/GetChildCategories`
  - `GET /api/v1/Category/GetById/{id}`
  - `POST /api/v1/Category/Create`
- **标签接口已具备**
  - `GET /api/v1/Tag/GetAll`
  - `GET /api/v1/Tag/GetHotTags`
  - `GET /api/v1/Tag/GetById/{id}`
- **发布帖子支持标签写入**
  - `PostController.Publish` 接收 `PublishPostDto.TagNames`
  - `PostService.PublishPostAsync` 会自动：
    - 获取或创建标签
    - 写入 `PostTag` 关联
    - 更新标签帖子计数
- **帖子详情包含标签信息**
  - `PostService.GetPostDetailAsync` 会填充 `PostVo.VoTags`（逗号分隔字符串）

### 尚未打通/不足

- **帖子列表接口尚未支持按标签筛选**
  - `PostController.GetList` 当前仅支持 `categoryId`、`keyword`、`sortBy`。
- **标签数据契约与前端展示字段不完全一致**
  - 后端详情返回 `VoTags`（字符串）。
  - 前端详情组件当前读取 `voTagNames`（数组）。

---

## 2.2 前端状态

### 已实现

- **分类 UI 已完成并已接后端**
  - 分类列表、分类高亮、按分类筛帖已可用。
  - 已调用 `GET /api/v1/Category/GetTopCategories` 与 `GET /api/v1/Post/GetList?categoryId=...`。

### 未实现

- **标签区仍为占位**
  - `ForumApp` 左侧存在 `tagsSection`，当前仅 `TODO`。
- **发帖未提供标签输入**
  - 前端发布逻辑固定传 `tagNames: []`。
- **帖子详情标签展示未打通**
  - 组件读取 `voTagNames`，但当前主数据契约实际是 `voTags`。
- **未接入标签相关 API**
  - 前端 `api/forum.ts` 尚无 `Tag/GetAll` / `Tag/GetHotTags` 调用封装。

---

## 3. 统一结论

- 后端：**分类 + 标签核心能力已具备**（模型、接口、发布写入链路都有）。
- 前端：**分类已完成，标签仍缺核心交互与展示闭环**。

---

## 4. 落地建议（按优先级）

### P0：前端先对齐现有后端契约

- 帖子详情优先兼容 `voTags` 字符串展示（前端按约定适配后端字段）。
- 发帖 UI 增加标签输入，提交时写入 `tagNames`。

### P1：补齐标签浏览能力

- 前端接入标签接口（至少 `GetHotTags`）。
- 左侧标签区支持标签点击筛选（先本地参数流转，再决定路由化）。

### P2：统一路由与搜索策略

- 与前端设计文档保持一致，逐步支持 `/forum/tag/{tag}`。
- 评估在 `Post/GetList` 增加标签筛选参数，避免前端内存过滤。

---

## 5. 相关文档

- [论坛应用功能说明](./forum-features.md)
- [论坛应用评估报告](./forum-assessment.md)
- [论坛重构方案](./forum-refactoring.md)
- [前端设计文档](/frontend/design)

