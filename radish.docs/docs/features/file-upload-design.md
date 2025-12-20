# 文件上传功能设计方案

## 📋 概述

本文档详细描述 Radish 项目的文件上传功能设计方案，包括存储方案选型、上传方式对比、安全性设计、性能优化等内容。

**目标**：
- 🎯 支持图片、文档等多种文件类型上传
- 🔒 确保上传安全性和数据完整性
- ⚡ 优化上传性能和用户体验
- 🏗️ 易于扩展和维护的架构设计

**适用场景**：
- 论坛帖子配图
- 用户头像上传
- 评论附件
- 文档分享
- 富文本编辑器图片插入

---

## 📦 存储方案对比

### 方案 1：本地文件系统存储

**适用场景**：小型项目、开发测试环境

#### 优点
- ✅ **实现简单**：无需额外服务，代码直接操作文件系统
- ✅ **零成本**：不需要付费，只占用服务器磁盘空间
- ✅ **访问速度快**：局域网或本机访问，延迟低
- ✅ **完全可控**：文件完全在自己掌控之下，无第三方依赖
- ✅ **调试方便**：可直接查看文件内容，便于开发调试

#### 缺点
- ❌ **扩展性差**：单机存储容量有限，磁盘满了需要手动扩容
- ❌ **不支持分布式**：多服务器部署时，文件不能共享
- ❌ **备份容灾复杂**：需要自己实现备份策略和容灾方案
- ❌ **无 CDN 加速**：需要额外配置 Nginx 反向代理 + CDN
- ❌ **磁盘故障风险**：硬盘损坏可能导致文件丢失
- ❌ **带宽限制**：大量下载会占用服务器带宽

#### 目录结构设计
```
DataBases/Uploads/
├── Images/                    # 图片文件
│   ├── 2025/                 # 按年份分目录
│   │   ├── 12/               # 按月份分目录
│   │   │   ├── original/     # 原图
│   │   │   │   ├── 1234567890123456.jpg
│   │   │   │   └── 1234567890123457.png
│   │   │   ├── thumb/        # 缩略图 (150x150)
│   │   │   │   ├── 1234567890123456.jpg
│   │   │   ├── small/        # 小图 (400x300)
│   │   │   ├── medium/       # 中图 (800x600)
│   │   │   └── large/        # 大图 (1200x900)
├── Documents/                 # 文档文件
│   ├── 2025/12/
│   │   ├── xxx.pdf
│   │   └── yyy.docx
├── Avatars/                   # 用户头像
│   ├── 2025/12/
│   │   ├── user_1.jpg
│   │   └── user_2.png
└── Temp/                      # 临时文件（定期清理）
    ├── upload_xxx.tmp
```

#### 静态文件访问配置
```csharp
// Program.cs
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "DataBases", "Uploads")),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // 设置缓存
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
    }
});
```

**访问 URL**：`https://api.example.com/uploads/Images/2025/12/original/xxx.jpg`

---

### 方案 2：云对象存储（OSS/S3）

**适用场景**：生产环境、有一定规模的项目

#### 主流服务商对比

| 服务商 | 特点 | 适用场景 | 价格（参考） |
|--------|------|----------|--------------|
| **阿里云 OSS** | 国内访问快，生态完善 | 国内业务为主 | 存储：0.12元/GB/月<br>流量：0.5元/GB |
| **腾讯云 COS** | 与微信生态集成好 | 微信小程序等 | 存储：0.11元/GB/月<br>流量：0.5元/GB |
| **AWS S3** | 国际标准，生态最完善 | 国际业务 | 存储：$0.023/GB/月<br>流量：$0.09/GB |
| **七牛云 KODO** | 免费额度大，适合小项目 | 初创项目 | 10GB存储免费<br>10GB流量免费/月 |
| **MinIO** | 开源自建，S3兼容 | 私有云部署 | 免费（自建成本） |

#### 优点
- ✅ **高可用**：99.9999999% 数据可靠性，多副本自动备份
- ✅ **无限扩展**：不用担心存储容量，按需扩展
- ✅ **自带 CDN**：全球节点分发，访问速度快
- ✅ **按量付费**：用多少付多少，成本可控
- ✅ **图片处理**：内置缩略图、水印、裁剪、格式转换等功能
- ✅ **权限控制**：精细的访问控制（公开/私有/临时授权）
- ✅ **日志审计**：完善的访问日志和统计分析
- ✅ **生命周期管理**：自动删除过期文件，节省成本

#### 缺点
- ❌ **需要付费**：虽然不贵，但需要成本预算
- ❌ **学习成本**：需要了解 SDK 和 API 使用
- ❌ **第三方依赖**：依赖云服务商稳定性
- ❌ **网络要求**：需要服务器能访问公网

#### 阿里云 OSS 示例配置
```json
{
  "OSS": {
    "Endpoint": "oss-cn-hangzhou.aliyuncs.com",
    "BucketName": "radish-uploads",
    "AccessKeyId": "YOUR_ACCESS_KEY_ID",
    "AccessKeySecret": "YOUR_ACCESS_KEY_SECRET",
    "Domain": "https://cdn.example.com",  // 自定义域名
    "IsPrivate": false,  // 是否私有 Bucket
    "EnableCdn": true,   // 是否启用 CDN
    "ImageProcess": {
      "Thumb": "image/resize,m_fill,w_150,h_150",      // 缩略图
      "Small": "image/resize,m_lfit,w_400,h_300",      // 小图
      "Medium": "image/resize,m_lfit,w_800,h_600",     // 中图
      "Watermark": "image/watermark,text_UmFkaXNo"     // 水印
    }
  }
}
```

**访问 URL**：`https://cdn.example.com/images/xxx.jpg?x-oss-process=image/resize,w_400`

---

### 方案 3：MinIO（开源 S3 兼容存储）

**适用场景**：私有云部署、对数据安全有严格要求的场景

#### 优点
- ✅ **开源免费**：无需付费，代码开源可审计
- ✅ **S3 兼容**：兼容 AWS S3 API，易于迁移
- ✅ **私有部署**：数据完全掌控，不依赖第三方
- ✅ **性能优异**：高性能对象存储，支持 SSD
- ✅ **易于部署**：Docker 一键部署，开箱即用
- ✅ **分布式**：支持分布式部署，高可用

#### 缺点
- ❌ **运维成本**：需要自己维护服务器
- ❌ **无 CDN**：需要自己配置 CDN 或反向代理
- ❌ **备份成本**：需要自己实现备份策略

#### Docker 部署示例
```bash
# 单节点模式（开发环境）
docker run -d \
  -p 9000:9000 \
  -p 9001:9001 \
  --name minio \
  -v /data/minio:/data \
  -e "MINIO_ROOT_USER=admin" \
  -e "MINIO_ROOT_PASSWORD=your_password" \
  minio/minio server /data --console-address ":9001"
```

**访问 URL**：`http://localhost:9000/radish-uploads/images/xxx.jpg`

---

### 方案 4：混合方案（推荐）⭐

**核心思想**：定义统一的存储接口，通过配置切换不同的存储实现

#### 架构设计
```csharp
// 统一接口
public interface IFileStorage
{
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType);
    Task<bool> DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
    string GetFileUrl(string filePath);
    Task<bool> ExistsAsync(string filePath);
}

// 本地存储实现
public class LocalFileStorage : IFileStorage
{
    // 实现细节...
}

// OSS 存储实现
public class OssFileStorage : IFileStorage
{
    // 实现细节...
}

// MinIO 存储实现
public class MinioFileStorage : IFileStorage
{
    // 实现细节...
}

// 工厂类
public class FileStorageFactory
{
    public static IFileStorage Create(FileStorageOptions options)
    {
        return options.Type switch
        {
            StorageType.Local => new LocalFileStorage(options.Local),
            StorageType.OSS => new OssFileStorage(options.OSS),
            StorageType.MinIO => new MinioFileStorage(options.MinIO),
            _ => throw new NotSupportedException()
        };
    }
}
```

#### 配置文件
```json
{
  "FileStorage": {
    "Type": "Local",  // Local / OSS / MinIO
    "MaxFileSize": 5242880,  // 5MB
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx"],
    "Local": {
      "BasePath": "DataBases/Uploads",
      "BaseUrl": "/uploads"
    },
    "OSS": {
      "Endpoint": "oss-cn-hangzhou.aliyuncs.com",
      "BucketName": "radish-uploads",
      "AccessKeyId": "",
      "AccessKeySecret": "",
      "Domain": "https://cdn.example.com"
    },
    "MinIO": {
      "Endpoint": "localhost:9000",
      "BucketName": "radish-uploads",
      "AccessKey": "admin",
      "SecretKey": "",
      "UseSSL": false
    }
  }
}
```

#### 优点
- ✅ **灵活切换**：开发用本地，测试用 MinIO，生产用 OSS
- ✅ **易于迁移**：更换存储方案只需改配置
- ✅ **降低成本**：开发阶段不需要云存储费用
- ✅ **统一接口**：业务代码无需修改

#### 使用示例
```csharp
// Controller
public class UploadController : ControllerBase
{
    private readonly IFileStorage _fileStorage;

    public UploadController(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType);
        return Ok(new { url = result.Url });
    }
}
```

---

## 🚀 上传方式对比

### 方式 1：直接上传到应用服务器

#### 流程图
```
┌─────────┐    HTTP POST    ┌──────────────┐    保存文件    ┌─────────┐
│  前端   │ ──────────────> │ API 服务器   │ ───────────> │  存储   │
│ (浏览器) │                 │ (ASP.NET)    │              │(Local/OSS)│
└─────────┘                 └──────────────┘              └─────────┘
```

#### 优点
- ✅ **实现简单**：使用 `IFormFile` 即可，代码量少
- ✅ **易于控制**：完全掌控上传流程，便于添加业务逻辑
- ✅ **安全性好**：统一鉴权和校验，不易被绕过
- ✅ **便于审计**：所有上传都经过服务器，易于记录日志

#### 缺点
- ❌ **占用带宽**：文件流经服务器，占用上行带宽
- ❌ **性能瓶颈**：大量并发上传会给服务器带来压力
- ❌ **大文件慢**：大文件上传需要占用服务器连接时间
- ❌ **扩展性差**：服务器数量增加时，需要共享存储

#### 适用场景
- 小文件（< 5MB）
- 并发量不大（< 100 并发）
- 需要严格校验和审核
- 开发和测试环境

#### 实现示例
```csharp
[HttpPost]
[RequestSizeLimit(5_242_880)] // 5MB
public async Task<IActionResult> Upload(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("文件不能为空");

    // 1. 校验文件类型
    if (!IsAllowedExtension(file.FileName))
        return BadRequest("不支持的文件类型");

    // 2. 校验文件大小
    if (file.Length > 5_242_880)
        return BadRequest("文件大小超过限制");

    // 3. 上传文件
    using var stream = file.OpenReadStream();
    var result = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType);

    // 4. 保存记录到数据库
    var attachment = new Attachment
    {
        OriginalName = file.FileName,
        StoredName = result.StoredName,
        FileSize = file.Length,
        Url = result.Url,
        UploaderId = _currentUser.UserId
    };
    await _attachmentService.AddAsync(attachment);

    return Ok(new { url = result.Url, id = attachment.Id });
}
```

---

### 方式 2：前端直传 OSS（推荐生产环境）⭐

#### 流程图
```
┌─────────┐  1.请求签名   ┌──────────────┐
│  前端   │ ──────────> │ API 服务器   │
│ (浏览器) │ <────────── │ (ASP.NET)    │
└─────────┘  2.返回签名  └──────────────┘
     │
     │ 3.直接上传
     ↓
┌─────────┐  4.上传成功  ┌──────────────┐
│   OSS   │ ──────────> │ API 服务器   │
│ (阿里云) │             │ (回调/通知)   │
└─────────┘             └──────────────┘
```

#### 优点
- ✅ **不占用服务器带宽**：文件直接上传到 OSS，不经过服务器
- ✅ **上传速度快**：OSS 有多个节点，就近上传
- ✅ **支持大文件**：可上传 GB 级文件，支持断点续传
- ✅ **减轻服务器压力**：服务器只需要生成签名

#### 缺点
- ❌ **实现稍复杂**：需要前端集成 SDK，处理签名逻辑
- ❌ **跨域配置**：需要在 OSS 配置 CORS 规则
- ❌ **安全性要求高**：签名机制要设计好，防止滥用

#### 适用场景
- 生产环境
- 大文件上传（> 10MB）
- 高并发场景
- 需要 CDN 加速

#### 后端实现（生成签名）
```csharp
[HttpPost("upload/signature")]
public IActionResult GetUploadSignature([FromBody] SignatureRequest request)
{
    // 1. 生成唯一文件名
    var fileName = $"{DateTime.Now:yyyyMMdd}/{Guid.NewGuid()}{Path.GetExtension(request.FileName)}";

    // 2. 生成上传策略
    var policy = new
    {
        expiration = DateTime.UtcNow.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        conditions = new[]
        {
            new { bucket = "radish-uploads" },
            new[] { "content-length-range", 0, 5242880 }, // 最大 5MB
            new[] { "starts-with", "$key", "images/" }
        }
    };

    // 3. 计算签名
    var signature = CalculateSignature(policy);

    return Ok(new
    {
        accessKeyId = _ossConfig.AccessKeyId,
        policy = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(policy))),
        signature = signature,
        bucket = "radish-uploads",
        key = fileName,
        host = $"https://{_ossConfig.BucketName}.{_ossConfig.Endpoint}"
    });
}
```

#### 前端实现（直传 OSS）
```typescript
// 1. 获取签名
const getSignature = async (fileName: string) => {
  const response = await apiPost('/api/v1/Upload/signature', { fileName });
  return response.data;
};

// 2. 上传文件
const uploadToOss = async (file: File) => {
  const signature = await getSignature(file.name);

  const formData = new FormData();
  formData.append('OSSAccessKeyId', signature.accessKeyId);
  formData.append('policy', signature.policy);
  formData.append('signature', signature.signature);
  formData.append('key', signature.key);
  formData.append('file', file);

  await axios.post(signature.host, formData, {
    onUploadProgress: (e) => {
      const percent = Math.round((e.loaded * 100) / e.total);
      console.log(`上传进度: ${percent}%`);
    }
  });

  // 3. 通知后端
  const fileUrl = `${signature.host}/${signature.key}`;
  await apiPost('/api/v1/Upload/callback', {
    url: fileUrl,
    fileName: file.name,
    fileSize: file.size
  });

  return fileUrl;
};
```

---

### 方式 3：分片上传（大文件专用）

#### 适用场景
- 超大文件（> 100MB）
- 需要断点续传
- 网络不稳定环境

#### 优点
- ✅ **支持超大文件**：可上传 GB 甚至 TB 级文件
- ✅ **断点续传**：网络中断后可继续上传，不需要重头开始
- ✅ **并行上传**：多个分片可并行上传，提升速度
- ✅ **节省带宽**：失败只需重传出错的分片

#### 实现流程
```
1. 前端：将文件切片（如每片 2MB）
2. 前端：逐片上传，记录进度
3. 后端：接收分片，临时存储
4. 前端：所有分片上传完成后，通知后端合并
5. 后端：合并分片，生成最终文件
6. 后端：删除临时分片
```

#### 技术要点
```typescript
// 前端：文件切片
const CHUNK_SIZE = 2 * 1024 * 1024; // 2MB

const uploadFile = async (file: File) => {
  const chunks = Math.ceil(file.size / CHUNK_SIZE);
  const fileHash = await calculateHash(file); // MD5

  for (let i = 0; i < chunks; i++) {
    const start = i * CHUNK_SIZE;
    const end = Math.min(start + CHUNK_SIZE, file.size);
    const chunk = file.slice(start, end);

    await uploadChunk({
      file: chunk,
      chunkIndex: i,
      totalChunks: chunks,
      fileHash: fileHash
    });
  }

  // 通知合并
  await mergeChunks({ fileHash, fileName: file.name });
};
```

```csharp
// 后端：接收分片
[HttpPost("upload/chunk")]
public async Task<IActionResult> UploadChunk([FromForm] ChunkUploadRequest request)
{
    var chunkPath = Path.Combine(_tempPath, request.FileHash, $"{request.ChunkIndex}.tmp");

    Directory.CreateDirectory(Path.GetDirectoryName(chunkPath));

    using var stream = new FileStream(chunkPath, FileMode.Create);
    await request.File.CopyToAsync(stream);

    return Ok();
}

// 后端：合并分片
[HttpPost("upload/merge")]
public async Task<IActionResult> MergeChunks([FromBody] MergeRequest request)
{
    var chunkDir = Path.Combine(_tempPath, request.FileHash);
    var chunks = Directory.GetFiles(chunkDir).OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)));

    var finalPath = Path.Combine(_uploadPath, $"{Guid.NewGuid()}{Path.GetExtension(request.FileName)}");

    using var finalStream = new FileStream(finalPath, FileMode.Create);
    foreach (var chunkPath in chunks)
    {
        using var chunkStream = new FileStream(chunkPath, FileMode.Open);
        await chunkStream.CopyToAsync(finalStream);
    }

    // 删除临时文件
    Directory.Delete(chunkDir, true);

    return Ok(new { url = GetFileUrl(finalPath) });
}
```

---

## 🔒 安全性设计

### 1. 文件类型校验

#### 白名单机制（推荐）
```csharp
private static readonly Dictionary<string, string[]> AllowedTypes = new()
{
    ["image"] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" },
    ["document"] = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt" },
    ["video"] = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv" },
    ["audio"] = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac" }
};

// 禁止的扩展名（黑名单）
private static readonly string[] ForbiddenExtensions = new[]
{
    ".exe", ".bat", ".cmd", ".sh", ".dll", ".so", ".dylib",
    ".js", ".vbs", ".ps1", ".php", ".asp", ".jsp", ".html"
};
```

#### Magic Number 检查（文件头校验）
```csharp
private static readonly Dictionary<string, byte[]> FileSignatures = new()
{
    [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
    [".png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
    [".gif"] = new byte[] { 0x47, 0x49, 0x46, 0x38 },
    [".pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },
    [".zip"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
};

private bool ValidateFileSignature(Stream stream, string extension)
{
    if (!FileSignatures.TryGetValue(extension.ToLower(), out var expectedSignature))
        return false;

    var buffer = new byte[expectedSignature.Length];
    stream.Read(buffer, 0, buffer.Length);
    stream.Position = 0; // 重置流位置

    return buffer.SequenceEqual(expectedSignature);
}
```

### 2. 文件大小限制

```csharp
public class FileSizeLimits
{
    public const long Avatar = 2 * 1024 * 1024;        // 2MB
    public const long Image = 5 * 1024 * 1024;         // 5MB
    public const long Document = 10 * 1024 * 1024;     // 10MB
    public const long Video = 100 * 1024 * 1024;       // 100MB
}

// 配置请求大小限制
[RequestSizeLimit(FileSizeLimits.Image)]
[RequestFormLimits(MultipartBodyLengthLimit = FileSizeLimits.Image)]
public async Task<IActionResult> Upload(IFormFile file) { }
```

### 3. 文件名处理

```csharp
public class FileNameGenerator
{
    // 使用雪花ID生成唯一文件名
    public static string GenerateUniqueFileName(string originalName)
    {
        var extension = Path.GetExtension(originalName);
        var uniqueName = $"{SnowFlakeSingle.Instance.NextId()}{extension}";
        return uniqueName;
    }

    // 生成带日期的路径
    public static string GenerateFilePath(string category, string fileName)
    {
        var now = DateTime.Now;
        return Path.Combine(category, now.Year.ToString(), now.Month.ToString("D2"), fileName);
    }

    // 清理文件名（移除特殊字符）
    public static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars));
        return sanitized;
    }
}
```

### 4. 访问权限控制

#### 公开文件
```csharp
// 任何人可访问
public string GetPublicUrl(string filePath)
{
    return $"{_baseUrl}/{filePath}";
}
```

#### 私有文件（需要鉴权）
```csharp
[Authorize]
[HttpGet("download/{id}")]
public async Task<IActionResult> Download(long id)
{
    var attachment = await _attachmentService.QueryByIdAsync(id);
    if (attachment == null)
        return NotFound();

    // 权限检查
    if (!attachment.IsPublic && attachment.UploaderId != _currentUser.UserId)
        return Forbid();

    var stream = await _fileStorage.DownloadAsync(attachment.StoragePath);
    return File(stream, attachment.MimeType, attachment.OriginalName);
}
```

#### 临时授权 URL（带签名）
```csharp
public string GetTemporaryUrl(string filePath, int expirationMinutes = 60)
{
    var expiration = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds();
    var signature = GenerateSignature(filePath, expiration);

    return $"{_baseUrl}/{filePath}?expires={expiration}&signature={signature}";
}

private string GenerateSignature(string filePath, long expiration)
{
    var data = $"{filePath}|{expiration}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    return Convert.ToBase64String(hash);
}
```

### 5. 恶意文件防护

#### 图片安全处理
```csharp
// 去除 EXIF 信息（可能含有恶意代码或隐私信息）
public async Task<Stream> SanitizeImage(Stream inputStream)
{
    using var image = await Image.LoadAsync(inputStream);

    // 移除 EXIF 数据
    image.Metadata.ExifProfile = null;

    // 重新编码
    var outputStream = new MemoryStream();
    await image.SaveAsJpegAsync(outputStream);
    outputStream.Position = 0;

    return outputStream;
}
```

#### 病毒扫描（可选）
```csharp
// 集成 ClamAV 病毒扫描
public async Task<bool> ScanForVirus(Stream stream)
{
    // 使用 ClamAV 或其他反病毒引擎扫描
    var clam = new ClamClient("localhost", 3310);
    var result = await clam.SendAndScanFileAsync(stream);

    return result.Result == ClamScanResults.Clean;
}
```

#### 内容审核（云服务）
```csharp
// 调用阿里云内容安全 API
public async Task<ContentAuditResult> AuditContent(string imageUrl)
{
    var client = new ContentAuditClient(_config);
    var result = await client.ScanImageAsync(imageUrl);

    return new ContentAuditResult
    {
        IsSafe = result.Suggestion == "pass",
        Labels = result.Labels, // 违规标签：porn, terrorism, ad等
        Score = result.Score
    };
}
```

---

## ⚡ 性能优化

### 1. 图片处理策略

#### 上传时自动处理
```csharp
public async Task<ImageUploadResult> UploadImage(Stream stream, string fileName)
{
    var result = new ImageUploadResult();

    // 加载图片
    using var image = await Image.LoadAsync(stream);

    // 1. 生成多种尺寸
    result.Thumbnail = await GenerateThumbnail(image, 150, 150);   // 缩略图
    result.Small = await ResizeImage(image, 400, 300);             // 小图
    result.Medium = await ResizeImage(image, 800, 600);            // 中图
    result.Large = await ResizeImage(image, 1200, 900);            // 大图

    // 2. 压缩原图（质量 85%）
    result.Original = await CompressImage(image, 85);

    // 3. 可选：添加水印
    if (_options.EnableWatermark)
    {
        result.Original = await AddWatermark(result.Original);
    }

    return result;
}

private async Task<string> ResizeImage(Image image, int maxWidth, int maxHeight)
{
    var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
    {
        Mode = ResizeMode.Max,
        Size = new Size(maxWidth, maxHeight)
    }));

    var fileName = $"{Guid.NewGuid()}.jpg";
    var filePath = Path.Combine(_uploadPath, "resized", fileName);

    await clone.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 85 });

    return filePath;
}
```

#### 图片格式转换
```csharp
// 自动转换为 WebP 格式（更小的文件体积）
public async Task<string> ConvertToWebP(Stream inputStream)
{
    using var image = await Image.LoadAsync(inputStream);

    var outputPath = Path.Combine(_uploadPath, $"{Guid.NewGuid()}.webp");
    await image.SaveAsWebpAsync(outputPath, new WebpEncoder { Quality = 85 });

    return outputPath;
}
```

### 2. CDN 加速配置

#### OSS + CDN 配置
```json
{
  "CDN": {
    "Enable": true,
    "Domain": "https://cdn.example.com",
    "CacheControl": "public, max-age=31536000",  // 1年
    "ImageProcess": {
      "Thumbnail": "?x-oss-process=image/resize,m_fill,w_150,h_150/quality,q_85",
      "Small": "?x-oss-process=image/resize,m_lfit,w_400/quality,q_85",
      "Watermark": "?x-oss-process=image/watermark,text_UmFkaXNo,color_FFFFFF,size_20,g_se"
    }
  }
}
```

#### 使用示例
```csharp
public string GetCdnUrl(string filePath, ImageSize size = ImageSize.Original)
{
    var baseUrl = _cdnConfig.Enable ? _cdnConfig.Domain : _baseUrl;
    var url = $"{baseUrl}/{filePath}";

    // 添加图片处理参数
    if (size != ImageSize.Original && _cdnConfig.ImageProcess.ContainsKey(size.ToString()))
    {
        url += _cdnConfig.ImageProcess[size.ToString()];
    }

    return url;
}
```

### 3. 懒加载和渐进式加载

#### 前端实现
```typescript
// 1. 列表页：加载缩略图，懒加载
<img
  src={getThumbnailUrl(image.url)}
  loading="lazy"
  data-original={getOriginalUrl(image.url)}
  onClick={handleImageClick}
/>

// 2. 点击查看大图
const handleImageClick = (e: React.MouseEvent<HTMLImageElement>) => {
  const img = e.currentTarget;
  const originalUrl = img.dataset.original;

  // 显示 Modal，加载原图
  setLightboxImage(originalUrl);
};

// 3. 渐进式加载（先模糊后清晰）
const ProgressiveImage = ({ src, placeholder }: Props) => {
  const [loaded, setLoaded] = useState(false);

  return (
    <div className={styles.imageContainer}>
      <img src={placeholder} className={styles.placeholder} />
      <img
        src={src}
        onLoad={() => setLoaded(true)}
        className={loaded ? styles.loaded : styles.loading}
      />
    </div>
  );
};
```

### 4. 上传进度和优化

#### 显示上传进度
```typescript
const [uploadProgress, setUploadProgress] = useState(0);

const uploadFile = async (file: File) => {
  const formData = new FormData();
  formData.append('file', file);

  await axios.post('/api/v1/Upload', formData, {
    onUploadProgress: (progressEvent) => {
      const percentCompleted = Math.round(
        (progressEvent.loaded * 100) / progressEvent.total!
      );
      setUploadProgress(percentCompleted);
    }
  });
};

// UI 组件
<div className={styles.uploadProgress}>
  <div className={styles.progressBar} style={{ width: `${uploadProgress}%` }} />
  <span>{uploadProgress}%</span>
</div>
```

#### 压缩后上传
```typescript
// 使用 browser-image-compression 库
import imageCompression from 'browser-image-compression';

const compressAndUpload = async (file: File) => {
  // 压缩配置
  const options = {
    maxSizeMB: 1,              // 最大 1MB
    maxWidthOrHeight: 1920,    // 最大宽高
    useWebWorker: true         // 使用 Web Worker
  };

  // 压缩图片
  const compressedFile = await imageCompression(file, options);

  // 上传压缩后的文件
  await uploadFile(compressedFile);
};
```

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

## 🤔 待讨论的问题

### 1. 存储方案选择
- [ ] **问题**：是现在就用 MinIO，还是先用本地存储？
- **方案 A**：本地存储（快速开发，后续迁移）
- **方案 B**：直接用 MinIO（Docker 部署，S3 兼容）
- **建议**：方案 A（先快速实现，接口设计好便于迁移）

### 2. 图片处理
- [ ] **问题**：图片处理在上传时还是访问时？
- **方案 A**：上传时处理（生成多尺寸，占用存储）
- **方案 B**：访问时处理（按需处理，节省存储）
- **建议**：方案 A（性能好，用户体验佳）

### 3. 是否需要水印
- [ ] **问题**：帖子图片是否添加水印？
- **考虑**：防止盗图 vs 用户体验
- **建议**：可选配置，默认关闭

### 4. 文件删除策略
- [ ] **问题**：删除帖子/评论时，是否删除图片？
- **方案 A**：软删除（标记为删除，定期清理）
- **方案 B**：硬删除（立即删除文件）
- **建议**：方案 A（可恢复，防止误删）

### 5. 图片审核
- [ ] **问题**：是否需要内容审核？
- **考虑**：安全合规 vs 成本
- **建议**：MVP 阶段不做，生产环境可选

### 6. 文件大小限制
- [ ] **问题**：各类型文件的大小限制？
- **当前建议**：
  - 头像：2MB
  - 帖子图片：5MB
  - 文档：10MB
- **需要确认**：是否合理？

### 7. 支持的文件类型
- [ ] **问题**：除了图片，是否支持视频、文档？
- **MVP 阶段**：只支持图片
- **后续扩展**：PDF、Office 文档、视频
- **建议**：先图片，后续按需扩展

### 8. 上传并发限制
- [ ] **问题**：单个用户同时上传文件数限制？
- **建议**：单用户最多 5 个并发上传

---

## 📝 下一步行动

### 1. 确认方案
- [ ] 讨论并确认上述"待讨论的问题"
- [ ] 明确 MVP 阶段的功能范围

### 2. 技术准备
- [ ] 安装图片处理库（如 SixLabors.ImageSharp）
- [ ] 准备测试图片素材

### 3. 开发计划
1. **后端**：
   - [ ] 创建 `Attachment` 实体和表
   - [ ] 实现 `IFileStorage` 接口
   - [ ] 实现 `LocalFileStorage` 本地存储
   - [ ] 创建 `AttachmentService`
   - [ ] 实现上传 API
   - [ ] 添加文件校验和安全检查

2. **前端**：
   - [ ] 创建文件上传组件
   - [ ] 集成到 MarkdownEditor（图片按钮）
   - [ ] 显示上传进度
   - [ ] 图片预览功能

3. **测试**：
   - [ ] 单元测试
   - [ ] 集成测试
   - [ ] 压力测试

4. **文档**：
   - [ ] API 文档
   - [ ] 使用说明

---

## 📚 参考资料

### 技术文档
- [ASP.NET Core 文件上传](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads)
- [SixLabors.ImageSharp 文档](https://docs.sixlabors.com/articles/imagesharp/index.html)
- [阿里云 OSS 文档](https://help.aliyun.com/product/31815.html)
- [MinIO 文档](https://min.io/docs/minio/linux/index.html)

### 开源项目参考
- [Ant Design ProComponents - Upload](https://procomponents.ant.design/components/upload)
- [Uppy - 文件上传库](https://uppy.io/)
- [FilePond - 优雅的文件上传](https://pqina.nl/filepond/)

---

**文档状态**：草稿待讨论
**最后更新**：2025-12-20
**责任人**：开发团队
