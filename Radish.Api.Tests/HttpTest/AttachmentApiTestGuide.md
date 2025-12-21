# 附件上传 API 测试指南

## 快速开始

### 1. 准备测试文件

在 `test-files/` 目录下准备以下测试文件：

```bash
cd /mnt/d/Code/Radish/Radish.Api/HttpTest/test-files

# 方法 1: 从其他位置复制现有文件
cp /path/to/your/image.jpg test-image.jpg
cp /path/to/your/document.pdf test-document.pdf

# 方法 2: 使用 ImageMagick 生成测试图片
convert -size 800x600 xc:blue test-image.jpg
convert -size 400x400 xc:red test-image.png
convert -size 200x200 xc:green avatar.jpg

# 方法 3: 从网络下载测试图片
wget https://via.placeholder.com/800x600.jpg -O test-image.jpg
wget https://via.placeholder.com/400x400.png -O test-image.png
wget https://via.placeholder.com/200x200.jpg -O avatar.jpg
```

### 2. 所需测试文件列表

| 文件名 | 类型 | 用途 | 大小建议 |
|--------|------|------|---------|
| `test-image.jpg` | JPG 图片 | 基础图片上传测试 | < 10MB |
| `test-image.png` | PNG 图片 | PNG 格式测试 | < 5MB |
| `avatar.jpg` | JPG 图片 | 头像上传测试 | < 2MB |
| `test-document.pdf` | PDF 文档 | PDF 文档上传测试 | < 10MB |
| `test-document.md` | Markdown | Markdown 文档测试 | < 1MB |
| `test-spreadsheet.xlsx` | Excel | Excel 文档测试 | < 5MB |
| `large-file.jpg` | JPG 图片 | 大文件测试（超过100MB） | > 100MB |

### 3. 快速生成测试文件

如果你没有现成的文件，可以使用以下命令快速生成：

**Linux/macOS (使用 ImageMagick):**
```bash
# 安装 ImageMagick
# Ubuntu/Debian: sudo apt install imagemagick
# macOS: brew install imagemagick

# 生成测试图片
convert -size 800x600 gradient:blue-white test-image.jpg
convert -size 400x400 gradient:red-yellow test-image.png
convert -size 200x200 gradient:green-cyan avatar.jpg
```

**Windows (使用 PowerShell):**
```powershell
# 生成简单的文本文件作为测试
"Test Document Content" | Out-File -FilePath test-document.md
"# Markdown Test`n`nThis is a test markdown file." | Out-File -FilePath test-document.md
```

### 4. 获取 Access Token

运行 API 服务，然后从 `Radish.Api.AuthFlow.http` 获取有效的 Token：

```bash
# 启动 API
cd /mnt/d/Code/Radish
dotnet run --project Radish.Api
```

在 VS Code 中打开 `Radish.Api.AuthFlow.http`，执行登录请求，获取 `access_token`。

### 5. 更新 Token

复制获取的 Token，更新 `Radish.Api.Attachment.http` 文件中的变量：

```http
@Access_Token = <your_token_here>
```

### 6. 运行测试

在 VS Code 中：
1. 安装 **REST Client** 扩展
2. 打开 `Radish.Api.Attachment.http`
3. 点击每个请求上方的 **"Send Request"** 链接

或使用 **HTTP Client** 插件（JetBrains IDEs）：
1. 打开 `Radish.Api.Attachment.http`
2. 点击左侧的绿色运行按钮

## 测试用例说明

### 图片上传测试

| 测试用例 | 说明 | 预期结果 |
|---------|------|---------|
| 1.1 基础上传 | 上传 JPG 图片，生成缩略图 | 200 OK，返回附件信息 |
| 1.2 指定业务类型 | 上传到 Post 业务类型 | 200 OK，businessType=Post |
| 1.3 头像上传 | 上传头像（Avatar） | 200 OK，businessType=Avatar |
| 1.4 不生成缩略图 | generateThumbnail=false | 200 OK，无缩略图路径 |
| 1.5 去重测试 | 上传相同文件 | 返回已存在的附件记录 |

### 文档上传测试

| 测试用例 | 说明 | 预期结果 |
|---------|------|---------|
| 2.1 PDF 上传 | 上传 PDF 文档 | 200 OK |
| 2.2 Markdown 上传 | 上传 Markdown 文件 | 200 OK |
| 2.3 Excel 上传 | 上传 Excel 文件 | 200 OK |

### 附件查询测试

| 测试用例 | 说明 | 预期结果 |
|---------|------|---------|
| 3.1 根据 ID 查询 | 获取指定附件详情 | 200 OK，返回附件详情 |
| 3.2 根据业务查询 | 获取业务对象的附件列表 | 200 OK，返回列表 |

### 附件下载测试

| 测试用例 | 说明 | 预期结果 |
|---------|------|---------|
| 4.1 下载附件 | 通过 API 下载 | 返回文件流 |
| 4.2 静态访问 | 通过静态路径访问 | 返回图片 |
| 4.3 访问缩略图 | 访问 _thumb 文件 | 返回缩略图 |

### 错误场景测试

| 测试用例 | 说明 | 预期结果 |
|---------|------|---------|
| 7.1 空文件 | 不上传文件 | 400 Bad Request |
| 7.2 不支持类型 | 上传 .exe 文件 | 400 Bad Request |
| 7.3 未认证 | 不带 Token | 401 Unauthorized |
| 7.4 删除不存在 | 删除不存在的附件 | 404 Not Found |
| 7.6 无权删除 | 普通用户删除别人的文件 | 403 Forbidden |
| 8.1 超大文件 | 上传超过 100MB 文件 | 400 Bad Request |

## 验证要点

### 1. 文件存储验证

检查文件是否正确保存：

```bash
# 查看 uploads 目录结构
tree /mnt/d/Code/Radish/Radish.Api/uploads

# 示例输出：
# uploads/
# ├── General/
# │   └── 2025/
# │       └── 12/
# │           ├── 1234567890.jpg
# │           └── 1234567890_thumb.jpg
# ├── Post/
# │   └── 2025/
# │       └── 12/
# │           ├── 1234567891.png
# │           └── 1234567891_thumb.png
# └── Avatar/
#     └── 2025/
#         └── 12/
#             ├── 1234567892.jpg
#             └── 1234567892_thumb.jpg
```

### 2. 数据库验证

检查附件记录是否正确保存：

```bash
# 使用 SQLite 查看数据库
sqlite3 /mnt/d/Code/Radish/DataBases/Radish.db

# 查询附件表
SELECT Id, OriginalName, StoredName, FileSize, BusinessType, UploaderId, Url
FROM Attachment
ORDER BY CreateTime DESC
LIMIT 10;

# 查看文件去重效果
SELECT FileHash, COUNT(*) as Count
FROM Attachment
WHERE FileHash IS NOT NULL
GROUP BY FileHash
HAVING Count > 1;
```

### 3. 缩略图验证

检查缩略图是否正确生成：

```bash
# 查找所有缩略图文件
find /mnt/d/Code/Radish/Radish.Api/uploads -name "*_thumb.*"

# 检查缩略图尺寸（需要 ImageMagick）
identify /mnt/d/Code/Radish/Radish.Api/uploads/General/2025/12/*_thumb.jpg
# 输出应该显示 150x150 或类似尺寸
```

### 4. 权限验证

测试权限控制：

1. 用 System/Admin 用户 Token 上传文件
2. 切换到普通用户 Token
3. 尝试删除 System 用户上传的文件
4. 应返回 403 Forbidden

### 5. 去重验证

测试文件去重功能：

1. 上传一个图片（记录返回的 ID 和 URL）
2. 再次上传相同的图片
3. 检查返回的 ID 和 URL 是否与第一次相同
4. 检查 uploads 目录，不应有重复文件

## 常见问题

### Q1: 测试文件路径问题

**问题**: `< ./test-files/test-image.jpg` 找不到文件

**解决**:
- 确保 `test-files` 目录存在且包含测试文件
- 使用绝对路径：`< /mnt/d/Code/Radish/Radish.Api/HttpTest/test-files/test-image.jpg`

### Q2: Token 过期

**问题**: 返回 401 Unauthorized

**解决**:
- 重新从 `Radish.Api.AuthFlow.http` 获取新的 Token
- 更新 `@Access_Token` 变量

### Q3: 文件类型不支持

**问题**: 返回 "仅支持图片格式" 或 "仅支持文档格式"

**解决**:
- 检查文件扩展名是否在支持列表中
- 查看 `appsettings.json` 中的 `FileStorage` 配置
- 图片：.jpg, .jpeg, .png, .gif, .bmp, .webp, .svg
- 文档：.pdf, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .txt, .md

### Q4: 上传超时

**问题**: 大文件上传超时

**解决**:
- 检查 ASP.NET Core 的请求大小限制
- 增加 Kestrel 的最大请求体大小配置

### Q5: 静态文件访问 404

**问题**: 通过 `/uploads/...` 路径无法访问文件

**解决**:
- 检查 `Program.cs` 中是否配置了静态文件中间件
- 确认 `uploads` 目录存在且文件路径正确

## 下一步

完成 HTTP 测试后，可以继续：

1. **前端集成**：创建 FileUpload 组件（@radish/ui）
2. **Markdown 编辑器集成**：在 MarkdownEditor 中添加图片上传功能
3. **单元测试**：编写 AttachmentService 和 Controller 的单元测试
4. **文档更新**：更新 CLAUDE.md 中的架构说明

## 相关文档

- `Radish.Api/Controllers/AttachmentController.cs` - API 控制器实现
- `Radish.Service/AttachmentService.cs` - 业务逻辑实现
- `Radish.Infrastructure/FileStorage/LocalFileStorage.cs` - 本地存储实现
- `Radish.Infrastructure/ImageProcessing/CSharpImageProcessor.cs` - 图片处理实现
- `Radish.Api/appsettings.json` - 文件存储配置

---

**最后更新**: 2025-12-21
**维护者**: Radish Team
