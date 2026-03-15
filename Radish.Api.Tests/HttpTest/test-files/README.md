# 测试文件说明

本目录存放附件相关 HTTP 脚本和自动化脚本需要的测试素材。

## 已包含的最小素材

| 文件名 | 用途 |
|--------|------|
| `test-image.jpg` | 基础图片上传 |
| `test-image.png` | PNG / 格式校验 |
| `avatar.jpg` | 头像上传 |
| `fake-image.jpg` | 伪装图片校验 |
| `fake-document.pdf` | 伪装文档校验 |
| `test-document.md` | Markdown 文档上传 |

## 常见补充素材

根据你要回归的脚本，可自行补充这些文件：

| 文件名 | 对应脚本 |
|--------|----------|
| `test-document.pdf` | `Radish.Api.Attachment.Upload.http` |
| `test-spreadsheet.xlsx` | `Radish.Api.Attachment.Upload.http` |
| `test-image.webp` | `Radish.Api.Attachment.Upload.http` |
| `large-file.jpg` | `Radish.Api.Attachment.Guardrail.http` |
| `large-test-image.jpg` | `Radish.Api.Attachment.Guardrail.http` |
| `chunk_0.bin` / `chunk_1.bin` | `Radish.Api.Attachment.Chunk.http` |
| `large_chunk_11mb.bin` | `Radish.Api.Attachment.Chunk.http` |
| `test.exe` | `Radish.Api.Attachment.Guardrail.http` |

## 使用提醒

- 现有图片大多是最小化占位文件，适合快速验证接口链路，不适合验证真实缩略图、水印、EXIF 处理效果。
- 需要认证的请求，先从上级目录的 `Radish.Api.AuthFlow.http` 获取 `access_token`。
- 目录入口与脚本分组见上级目录的 `README.md`。
