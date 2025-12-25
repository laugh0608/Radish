# 文件上传与附件管理（Radish.Api）

> **最后更新**：2025-12-24

本文档是 Radish 当前版本的“使用说明文档”，仅描述已落地的实现、接口、配置与测试方法。

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

- 默认所有接口需要登录（`Authorization: Bearer <access_token>`）。
- `GET /api/v1/Attachment/DownloadByToken` 允许匿名访问，但需要携带有效 `token`。
- 临时访问令牌的创建/撤销/列表：当前实现按“附件上传者”判断（管理员判定待补充）。

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
  - `GET /api/v1/Attachment/GetById/{id}`
  - `GET /api/v1/Attachment/GetByBusiness?businessType=...&businessId=...`
  - `GET /api/v1/Attachment/Download/{id}`

- 删除与关联
  - `DELETE /api/v1/Attachment/Delete/{id}`
  - `POST /api/v1/Attachment/DeleteBatch`
  - `PUT /api/v1/Attachment/UpdateBusinessAssociation/{id}?businessType=...&businessId=...`

### 2) 上传限流（UploadRateLimit）

- 触发位置：`UploadImage` / `UploadDocument`
- 返回码：触发限流时返回 `429`
- 限流维度：
  - 并发上传数（默认 5）
  - 每分钟上传次数（默认 20）
  - 每日上传总大小（默认 100MB）

### 3) 分片上传（ChunkedUpload）

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

### 4) 临时访问令牌（FileAccessToken）

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

---

## 配置说明（Radish.Api/appsettings.json）

### 1) FileStorage

- `FileStorage:Type`：`Local` / `MinIO` / `OSS`
- `FileStorage:Local:BasePath`：默认 `DataBases/Uploads`
- `FileStorage:Local:BaseUrl`：默认 `/uploads`
- `FileStorage:MaxFileSize`：按业务类型控制单文件大小
- `FileStorage:AllowedExtensions`：按业务类型控制扩展名白名单
- `FileStorage:Deduplication:Enable`：是否启用去重
- `FileStorage:Deduplication:HashAlgorithm`：默认 `SHA256`

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

## 数据模型（数据库表）

- `Attachment`：`Radish.Model/Attachment.cs`
- `UploadSession`：`Radish.Model/Models/UploadSession.cs`
- `FileAccessToken`：`Radish.Model/Models/FileAccessToken.cs`

表创建：本地启动 API 后，SqlSugar 会根据实体自动创建缺失表（以当前项目配置为准）。

---

## 本地测试

- HTTP 测试用例：
  - `Radish.Api.Tests/HttpTest/Radish.Api.Attachment.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.RateLimit.http`
  - `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http`

- 启动服务：
  - `dotnet run --project Radish.Api/Radish.Api.csproj`

---

## 未完成事项（TODO）

- 前端分片上传实现（切片、并发、重试、断点续传 UI/交互）
- 分片上传服务接入配置项并做参数校验（`MinChunkSize/MaxChunkSize/TempChunkPath/SessionExpirationHours`）
- 临时令牌权限：管理员判定、审计与管理界面
- 分片上传与临时令牌的单元/集成测试覆盖（服务层 + 控制器）
