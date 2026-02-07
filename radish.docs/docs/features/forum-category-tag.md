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
  - `GET /api/v1/Tag/GetFixedTags`
  - `GET /api/v1/Tag/GetHotTags`
  - `GET /api/v1/Tag/GetById/{id}`
  - `GET /api/v1/Tag/GetPage`（管理端分页）
  - `POST /api/v1/Tag/Create`（管理端）
  - `PUT /api/v1/Tag/Update/{id}`（管理端）
  - `PUT /api/v1/Tag/ToggleStatus/{id}/status`（管理端）
  - `PUT /api/v1/Tag/UpdateSort/{id}/sort`（管理端）
  - `DELETE /api/v1/Tag/Delete/{id}`（管理端软删除）
  - `PUT /api/v1/Tag/Restore/{id}/restore`（管理端恢复）
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
- **发帖标签规则已收敛，但筛选仍建议后端化**
  - 发布帖子已要求标签数量 `1~5`。
  - 非管理员发布时不允许自动创建新标签。
  - 标签筛选当前以前端兼容实现为主，后续建议后端支持查询参数。

### 新增能力（固定标签后端驱动）

- 固定标签来源已切换为后端数据库，不再由前端 `env` 注入。
- `radish.client` 通过 `GET /api/v1/Tag/GetFixedTags` 渲染固定标签。
- 固定标签支持管理端增删改查、启停与排序调整。
- 删除采用软删除，支持恢复接口。
- 默认固定标签种子：`社区新闻`、`社区活动`、`精华帖`、`碎碎念`、`公告`。

---

## 2.2 前端状态

### 已实现

- **分类 UI 已完成并已接后端**
  - 分类列表、分类高亮、按分类筛帖已可用。
  - 已调用 `GET /api/v1/Category/GetTopCategories` 与 `GET /api/v1/Post/GetList?categoryId=...`。

### 未实现

- **标签筛选缺少后端原生支持**
  - 当前 `Post/GetList` 仍未支持 `tagName/tagId` 参数。
  - 大数据量下不宜长期依赖前端侧筛选。

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
