# 测试文件说明

本目录包含用于附件上传 API 测试的示例文件。

## 已创建的文件

| 文件名 | 类型 | 大小 | 用途 |
|--------|------|------|------|
| `test-image.jpg` | JPG 图片 | 70B | 基础图片上传测试 |
| `test-image.png` | PNG 图片 | 70B | PNG 格式测试 |
| `avatar.jpg` | JPG 图片 | 70B | 头像上传测试 |
| `test-document.md` | Markdown | 325B | Markdown 文档测试 |

## 添加更多测试文件

如果需要更真实的测试场景，建议添加以下文件：

### 图片文件
```bash
# 从网络下载测试图片
wget https://via.placeholder.com/800x600.jpg -O test-image-large.jpg
wget https://via.placeholder.com/400x400.png -O test-image-medium.png

# 或者使用 ImageMagick 生成
convert -size 800x600 gradient:blue-white test-image-large.jpg
convert -size 400x400 gradient:red-yellow test-image-medium.png
```

### 文档文件
```bash
# 创建测试 PDF（需要 pandoc）
echo "# Test PDF\n\nThis is a test document." | pandoc -o test-document.pdf

# 或者从现有文档复制
cp /path/to/your/document.pdf test-document.pdf
cp /path/to/your/spreadsheet.xlsx test-spreadsheet.xlsx
```

### 大文件测试
```bash
# 创建超过 100MB 的文件用于测试大小限制
dd if=/dev/zero of=large-file.jpg bs=1M count=101
```

## 注意事项

1. **当前文件**：现有的测试文件都是最小化的示例（1x1 像素图片），适合快速功能测试
2. **真实测试**：如需测试图片处理（缩略图、EXIF 移除）功能，建议使用真实的照片
3. **文件大小**：默认限制为 100MB，可在 `appsettings.json` 中修改 `FileStorage.MaxFileSize`
4. **支持格式**：
   - 图片：.jpg, .jpeg, .png, .gif, .bmp, .webp, .svg
   - 文档：.pdf, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .txt, .md

## 快速测试

使用现有文件即可进行基础功能测试：

1. 打开 `Radish.Api.Attachment.http`
2. 确保 API 服务已启动
3. 获取有效的 Access Token
4. 运行上传请求（会自动使用本目录中的文件）

参考：`../AttachmentApiTestGuide.md`
