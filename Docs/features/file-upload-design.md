# 文件上传与附件管理（Radish.Api）

> **最后更新**：2026-03-29

本文档描述当前附件系统的真实实现口径，重点覆盖“附件如何落库、如何在正文中引用、如何在运行时解析 URL，以及更换域名时哪些数据不需要再人工修补”。

---

## 核心口径

- **附件业务真值统一为 `Attachment.Id`**。所有业务对象都应该引用 `attachmentId`，而不是把完整绝对 URL 存入数据库。
- **`Attachment` 实体当前只存储存储层信息**，例如 `StoragePath`、`ThumbnailPath`、`BusinessType`、`BusinessId`、`MimeType`、`FileHash` 等，不再把域名和完整 URL 作为持久化真值。
- **`AttachmentVo.VoUrl` / `VoThumbnailUrl` 是运行时派生字段**。后端通过 `IAttachmentUrlResolver` 把附件 Id 解析成相对资源路径：
  - 原图：`/_assets/attachments/{id}`
  - 缩略图：`/_assets/attachments/{id}/thumbnail`
- **正文中的图片 / 文档引用统一保存为 `attachment://{id}` 协议**。论坛帖子、评论、Wiki 等场景不再把上传后的完整 URL 直接写入 Markdown / 富文本正文。
- **更换域名时不再需要更新附件类数据库字段**。只要公开入口、反向代理和运行时配置切到新域名，媒体资源 URL 会自然跟随切换。

---

## 功能概览

- 普通上传：图片 / 文档上传、查询、下载、删除
- 图片处理：缩略图、多尺寸、文字水印、EXIF 移除（按请求参数控制）
- 文件去重：基于 SHA256 的内容哈希
- 上传限流：并发上传数、每分钟上传次数、每日上传总大小
- 分片上传：会话创建、分片上传、会话查询、合并、取消（支持断点续传）
- 临时访问令牌：为附件生成临时访问链接，支持过期、次数、用户、IP 限制
- 清理任务：Hangfire 定时清理软删除文件、临时文件、回收站、孤立附件、过期分片会话、过期令牌

---

## 认证与权限

- 默认所有管理型接口需要登录（`Authorization: Bearer <access_token>`）。
- `GET /api/v1/Attachment/DownloadByToken` 允许匿名访问，但必须携带有效 `token`。
- `GET /_assets/attachments/{id}` 与 `GET /_assets/attachments/{id}/thumbnail` 会根据附件公开性与当前用户权限决定是否允许访问。
- 临时访问令牌的创建 / 撤销 / 列表当前按“附件上传者”做权限判断（管理员判定仍待补充）。

获取 `access_token`：参考 `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http`。

---

## 接口说明

### 1) 普通上传（Attachment）

- `POST /api/v1/Attachment/UploadImage`
  - `multipart/form-data`
  - 表单字段：
    - `file`：图片文件
    - `businessType`：业务类型（默认 `General`）
    - `generateThumbnail`：是否生成缩略图（默认 `true`）
    - `generateMultipleSizes`：是否生成多尺寸（默认 `false`）
    - `addWatermark`：是否添加水印（默认 `false`）
    - `watermarkText`：水印文本（默认 `Radish`）
    - `removeExif`：是否移除 EXIF（默认 `true`）

- `POST /api/v1/Attachment/UploadDocument`
  - `multipart/form-data`
  - 表单字段：
    - `file`：文档文件
    - `businessType`：业务类型（默认 `Document`）

- 查询与下载
  - `GET /api/v1/Attachment/GetById/{id}`：获取附件元数据（含运行时派生的 `voUrl` / `voThumbnailUrl`）
  - `GET /api/v1/Attachment/GetByBusiness?businessType=...&businessId=...`
  - `GET /api/v1/Attachment/Download/{id}`：以附件原始文件名下载

- 删除与关联
  - `DELETE /api/v1/Attachment/Delete/{id}`
  - `POST /api/v1/Attachment/DeleteBatch`
  - `PUT /api/v1/Attachment/UpdateBusinessAssociation/{id}?businessType=...&businessId=...`

### 2) 公开资源访问口径

- `GET /_assets/attachments/{id}`
  - 返回附件原始文件流
  - 适合作为运行时展示地址
- `GET /_assets/attachments/{id}/thumbnail`
  - 返回缩略图文件流
  - 适合作为列表预览、卡片缩略图和富文本缩略显示地址

> 业务侧统一推荐使用 `/_assets/attachments/*` 作为公开媒体访问口径；`FileStorage:Local:BaseUrl=/uploads` 仅表示底层本地静态文件暴露前缀，不再作为业务真值。

### 3) 上传限流（UploadRateLimit）

- 触发位置：`UploadImage` / `UploadDocument`
- 返回码：触发限流时返回 `429`
- 限流维度：
  - 并发上传数（默认 5）
  - 每分钟上传次数（默认 20）
  - 每日上传总大小（默认 100MB）

### 4) 分片上传（ChunkedUpload）

接口路由基于 `ChunkedUploadController`：`/api/v1/ChunkedUpload/{Action}`。

- `POST /api/v1/ChunkedUpload/CreateSession`
  - `application/json`
  - body：`CreateUploadSessionDto`

- `POST /api/v1/ChunkedUpload/UploadChunk`
  - `multipart/form-data`
  - 表单字段：
    - `sessionId`：会话 ID
    - `chunkIndex`：分片索引（从 0 开始）
    - `chunkData`：分片文件
  - 单分片请求大小限制：10MB（控制器 `RequestSizeLimit`）

- `GET /api/v1/ChunkedUpload/GetSession?sessionId=...`

- `POST /api/v1/ChunkedUpload/MergeChunks`
  - `application/json`
  - body：`MergeChunksDto`

- `POST /api/v1/ChunkedUpload/CancelSession`
  - `application/json`
  - body：JSON 字符串（示例：`"{sessionId}"`）

断点续传方式：通过 `GetSession` 返回的 `uploadedChunkIndexes` 识别已上传分片，只补传缺失分片。

### 5) 临时访问令牌（FileAccessToken）

- `POST /api/v1/Attachment/CreateAccessToken`
  - `application/json`
  - body：`CreateFileAccessTokenDto`
  - 返回：令牌信息与 `accessUrl`

- `GET /api/v1/Attachment/DownloadByToken?token=...`
  - `AllowAnonymous`
  - 验证项：过期 / 撤销 / 次数上限 / 限定用户 / 限定 IP

- `POST /api/v1/Attachment/RevokeAccessToken`
  - `application/json`
  - body：JSON 字符串（示例：`"{token}"`）

- `GET /api/v1/Attachment/GetAttachmentTokens?attachmentId=...`

> `accessUrl` 会基于当前请求的 `scheme + host` 生成，因此在切换域名后，历史分发出去的访问链接如果写死旧域名，应重新生成或让调用方基于新公开入口重建链接；这不影响数据库中的附件真值。

---

## 正文引用协议

当前论坛、评论、Wiki 等正文层已经统一采用 `attachment://{id}` 协议：

```markdown
![示例图片](attachment://123456789#radish:display=thumbnail&scale=60)
[设计文档](attachment://2233445566)
```

- `attachment://{id}` 表示正文只持有附件标识，不持有运行时域名。
- `display=thumbnail` 和 `scale=...` 属于渲染元数据，由前端编辑器写入、渲染器解析。
- 运行时真实资源地址通过 `buildAttachmentAssetUrl()` / `parseAttachmentMarkdownUrl()` 再结合当前 `baseUrl` 解析。

---

## 配置说明（Radish.Api/appsettings.json）

### 1) FileStorage

- `FileStorage:Type`：`Local` / `MinIO` / `OSS`
- `FileStorage:Local:BasePath`：默认 `DataBases/Uploads`
- `FileStorage:Local:BaseUrl`：默认 `/uploads`
- `FileStorage:MaxFileSize`：按业务类型控制单文件大小（当前默认 `Document=30MB`）
- `FileStorage:AllowedExtensions`：按业务类型控制扩展名白名单
- `FileStorage:Deduplication:Enable`：是否启用去重
- `FileStorage:Deduplication:HashAlgorithm`：默认 `SHA256`

补充说明：

- `Local.BasePath` 是物理存储目录。
- `Local.BaseUrl` 是底层本地静态文件暴露前缀，不再是业务对外引用的推荐口径。
- 业务层、前端编辑器、ViewModel 与部署文档统一推荐理解为：**展示用 URL 由 `attachmentId` 在运行时解析**。

### 2) UploadRateLimit

- `UploadRateLimit:Enable`
- `UploadRateLimit:MaxConcurrentUploads`
- `UploadRateLimit:MaxUploadsPerMinute`
- `UploadRateLimit:MaxDailyUploadSize`

### 3) ChunkedUpload

- `ChunkedUpload:Enable`
- `ChunkedUpload:DefaultChunkSize`
- `ChunkedUpload:MinChunkSize`
- `ChunkedUpload:MaxChunkSize`
- `ChunkedUpload:SessionExpirationHours`
- `ChunkedUpload:TempChunkPath`

备注：当前后端实现的默认临时目录为 `DataBases/Temp/Chunks`，会话默认过期为 24 小时。

### 4) Hangfire（清理任务）

- Dashboard：`Hangfire:Dashboard:*`
- 文件清理：`Hangfire:FileCleanup:*`
  - 软删除文件、临时文件、回收站、孤立附件

分片会话与令牌清理任务：在 `Radish.Api/Program.cs` 注册为 RecurringJob（按天执行）。

---

## 数据模型

- `Attachment`：附件实体，存储路径与业务关联信息
- `UploadSession`：分片上传会话
- `FileAccessToken`：临时访问令牌

表创建：本地启动 API 后，SqlSugar 会根据实体自动创建缺失表（以当前项目配置为准）。

---

## 本地测试

- HTTP 测试用例：
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.Upload.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.Manage.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.Guardrail.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.Chunk.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.Token.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.RateLimit.Core.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.RateLimit.Policy.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.RateLimit.Edge.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http`

- 启动服务：
  - `dotnet run --project Radish.Api/Radish.Api.csproj`

---

## 更换域名时的处理边界

### 不需要手工更新的数据

- `Attachment` 表中的附件记录
- 依赖 `AttachmentId` 的贴图、Reaction、聊天图片、商品图标 / 封面、订单商品图标快照
- 正文中的 `attachment://{id}` 引用

### 需要同步调整的配置

- `RADISH_PUBLIC_URL`
- Gateway / Nginx / Traefik / Caddy 的公开域名与证书
- OIDC `Issuer`、客户端回调地址与前端运行时公开入口
- 如有外部系统缓存了旧的 `accessUrl`，需要重新生成或更新分发链接

---

## 未完成事项（TODO）

- 前端分片上传交互完善（暂停 / 恢复、断点续传 UI、失败重试策略可视化）
- 分片上传服务接入配置项并做参数校验（`MinChunkSize` / `MaxChunkSize` / `TempChunkPath` / `SessionExpirationHours`）
- 临时令牌权限：管理员判定、审计与管理界面
- 分片上传与临时令牌的单元 / 集成测试覆盖（服务层 + 控制器）
