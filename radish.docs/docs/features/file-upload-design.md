# 文件上传功能文档

> **状态**：✅ Phase 1 MVP 已完成 | ✅ Phase 2 基本完成（限流已实现）
> **最后更新**：2025-12-24
> **维护者**：Radish Team

## 📋 概述

Radish 项目的文件上传功能提供了完整的文件管理解决方案，支持图片、文档等多种文件类型的上传、存储、查询和删除。

**核心特性**：
- 🎯 支持图片（JPG/PNG/GIF/WebP）和文档（PDF/DOC/DOCX/XLSX）上传
- 🔒 多层安全防护（文件类型校验、**Magic Number 检查**、大小限制）
- ⚡ 自动图片处理（缩略图生成、EXIF 移除、压缩）
- 🏗️ 可扩展架构（本地存储 / MinIO / OSS）
- 🔄 文件去重（基于 SHA256 哈希）
- 🌐 **前端自动重试机制**（指数退避：1s, 2s, 4s）

**适用场景**：
- 论坛帖子配图
- 用户头像上传
- 评论附件
- 文档分享
- 富文本编辑器图片插入

---

## 🚀 快速开始

### 前端使用示例

#### 在 MarkdownEditor 中上传图片

```typescript
import { MarkdownEditor } from '@radish/ui';
import { uploadImage } from '@/api/attachment';
import { useTranslation } from 'react-i18next';

function MyComponent() {
  const { t } = useTranslation();
  const [content, setContent] = useState('');

  const handleImageUpload = async (file: File) => {
    const result = await uploadImage({
      file,
      businessType: 'Post',
      generateThumbnail: true,
      removeExif: true
    }, t);

    return {
      url: result.fileUrl,
      thumbnailUrl: result.thumbnailUrl
    };
  };

  return (
    <MarkdownEditor
      value={content}
      onChange={setContent}
      onImageUpload={handleImageUpload}
      placeholder="输入内容，支持 Markdown..."
    />
  );
}
```

**支持的上传方式**：
1. 点击工具栏图片按钮
2. 拖拽图片到编辑器
3. 粘贴图片（Ctrl+V）

#### 使用 FileUpload 组件

```typescript
import { FileUpload } from '@radish/ui';
import { uploadImage } from '@/api/attachment';

function MyUploadForm() {
  const { t } = useTranslation();

  const handleUpload = async (file: File) => {
    const result = await uploadImage({
      file,
      businessType: 'Avatar',
      generateThumbnail: true,
      onProgress: (progress) => {
        console.log(`上传进度：${progress}%`);
      }
    }, t);

    return {
      id: result.id,
      fileName: result.fileName,
      fileUrl: result.fileUrl,
      thumbnailUrl: result.thumbnailUrl
    };
  };

  return (
    <FileUpload
      accept="image/*"
      maxSize={2 * 1024 * 1024} // 2MB
      onUpload={handleUpload}
      onSuccess={(result) => console.log('上传成功', result)}
      onError={(error) => console.error('上传失败', error)}
      showPreview={true}
    />
  );
}
```

### 后端 API 调用

#### 上传图片

```http
POST /api/v1/Attachment/UploadImage
Authorization: Bearer {access_token}
Content-Type: multipart/form-data

file: (binary)
businessType: Post
generateThumbnail: true
removeExif: true
```

**成功响应**：
```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "上传成功",
  "responseData": {
    "id": 2002696346624065536,
    "fileName": "2002696344824709120.jpg",
    "originalFileName": "my-image.jpg",
    "fileSize": 47295,
    "fileSizeFormatted": "46.19 KB",
    "mimeType": "image/jpeg",
    "storageType": "Local",
    "url": "/uploads/Post/2025/12/2002696344824709120.jpg",
    "thumbnailUrl": "/uploads/Post/2025/12/2002696344824709120_thumb.jpg",
    "uploaderId": 20000,
    "uploaderName": "system",
    "businessType": "Post",
    "isPublic": true,
    "downloadCount": 0,
    "createTime": "2025-12-21T18:52:09"
  }
}
```

---

## ✅ 已实现功能（Phase 1 MVP）

### 后端功能

- ✅ **数据模型和存储接口**
  - Attachment 实体和数据库表
  - IFileStorage 接口
  - LocalFileStorage 实现（本地文件系统）
  - IImageProcessor 接口
  - CSharpImageProcessor 实现（ImageSharp）

- ✅ **安全机制**
  - 文件类型白名单校验
  - **Magic Number 检查**（文件头校验，防止扩展名伪装）
  - 文件大小限制（Avatar: 2MB, Image: 5MB, Document: 10MB）
  - 文件名随机化（雪花ID）
  - EXIF 信息移除

- ✅ **图片处理**
  - 缩略图生成（150x150）
  - 图片压缩（JPEG 85%）
  - EXIF 移除

- ✅ **文件去重**
  - 基于 SHA256 哈希
  - 相同文件秒传

- ✅ **业务逻辑**
  - AttachmentService（CRUD + 上传逻辑）
  - 文件校验
  - 文件去重
  - 图片处理
  - 文件名生成

- ✅ **API 端点**
  - POST /api/v1/Attachment/UploadImage
  - POST /api/v1/Attachment/UploadDocument
  - GET /api/v1/Attachment/GetById/{id}
  - GET /api/v1/Attachment/GetByBusiness
  - GET /api/v1/Attachment/GetUploadStatistics（上传统计）
  - GET /api/v1/Attachment/Download/{id}
  - DELETE /api/v1/Attachment/Delete/{id}
  - POST /api/v1/Attachment/DeleteBatch
  - PUT /api/v1/Attachment/UpdateBusinessAssociation/{id}

### 前端功能

- ✅ **上传组件**
  - FileUpload 组件（拖拽 + 点击上传）
  - 上传进度显示
  - 图片预览
  - 错误提示
  - **自动重试机制**（指数退避：1s, 2s, 4s，最多 3 次）

- ✅ **MarkdownEditor 集成**
  - 图片按钮点击触发上传
  - 上传成功后插入 Markdown 图片语法
  - 支持粘贴图片上传（Ctrl+V）
  - 支持拖拽图片上传
  - 上传状态显示（loading、error）

- ✅ **API 服务层**
  - uploadImage() 函数
  - uploadDocument() 函数
  - 完整的 TypeScript 类型定义
  - 自动重试逻辑

### 配置和测试

- ✅ FileStorage 配置（appsettings.json）
- ✅ Gateway YARP 路由配置（/uploads）
- ✅ HTTP 测试文件（Radish.Api.Attachment.http）
- ✅ 自动化测试脚本（test-attachment-upload.sh/ps1）
- ✅ 测试指南（AttachmentApiTestGuide.md）
- ✅ 测试报告（AttachmentApiTestReport.md）

---

## ✅ 已确认的技术方案（2025-12-20）

### 1. 存储架构
- **架构模式**：统一接口 + 多实现（`IFileStorage`）
- **开发环境**：本地文件系统（`DataBases/Uploads/`）
- **生产环境**：MinIO（可选 docker-compose 部署或远程 OSS）
- **配置切换**：通过 `appsettings.json` 切换存储实现

### 2. 上传方式
- **实现方式**：直接上传到 API 服务器，服务器根据配置路由到存储
- **当前支持**：同步上传（适合 < 10MB）
- **未来扩展**：预留分片上传 API（`UploadChunk` / `MergeChunks`）

### 3. 文件类型支持
- **图片**：`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`
- **文档**：`.pdf`, `.doc`, `.docx`, `.txt`
- **扩展方式**：配置文件白名单

### 4. 文件大小限制（可配置）
- 头像：2MB
- 图片：5MB
- 文档：10MB

### 5. 图片处理
- **处理时机**：上传时同步处理
- **基础功能**：缩略图生成、图片压缩（JPEG 85%）
- **增强功能**：多尺寸、水印、内容审核（Phase 2）
- **性能方案**：优先使用 C# (ImageSharp)，性能不足时切换 Rust

### 6. 删除策略
- **软删除**：标记 `IsDeleted = true`，文件保留
- **自动清理**：定时任务清理过期文件（默认 30 天）
- **定时任务实现**：已集成 Hangfire，支持 4 种清理任务
  - 软删除文件清理（每天凌晨 3 点）
  - 临时文件清理（每小时）
  - 回收站清理（保留 90 天）
  - 孤立附件清理（保留 24 小时）

---

## 📦 存储方案技术对比

### 本地文件系统存储

**目录结构**：
```
DataBases/Uploads/
├── Images/
│   └── 2025/12/
│       ├── original/    # 原图
│       ├── thumb/       # 缩略图 150x150
│       ├── small/       # 小图 400x300
│       └── medium/      # 中图 800x600
├── Documents/
│   └── 2025/12/
└── Temp/                # 临时文件
```

**优点**：实现简单、零成本、调试方便
**缺点**：扩展性差、不支持分布式、无 CDN
**适用**：开发测试环境

### MinIO（S3 兼容存储）

**优点**：开源免费、S3 兼容、私有部署、高性能
**缺点**：需要运维、无自带 CDN
**适用**：生产环境私有云部署

**Docker 部署**：
```bash
docker run -d \
  -p 9000:9000 -p 9001:9001 \
  --name minio \
  -v /data/minio:/data \
  -e "MINIO_ROOT_USER=admin" \
  -e "MINIO_ROOT_PASSWORD=your_password" \
  minio/minio server /data --console-address ":9001"
```

### 云对象存储（OSS/COS/S3）

**优点**：高可用、无限扩展、自带 CDN、图片处理
**缺点**：需要付费、第三方依赖
**适用**：大规模生产环境

### 混合方案架构（推荐）⭐

**接口设计**：
```csharp
public interface IFileStorage
{
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType);
    Task<bool> DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
    string GetFileUrl(string filePath);
    Task<bool> ExistsAsync(string filePath);
}

// 实现类
- LocalFileStorage    // 本地存储
- MinioFileStorage    // MinIO 存储
- OssFileStorage      // 阿里云 OSS（可选）
```

**配置示例**：
```json
{
  "FileStorage": {
    "Type": "Local",  // 切换：Local / MinIO / OSS
    "Local": {
      "BasePath": "DataBases/Uploads",
      "BaseUrl": "/uploads"
    },
    "MinIO": {
      "Endpoint": "localhost:9000",
      "BucketName": "radish-uploads",
      "AccessKey": "admin",
      "SecretKey": "password",
      "UseSSL": false
    }
  }
}
```

---

## 🚀 上传方式对比

### 方式 1：直接上传到服务器（推荐 MVP）⭐

**流程**：浏览器 → API 服务器 → 存储后端

**优点**：实现简单、易于控制、便于审计
**缺点**：占用服务器带宽、大文件慢
**适用**：小文件（< 5MB）、中低并发

**实现示例**：
```csharp
[HttpPost]
[RequestSizeLimit(5_242_880)] // 5MB
public async Task<IActionResult> Upload(IFormFile file)
{
    // 1. 校验文件类型和大小
    if (!ValidateFile(file))
        return BadRequest("文件校验失败");

    // 2. 上传到存储
    using var stream = file.OpenReadStream();
    var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType);

    // 3. 保存记录
    var attachment = new Attachment { /* ... */ };
    await _attachmentService.AddAsync(attachment);

    return Ok(new { url = result.Url, id = attachment.Id });
}
```

### 方式 2：前端直传 OSS（生产环境优化）

**流程**：浏览器 → 获取签名 → 直接上传 OSS → 通知服务器

**优点**：不占服务器带宽、速度快、支持大文件
**缺点**：实现复杂、需要跨域配置
**适用**：大文件（> 10MB）、高并发场景

**核心思路**：后端生成上传签名，前端直接上传到 OSS，完成后回调通知。

### 方式 3：分片上传（大文件专用）

**适用**：超大文件（> 100MB）、需要断点续传

**实现思路**：
1. 前端：文件切片（每片 2MB）
2. 逐片上传到服务器
3. 服务器临时存储分片
4. 所有分片完成后，服务器合并
5. 清理临时分片

---

## 🔒 安全性设计（核心要点）

### 1. 文件类型校验

**白名单机制**：
```csharp
private static readonly Dictionary<string, string[]> AllowedTypes = new()
{
    ["image"] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
    ["document"] = new[] { ".pdf", ".doc", ".docx", ".txt" }
};
```

**Magic Number 检查**（文件头校验）：
```csharp
// 验证文件真实类型，防止扩展名伪装
private static readonly Dictionary<string, byte[]> FileSignatures = new()
{
    [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
    [".png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
    [".pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },
};
```

### 2. 文件大小限制

```csharp
[RequestSizeLimit(5_242_880)] // 5MB
[RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
public async Task<IActionResult> Upload(IFormFile file) { }
```

### 3. 文件名处理

- 使用雪花ID生成唯一文件名
- 按年月分目录存储
- 原始文件名仅用于展示，不作为存储文件名

### 4. 访问权限控制

**公开文件**：任何人可访问（帖子图片）
**私有文件**：需要鉴权（用户私密文档）
**临时授权 URL**：带签名的临时访问链接（有效期 1 小时）

### 5. 恶意文件防护

- **图片安全处理**：去除 EXIF 信息（可能含恶意代码）
- **病毒扫描**：可选集成 ClamAV（Phase 3）
- **内容审核**：调用云服务 API 检测违规内容（Phase 3）

### 6. 上传限流控制 ✅ **已实现（2025-12-24）**

为防止滥用和资源耗尽，实现了完整的上传限流机制：

**限流维度**：
1. **并发控制**：单用户最多 5 个文件同时上传
2. **速率限制**：每分钟最多 20 个文件
3. **日上传大小限制**：每天最多 100MB

**实现方式**：
```csharp
// 限流检查
var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, fileSize);
if (!isAllowed)
{
    return new MessageModel
    {
        IsSuccess = false,
        StatusCode = 429, // Too Many Requests
        MessageInfo = errorMessage
    };
}

// 记录上传开始（增加并发计数）
await _rateLimitService.RecordUploadStartAsync(userId, uploadId);

// 上传完成后记录（减少并发计数，增加速率和大小计数）
await _rateLimitService.RecordUploadCompleteAsync(userId, uploadId, fileSize);
```

**配置选项**（`appsettings.json`）：
```json
{
  "UploadRateLimit": {
    "Enable": true,
    "MaxConcurrentUploads": 5,
    "MaxUploadsPerMinute": 20,
    "MaxDailyUploadSize": 104857600  // 100MB
  }
}
```

**错误响应示例**：
```json
{
  "isSuccess": false,
  "statusCode": 429,
  "messageInfo": "您当前有 5 个文件正在上传，已达到并发上传限制（最多 5 个）"
}
```

**统计查询接口**：
```http
GET /api/v1/Attachment/GetUploadStatistics
Authorization: Bearer {token}

Response:
{
  "currentConcurrentUploads": 2,
  "uploadsThisMinute": 5,
  "uploadedSizeToday": 52428800,
  "uploadedSizeTodayFormatted": "50 MB",
  "maxConcurrentUploads": 5,
  "maxUploadsPerMinute": 20,
  "maxDailyUploadSize": 104857600,
  "maxDailyUploadSizeFormatted": "100 MB"
}
```

**技术特点**：
- 基于 Redis/内存缓存实现
- 自动过期机制（并发：1小时，速率：1分钟，日大小：当天结束）
- 用户隔离（不同用户的限流独立）
- 可配置开关（Enable 参数）
- 友好的错误提示（包含具体数值和剩余配额）

---

## ⚡ 性能优化策略

### 1. 图片处理

**上传时处理（推荐）**：
- 生成缩略图（150x150）
- 生成多尺寸（small/medium/large）
- 压缩原图（JPEG 质量 85%）
- 可选：添加水印

**使用 ImageSharp 库**：
```bash
dotnet add package SixLabors.ImageSharp --version 3.1.0
```

### 2. CDN 加速（生产环境）

- 配置 CDN 域名
- 设置缓存策略（`Cache-Control: public, max-age=31536000`）
- OSS 内置图片处理参数（缩略图/水印）

### 3. 懒加载和渐进式加载

```tsx
// 列表页：加载缩略图
<img src={getThumbnailUrl(image.url)} loading="lazy" />

// 点击查看：加载原图
<img src={getOriginalUrl(image.url)} />
```

### 4. 上传优化

- **前端压缩**：使用 `browser-image-compression` 库压缩后上传
- **进度显示**：`axios` 的 `onUploadProgress` 回调
- **错误重试**：自动重试 3 次，指数退避

---

## 🗄️ 数据库设计

### Attachment 表（附件表）

```csharp
/// <summary>
/// 附件表
/// </summary>
[SugarTable("Attachment")]
public class Attachment : RootEntityTKey<long>
{
    /// <summary>原始文件名</summary>
    [SugarColumn(Length = 255)]
    public string OriginalName { get; set; }

    /// <summary>存储文件名（GUID）</summary>
    [SugarColumn(Length = 100)]
    public string StoredName { get; set; }

    /// <summary>文件扩展名</summary>
    [SugarColumn(Length = 20)]
    public string Extension { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>MIME 类型</summary>
    [SugarColumn(Length = 100)]
    public string MimeType { get; set; }

    /// <summary>文件哈希值（MD5/SHA256）</summary>
    [SugarColumn(Length = 64)]
    public string? FileHash { get; set; }

    /// <summary>存储类型（Local/OSS/MinIO）</summary>
    [SugarColumn(Length = 20)]
    public string StorageType { get; set; }

    /// <summary>存储路径（相对路径）</summary>
    [SugarColumn(Length = 500)]
    public string StoragePath { get; set; }

    /// <summary>缩略图路径</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ThumbnailPath { get; set; }

    /// <summary>访问 URL</summary>
    [SugarColumn(Length = 1000)]
    public string Url { get; set; }

    /// <summary>上传者 ID</summary>
    public long UploaderId { get; set; }

    /// <summary>上传者名称</summary>
    [SugarColumn(Length = 50)]
    public string UploaderName { get; set; }

    /// <summary>业务类型（Post/Comment/Avatar/Document）</summary>
    [SugarColumn(Length = 50)]
    public string BusinessType { get; set; }

    /// <summary>业务 ID（如 PostId、CommentId）</summary>
    public long? BusinessId { get; set; }

    /// <summary>是否公开</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>下载次数</summary>
    public int DownloadCount { get; set; } = 0;

    /// <summary>内容审核状态（Pending/Pass/Reject）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? AuditStatus { get; set; }

    /// <summary>内容审核结果</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? AuditResult { get; set; }

    /// <summary>备注</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}
```

### 索引设计
```csharp
// 创建索引
[SugarIndex("idx_uploader", nameof(UploaderId), OrderByType.Asc)]
[SugarIndex("idx_business", nameof(BusinessType) + "," + nameof(BusinessId), OrderByType.Asc)]
[SugarIndex("idx_hash", nameof(FileHash), OrderByType.Asc)]
```

### ViewModel
```csharp
public class AttachmentVo
{
    public long Id { get; set; }
    public string OriginalName { get; set; }
    public long FileSize { get; set; }
    public string Extension { get; set; }
    public string Url { get; set; }
    public string ThumbnailUrl { get; set; }
    public string UploaderName { get; set; }
    public string CreateTime { get; set; }
    public int DownloadCount { get; set; }
}
```

---

## 🎯 推荐实施方案

### 阶段 1：MVP（当前开发阶段）

#### 存储方案
- **本地文件系统存储**
- 目录结构：`DataBases/Uploads/{Category}/{Year}/{Month}/{UniqueFileName}`
- 静态文件中间件访问

#### 上传方式
- **直接上传到 API 服务器**
- 使用 `IFormFile` 接收文件
- 基础的文件校验（类型、大小）

#### 图片处理
- 基础压缩（质量 85%）
- 生成缩略图（150x150）

#### 安全措施
- 文件类型白名单
- 文件大小限制
- 文件名随机化

#### 实现重点
1. ✅ 定义统一的 `IFileStorage` 接口
2. ✅ 实现 `LocalFileStorage` 本地存储
3. ✅ 创建 `Attachment` 表和相关 Service
4. ✅ 提供上传 API（`/api/v1/Upload`）
5. ✅ 集成到 MarkdownEditor（图片按钮）

**优点**：
- 快速实现，专注核心功能
- 无额外成本
- 开发调试方便

---

### 阶段 2：生产优化（部署上线前）

#### 存储方案
- **迁移到阿里云 OSS** 或 **自建 MinIO**
- 保持接口不变，只改配置

#### 上传方式
- **前端直传 OSS**（大文件）
- **服务器中转**（小文件）

#### 图片处理
- OSS 自带图片处理（多尺寸、水印）
- 或自建图片处理服务

#### CDN 加速
- 配置 CDN 域名
- 设置缓存策略

#### 安全增强
- 内容审核（调用云服务 API）
- 访问频率限制
- 临时授权 URL

---

## 🤔 待讨论的问题与决策记录

### ✅ 已确认的决策

#### 第一批决策（2025-12-20 上午）

**1. 存储方案选择** ✅
- 采用**可配置的混合方案**
- 开发环境：本地文件系统（`DataBases/Uploads/`）
- 生产环境：MinIO（可选 docker-compose 部署或远程 OSS）
- 架构原则：定义统一的 `IFileStorage` 接口，通过配置文件切换实现

**2. 上传方式** ✅
- 直接上传到 API 服务器，由服务器根据配置路由到存储后端
- 当前实现：同步上传（适合 < 50MB 文件）
- 架构保证：设计必须兼容未来的分片上传（预留 `UploadChunk` / `MergeChunks` API）

**3. MVP 文件类型支持** ✅
- 同时支持**图片和文档**
- 图片类型：`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`
- 文档类型：`.pdf`, `.doc`, `.docx`, `.txt`
- 扩展性：允许的文件扩展名通过配置文件管理（白名单机制）

**4. 文件大小限制（可配置）** ✅
- 头像：2MB
- 图片：5MB
- 文档：10MB
- 配置路径：`appsettings.json` → `FileStorage:MaxFileSize`

**5. 图片处理时机** ✅
- 上传时同步处理
- 理由：性能更好、用户体验佳、便于缓存
- 处理内容：缩略图生成、多尺寸、压缩、可选水印

**6. 文件删除策略** ✅
- 软删除 + 自动清理机制
- 标记 `IsDeleted = true` 和 `DeleteTime`
- 定时任务清理过期文件（默认：30 天）

#### 第二批决策（2025-12-20 晚上）

**7. 图片处理实现方案** ✅
- **混合架构**：同时提供 C# 和 Rust 两种实现
- **默认使用**：C# (ImageSharp)
- **Rust 实现**：作为高性能备选方案，方便切换测试
- **切换方式**：通过配置文件 `ImageProcessing:UseRustExtension`
- **Rust 项目名称**：`radish-lib`（统一的 Rust 扩展库）
- **项目位置**：`Radish.Core/radish-lib`
- **MVP 实现**：图片加水印算法

**8. 水印具体设计** ✅
- **默认类型**：文字水印
- **水印内容**：`"Radish"`
- **位置**：右下角
- **透明度**：50%
- **字体大小**：相对图片宽度的 5%
- **用户选择**：上传时可选择是否添加水印
- **可配置项**：内容、位置、透明度、字体大小、颜色

**9. 内容审核方案** ✅
- **审核方式**：本地算法 + 人工审核
- **开发阶段**：先不实现审核功能，只记录上传日志
- **未来实现**：
  - Phase 1：日志记录
  - Phase 2：本地 NSFW 模型（ONNX Runtime）
  - Phase 3：人工审核工具（管理后台）

**10. 文件去重策略** ✅
- **实现去重**：基于 SHA256 哈希
- **去重逻辑**：
  1. 上传时计算文件哈希
  2. 查询数据库是否存在相同哈希
  3. 存在则复用文件，只创建新的附件记录
  4. 不存在则上传文件并保存记录
- **性能优化**：哈希计算考虑使用 Rust 扩展（计算密集型操作）

**11. 分片上传触发条件** ✅
- **可配置**：通过 `ChunkedUpload:Threshold` 配置
- **默认阈值**：50MB（50 * 1024 * 1024 = 52428800 字节）
- **分片大小**：2MB / 片
- **实施阶段**：Phase 1 预留 API 和配置，Phase 2 实现

**12. 并发上传限制** ✅
- **单用户并发**：最多 5 个文件同时上传
- **速率限制**：每分钟最多 20 个文件
- **总大小限制**：每天最多上传 100MB
- **用户分级**：暂不区分，所有用户统一限制
- **实现方式**：Redis 计数器（或内存缓存单机模式）

**13. 临时文件清理** ✅
- **临时目录**：`DataBases/Uploads/Temp/`
- **清理策略**：
  - 定时任务：每小时执行一次
  - 保留时间：2 小时未完成的上传视为失败
  - 分片文件：24 小时后清理
- **配置项**：`TempFileCleanup:IntervalMinutes`, `RetentionHours`

**14. 错误处理和重试** ✅
- **前端重试**：
  - 自动重试 3 次
  - 指数退避：1s, 2s, 4s
  - 超过 3 次提示用户
- **后端错误码**：
  - 507：磁盘空间不足
  - 415：文件类型不支持
  - 413：文件过大
  - 503：存储服务不可用

**15. Rust 扩展架构** ✅
- **项目名称**：`radish-lib`（统一的 Rust 扩展库）
- **项目位置**：`Radish.Core/radish-lib`
- **重构现有**：将现有的 `test_lib` 重构为正式的 `radish-lib`
- **初期功能**：
  - 图片加水印（`add_watermark`）
  - 文件哈希计算（`calculate_file_hash`，可选）
- **调用方式**：C# DllImport
- **配置切换**：`ImageProcessing:UseRustExtension = true/false`

---

### 📋 完整配置文件示例

根据以上所有决策，完整的 `appsettings.json` 配置示例如下：

```json
{
  "FileStorage": {
    "Type": "Local",  // Local / MinIO / OSS (可切换)

    // 文件大小限制（字节）
    "MaxFileSize": {
      "Avatar": 2097152,      // 2MB
      "Image": 5242880,       // 5MB
      "Document": 10485760    // 10MB
    },

    // 允许的文件扩展名（白名单）
    "AllowedExtensions": {
      "Image": [".jpg", ".jpeg", ".png", ".gif", ".webp"],
      "Document": [".pdf", ".doc", ".docx", ".txt"]
    },

    // 本地存储配置
    "Local": {
      "BasePath": "DataBases/Uploads",
      "BaseUrl": "/uploads"
    },

    // MinIO 配置（生产环境）
    "MinIO": {
      "Endpoint": "localhost:9000",
      "BucketName": "radish-uploads",
      "AccessKey": "admin",
      "SecretKey": "your_password",
      "UseSSL": false
    },

    // 阿里云 OSS 配置（可选）
    "OSS": {
      "Endpoint": "oss-cn-hangzhou.aliyuncs.com",
      "BucketName": "radish-uploads",
      "AccessKeyId": "",
      "AccessKeySecret": "",
      "Domain": "https://cdn.example.com"
    },

    // 图片处理配置
    "ImageProcessing": {
      "UseRustExtension": false,  // true: 使用 Rust, false: 使用 C#
      "GenerateThumbnail": true,
      "ThumbnailSize": { "Width": 150, "Height": 150 },
      "GenerateMultipleSizes": true,
      "Sizes": {
        "Small": { "Width": 400, "Height": 300 },
        "Medium": { "Width": 800, "Height": 600 },
        "Large": { "Width": 1200, "Height": 900 }
      },
      "CompressQuality": 85,  // JPEG 压缩质量 (1-100)
      "RemoveExif": true      // 移除 EXIF 信息
    },

    // 水印配置
    "Watermark": {
      "Enable": false,  // 全局开关（用户仍可选择）
      "Type": "Text",   // Text / Image
      "Text": {
        "Content": "Radish",
        "Position": "BottomRight",  // TopLeft, TopRight, BottomLeft, BottomRight, Center
        "FontSize": 24,
        "FontSizeRelative": 0.05,  // 相对图片宽度的 5%
        "Color": "#FFFFFF",
        "Opacity": 0.5
      },
      "Image": {
        "Path": "wwwroot/images/watermark.png",
        "Position": "BottomRight",
        "Scale": 0.1  // 图片宽度的 10%
      }
    },

    // 文件去重配置
    "Deduplication": {
      "Enable": true,
      "HashAlgorithm": "SHA256",  // MD5 / SHA256
      "UseRustExtension": false   // 哈希计算是否使用 Rust
    },

    // 分片上传配置
    "ChunkedUpload": {
      "Enable": false,  // Phase 1 关闭，Phase 2 启用
      "Threshold": 52428800,  // 50MB (50 * 1024 * 1024)
      "ChunkSize": 2097152     // 2MB (2 * 1024 * 1024)
    },

    // 并发上传限制
    "RateLimit": {
      "MaxConcurrentUploads": 5,      // 单用户最多同时上传
      "MaxUploadsPerMinute": 20,       // 每分钟最多上传文件数
      "MaxDailyUploadSize": 104857600  // 每天最多上传 100MB
    },

    // 临时文件清理
    "TempFileCleanup": {
      "Enable": true,
      "IntervalMinutes": 60,   // 每小时执行一次
      "RetentionHours": 2,     // 保留 2 小时
      "ChunkRetentionHours": 24 // 分片保留 24 小时
    },

    // 软删除文件清理
    "DeletedFileCleanup": {
      "Enable": true,
      "RetentionDays": 30,  // 软删除后保留 30 天
      "CleanupTime": "03:00"  // 每天凌晨 3 点执行
    },

    // 内容审核配置（Phase 1 不启用）
    "ContentAudit": {
      "Enable": false,
      "Type": "Local",  // Local / Cloud / Manual
      "AutoReject": false  // 是否自动拒绝违规内容
    }
  }
}
```

---

### 🦀 Rust 扩展架构详细设计

根据决策，将现有的 `test_lib` 重构为正式的 `radish-lib` 统一扩展库。

#### 项目结构

```
Radish.Core/
└── native/
    └── rust/
        └── radish-lib/          # 统一的 Rust 扩展库
            ├── Cargo.toml
            ├── src/
            │   ├── lib.rs       # 入口和 FFI 导出
            │   ├── image/       # 图片处理模块
            │   │   ├── mod.rs
            │   │   ├── watermark.rs    # 水印功能
            │   │   ├── resize.rs       # 缩放功能（可选）
            │   │   └── compress.rs     # 压缩功能（可选）
            │   ├── hash/        # 哈希计算模块
            │   │   ├── mod.rs
            │   │   └── file_hash.rs    # 文件哈希
            │   └── utils/       # 工具函数
            │       └── mod.rs
            ├── build.sh         # Linux/macOS 编译脚本
            ├── build.ps1        # Windows 编译脚本
            └── README.md        # Rust 扩展使用说明
```

#### Cargo.toml 配置

```toml
[package]
name = "radish-lib"
version = "0.1.0"
edition = "2021"

[lib]
crate-type = ["cdylib"]  # 编译为动态库

[dependencies]
image = "0.25"            # 图片处理
imageproc = "0.25"        # 图片处理（水印、文字）
rusttype = "0.9"          # 字体渲染
sha2 = "0.10"             # SHA256 哈希

[profile.release]
opt-level = 3             # 最大优化
lto = true                # 链接时优化
codegen-units = 1         # 单个代码生成单元（更好的优化）
```

#### Rust 实现示例（MVP: 图片加水印）

**src/lib.rs**：
```rust
use std::ffi::{CStr, CString};
use std::os::raw::c_char;

mod image;
mod hash;

// 导出图片加水印函数
#[no_mangle]
pub extern "C" fn add_text_watermark(
    input_path: *const c_char,
    output_path: *const c_char,
    text: *const c_char,
    font_size: u32,
    opacity: f32,
    position: u8,  // 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight, 4=Center
) -> i32 {
    // 安全转换 C 字符串
    let input = unsafe { CStr::from_ptr(input_path).to_str().unwrap() };
    let output = unsafe { CStr::from_ptr(output_path).to_str().unwrap() };
    let watermark_text = unsafe { CStr::from_ptr(text).to_str().unwrap() };

    // 调用内部实现
    match image::watermark::add_watermark(input, output, watermark_text, font_size, opacity, position) {
        Ok(_) => 0,   // 成功
        Err(e) => {
            eprintln!("Error: {}", e);
            -1  // 失败
        }
    }
}

// 导出文件哈希计算函数（可选）
#[no_mangle]
pub extern "C" fn calculate_file_sha256(
    file_path: *const c_char,
    hash_output: *mut c_char,
    output_len: usize,
) -> i32 {
    let path = unsafe { CStr::from_ptr(file_path).to_str().unwrap() };

    match hash::file_hash::calculate_sha256(path) {
        Ok(hash) => {
            let c_hash = CString::new(hash).unwrap();
            let bytes = c_hash.as_bytes_with_nul();
            if bytes.len() <= output_len {
                unsafe {
                    std::ptr::copy_nonoverlapping(bytes.as_ptr(), hash_output as *mut u8, bytes.len());
                }
                0
            } else {
                -2  // 缓冲区太小
            }
        }
        Err(e) => {
            eprintln!("Error: {}", e);
            -1
        }
    }
}
```

**src/image/watermark.rs**：
```rust
use image::{DynamicImage, GenericImageView, Rgba};
use imageproc::drawing::draw_text_mut;
use rusttype::{Font, Scale};

pub fn add_watermark(
    input_path: &str,
    output_path: &str,
    text: &str,
    font_size: u32,
    opacity: f32,
    position: u8,
) -> Result<(), Box<dyn std::error::Error>> {
    // 加载图片
    let mut img = image::open(input_path)?;

    // 加载字体（需要内嵌或指定字体文件）
    let font_data = include_bytes!("../../fonts/DejaVuSans.ttf");
    let font = Font::try_from_bytes(font_data as &[u8]).unwrap();

    // 计算文字位置
    let (img_width, img_height) = img.dimensions();
    let scale = Scale::uniform(font_size as f32);

    // 根据 position 计算坐标
    let (x, y) = match position {
        0 => (10, 10),  // TopLeft
        1 => (img_width - 200, 10),  // TopRight
        2 => (10, img_height - 50),  // BottomLeft
        3 => (img_width - 200, img_height - 50),  // BottomRight
        4 => (img_width / 2 - 50, img_height / 2),  // Center
        _ => (10, 10),
    };

    // 绘制半透明文字
    let color = Rgba([255u8, 255u8, 255u8, (255.0 * opacity) as u8]);
    draw_text_mut(&mut img, color, x, y, scale, &font, text);

    // 保存图片
    img.save(output_path)?;

    Ok(())
}
```

#### C# 调用封装

**Radish.Core/NativeExtensions/RustImageProcessor.cs**：
```csharp
using System.Runtime.InteropServices;

namespace Radish.Core.NativeExtensions;

/// <summary>
/// Rust 图片处理扩展
/// </summary>
public class RustImageProcessor : IImageProcessor
{
    private const string LibraryName = "radish_lib";

    [DllImport(LibraryName, EntryPoint = "add_text_watermark")]
    private static extern int AddTextWatermarkNative(
        string inputPath,
        string outputPath,
        string text,
        uint fontSize,
        float opacity,
        byte position
    );

    [DllImport(LibraryName, EntryPoint = "calculate_file_sha256")]
    private static extern int CalculateFileSha256Native(
        string filePath,
        [Out] StringBuilder hashOutput,
        int outputLen
    );

    public async Task<string> AddWatermarkAsync(
        string inputPath,
        string text,
        WatermarkOptions options
    )
    {
        var outputPath = GenerateOutputPath(inputPath);

        var result = AddTextWatermarkNative(
            inputPath,
            outputPath,
            text,
            (uint)options.FontSize,
            options.Opacity,
            (byte)options.Position
        );

        if (result != 0)
            throw new ImageProcessingException($"Rust watermark failed: code {result}");

        return outputPath;
    }

    public string CalculateFileHash(string filePath)
    {
        var buffer = new StringBuilder(65);  // SHA256 = 64 chars + null

        var result = CalculateFileSha256Native(filePath, buffer, buffer.Capacity);

        if (result != 0)
            throw new Exception($"Rust hash calculation failed: code {result}");

        return buffer.ToString();
    }
}
```

#### 编译脚本

**build.sh** (Linux/macOS):
```bash
#!/bin/bash
cd "$(dirname "$0")"

echo "Building radish-lib for Rust..."
cargo build --release

# 复制到输出目录
cp target/release/libradish_lib.so ../../../Radish.Api/bin/Debug/net10.0/ 2>/dev/null || true
cp target/release/libradish_lib.dylib ../../../Radish.Api/bin/Debug/net10.0/ 2>/dev/null || true

echo "Build complete!"
```

**build.ps1** (Windows):
```powershell
Set-Location $PSScriptRoot

Write-Host "Building radish-lib for Rust..." -ForegroundColor Green
cargo build --release

# 复制到输出目录
Copy-Item "target\release\radish_lib.dll" "..\..\..\Radish.Api\bin\Debug\net10.0\" -Force

Write-Host "Build complete!" -ForegroundColor Green
```

#### 配置切换实现

**Radish.Extension/ImageProcessorFactory.cs**：
```csharp
public class ImageProcessorFactory
{
    public static IImageProcessor Create(ImageProcessingOptions options)
    {
        if (options.UseRustExtension)
        {
            // 检查 Rust 库是否存在
            var rustLibPath = GetRustLibraryPath();
            if (File.Exists(rustLibPath))
            {
                Log.Information("Using Rust image processor");
                return new RustImageProcessor(options);
            }
            else
            {
                Log.Warning("Rust library not found, fallback to C# processor");
                return new CSharpImageProcessor(options);
            }
        }

        return new CSharpImageProcessor(options);
    }
}
```

---

## 📝 实施计划

### Phase 1: MVP 基础功能（预计 5-7 天）

#### 后端开发（3-4 天）

**1. 数据模型和存储接口**
   - [ ] 创建 `Attachment` 实体和数据库迁移
   - [ ] 定义 `IFileStorage` 接口
   - [ ] 实现 `LocalFileStorage`（本地存储）
   - [ ] 定义 `IImageProcessor` 接口
   - [ ] 实现 `CSharpImageProcessor`（使用 ImageSharp）

**2. Rust 扩展基础架构（与上面并行，1-2 天）**
   - [ ] 重构 `test_lib` 为 `radish-lib`
   - [ ] 实现图片加水印功能（Rust）
   - [ ] 实现文件哈希计算（Rust，可选）
   - [ ] 创建 C# FFI 调用封装
   - [ ] 实现 `RustImageProcessor`
   - [ ] 实现 `ImageProcessorFactory`（配置切换）
   - [ ] 编写编译脚本（build.sh / build.ps1）

**3. 业务逻辑**
   - [ ] 创建 `AttachmentService`（CRUD + 上传逻辑）
   - [ ] 文件校验（类型、大小、Magic Number）
   - [ ] 文件去重逻辑（SHA256 哈希）
   - [ ] 图片处理（缩略图、多尺寸、压缩）
   - [ ] 文件名生成（雪花ID + 年月目录）

**4. API 端点**
   - [ ] `POST /api/v1/Upload/Image` - 上传图片（可选水印）
   - [ ] `POST /api/v1/Upload/Document` - 上传文档
   - [ ] `GET /api/v1/Upload/{id}` - 获取文件信息
   - [ ] `DELETE /api/v1/Upload/{id}` - 软删除文件
   - [ ] 配置静态文件中间件
   - [ ] 预留分片上传 API（不实现）
     - `POST /api/v1/Upload/Chunk`
     - `POST /api/v1/Upload/Merge`

#### 前端开发（2-3 天）

**1. 上传组件**
   - [ ] 创建 `FileUpload` 组件（拖拽 + 点击上传）
   - [ ] 上传进度显示
   - [ ] 图片预览
   - [ ] 错误提示和重试逻辑
   - [ ] 水印选项（用户可选）

**2. 集成到 MarkdownEditor**
   - [ ] 图片按钮点击触发上传
   - [ ] 上传成功后插入 Markdown 图片语法
   - [ ] 支持粘贴图片上传（Ctrl+V）
   - [ ] 支持拖拽图片上传

**3. 文件管理界面**（可选，Phase 2 可做）
   - [ ] 我的附件列表
   - [ ] 删除附件
   - [ ] 查看附件详情

#### 配置和测试

**1. 配置文件**
   - [ ] 添加完整的 `FileStorage` 配置到 `appsettings.json`
   - [ ] 添加 `appsettings.Local.json` 示例

**2. 测试**
   - [ ] 单元测试（AttachmentService）
   - [ ] 集成测试（上传 API）
   - [ ] Rust 扩展性能对比测试
   - [ ] 文件去重测试

---

### Phase 2: 生产环境支持（预计 3-4 天）

**1. MinIO 集成**
   - [x] 实现 `MinioFileStorage`
   - [ ] Docker Compose 配置（MinIO + Radish）
   - [ ] MinIO 初始化脚本（创建 Bucket、设置权限）
   - [x] 配置切换测试（Local ↔ MinIO）

**2. 图片处理增强**
   - [x] 多尺寸生成（Small, Medium, Large）
   - [x] 水印功能完整实现（文字 + 图片水印）
   - [ ] 图片格式转换（WebP）
   - [x] Rust vs C# 性能对比测试
   - [x] 根据测试结果决定默认实现

**3. 定时任务（Hangfire）**
   - [x] 集成 Hangfire
   - [x] 软删除文件清理任务（每天凌晨 3 点）
   - [x] 临时文件清理任务（每小时）
   - [x] 任务监控和日志

**4. 并发控制和限流** ✅ **已完成（2025-12-24）**
   - [x] Redis 集成（或内存缓存）
   - [x] 单用户并发限制（5 个）
   - [x] 速率限制（20 文件/分钟）
   - [x] 日上传大小限制（100MB）
   - [x] 限流服务实现（`IUploadRateLimitService`）
   - [x] 集成到上传接口（`UploadImage` / `UploadDocument`）
   - [x] 上传统计查询接口（`GetUploadStatistics`）
   - [x] 单元测试（`UploadRateLimitServiceTest`）
   - [x] HTTP 测试用例
   - [x] 配置选项（`UploadRateLimitOptions`）

---

### Phase 3: 高级特性（按需实施）

**1. 分片上传（2-3 天）**
   - [ ] 实现 `POST /api/v1/Upload/Chunk`
   - [ ] 实现 `POST /api/v1/Upload/Merge`
   - [ ] 前端分片逻辑（2MB/片）
   - [ ] 断点续传支持
   - [ ] 进度持久化

**2. 内容审核（3-5 天）**
   - [ ] 本地 NSFW 模型集成（ONNX Runtime）
   - [ ] 人工审核工具（管理后台）
   - [ ] 审核工作流（待审核 → 通过/拒绝）
   - [ ] 审核日志和统计

**3. Rust 扩展增强（按需）**
   - [ ] 图片缩放（Rust）
   - [ ] 图片压缩（Rust）
   - [ ] 图片格式转换（Rust）
   - [ ] 完整性能测试和优化

**4. 安全增强**
   - [ ] 病毒扫描（ClamAV 集成）
   - [ ] 临时授权 URL（带签名）
   - [ ] 访问日志和审计
   - [ ] 防盗链（Referer 检查）

**5. CDN 集成（生产环境）**
   - [ ] CDN 域名配置
   - [ ] 缓存策略优化
   - [ ] OSS 图片处理参数

---

### 技术准备清单

#### NuGet 包
```bash
# 图片处理（必需）
dotnet add package SixLabors.ImageSharp --version 3.1.0 --project Radish.Core

# MinIO SDK（Phase 2）
dotnet add package Minio --version 6.0.0 --project Radish.Infrastructure

# 定时任务（Phase 2）
dotnet add package Hangfire.AspNetCore --version 1.8.0 --project Radish.Api
dotnet add package Hangfire.SqlSugar --version 1.0.0 --project Radish.Api
```

#### npm 包
```bash
# 前端图片压缩（可选）
npm install browser-image-compression --workspace=radish.client

# Axios 已安装，无需额外安装
```

#### Rust 工具链
```bash
# 安装 Rust（如果还没有）
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# 验证安装
rustc --version
cargo --version

# 添加 Windows MSVC 工具链（Windows 用户）
rustup target add x86_64-pc-windows-msvc
```

---

### 开发优先级建议

**第一周（Phase 1）**：
- Day 1-2：后端核心功能（IFileStorage, LocalFileStorage, Attachment, AttachmentService）
- Day 2-3：Rust 扩展基础架构（重构 test_lib, 实现水印, FFI 封装）
- Day 3-4：图片处理和文件去重
- Day 4-5：前端上传组件和 API 集成
- Day 5：测试和文档

**第二周（Phase 2）**：
- Day 6-7：MinIO 集成和 Docker 配置
- Day 8：定时任务和清理机制
- Day 9：并发控制和限流
- Day 10：性能测试和优化

**后续（Phase 3）**：
- 按需实施分片上传
- 按需实施内容审核
- 按需增强 Rust 扩展

---

### 成功标准

**Phase 1 完成标准**：
- [x] 可以上传图片和文档到本地存储
- [x] 自动生成缩略图和压缩原图
- [x] 文件去重功能正常工作
- [x] 水印功能可选配置
- [x] C# 和 Rust 两种实现都能正常工作且可切换
- [x] 前端可以上传文件并显示进度
- [x] MarkdownEditor 集成图片上传

**Phase 2 完成标准**：
- [x] MinIO 可正常使用并通过配置切换
- [x] 定时清理任务正常运行
- [x] 并发限制生效
- [x] 性能测试报告完成

**Phase 3 完成标准**：
- [x] 分片上传支持大文件（> 50MB）
- [x] 内容审核功能可用（如果实施）

---

---

## 📚 参考资料

### 技术文档
- [ASP.NET Core 文件上传](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads)
- [SixLabors.ImageSharp 文档](https://docs.sixlabors.com/articles/imagesharp/index.html)
- [MinIO 文档](https://min.io/docs/minio/linux/index.html)
- [阿里云 OSS 文档](https://help.aliyun.com/product/31815.html)

### 开源项目参考
- [Uppy - 文件上传库](https://uppy.io/)
- [FilePond - 优雅的文件上传](https://pqina.nl/filepond/)

---

**文档状态**：设计完成，待实施
**最后更新**：2025-12-20
**版本**：v1.0
