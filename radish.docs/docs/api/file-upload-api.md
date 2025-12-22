# 文件上传 API 文档

> **状态**：✅ 已实现
> **最后更新**：2025-12-22
> **维护者**：Radish Team

## 📋 概述

Radish 文件上传 API 提供了图片与文档的上传、查询、下载与删除能力，支持图片处理（缩略图、多尺寸、水印、WebP）、文件去重、软删除与定时清理。

**核心特性**：
- ✅ 支持图片与文档上传
- ✅ 文件去重（SHA256）
- ✅ 图片处理（缩略图、多尺寸、水印、WebP）
- ✅ 软删除与定时清理（Hangfire）
- ✅ 统一的返回结构（MessageModel）

---

## 🔐 认证方式

所有上传与下载接口均需携带 Bearer Token：

```
Authorization: Bearer {access_token}
```

---

## 📦 接口列表

### 1. 上传图片

**POST** `/api/v1/Attachment/UploadImage`

#### 请求参数（multipart/form-data）

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `file` | File | ✅ | 上传文件 |
| `businessType` | string | ✅ | 业务类型（Post/Comment/Avatar/Document） |
| `generateThumbnail` | bool | ❌ | 是否生成缩略图（默认 true） |
| `removeExif` | bool | ❌ | 是否移除 EXIF（默认 true） |
| `generateSizes` | bool | ❌ | 是否生成多尺寸图（默认 false） |
| `watermark` | bool | ❌ | 是否添加水印（默认 false） |
| `webp` | bool | ❌ | 是否生成 WebP（默认 false） |

#### 请求示例

```http
POST /api/v1/Attachment/UploadImage
Authorization: Bearer {access_token}
Content-Type: multipart/form-data

file: (binary)
businessType: Post
generateThumbnail: true
removeExif: true
generateSizes: true
watermark: false
webp: true
```

#### 成功响应

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
    "smallUrl": "/uploads/Post/2025/12/2002696344824709120_small.jpg",
    "mediumUrl": "/uploads/Post/2025/12/2002696344824709120_medium.jpg",
    "webpUrl": "/uploads/Post/2025/12/2002696344824709120.webp",
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

### 2. 上传文档

**POST** `/api/v1/Attachment/UploadDocument`

#### 请求参数（multipart/form-data）

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `file` | File | ✅ | 上传文件 |
| `businessType` | string | ✅ | 业务类型（Post/Comment/Avatar/Document） |

#### 请求示例

```http
POST /api/v1/Attachment/UploadDocument
Authorization: Bearer {access_token}
Content-Type: multipart/form-data

file: (binary)
businessType: Document
```

#### 成功响应

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "上传成功",
  "responseData": {
    "id": 2002696346624065537,
    "fileName": "2002696344824709121.pdf",
    "originalFileName": "contract.pdf",
    "fileSize": 186420,
    "fileSizeFormatted": "182.04 KB",
    "mimeType": "application/pdf",
    "storageType": "Local",
    "url": "/uploads/Document/2025/12/2002696344824709121.pdf",
    "uploaderId": 20000,
    "uploaderName": "system",
    "businessType": "Document",
    "isPublic": false,
    "downloadCount": 0,
    "createTime": "2025-12-21T18:52:09"
  }
}
```

---

### 3. 获取附件详情

**GET** `/api/v1/Attachment/GetById/{id}`

#### 请求示例

```http
GET /api/v1/Attachment/GetById/2002696346624065536
Authorization: Bearer {access_token}
```

#### 成功响应

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": {
    "id": 2002696346624065536,
    "originalFileName": "my-image.jpg",
    "fileSize": 47295,
    "mimeType": "image/jpeg",
    "url": "/uploads/Post/2025/12/2002696344824709120.jpg",
    "thumbnailUrl": "/uploads/Post/2025/12/2002696344824709120_thumb.jpg",
    "uploaderName": "system",
    "createTime": "2025-12-21T18:52:09"
  }
}
```

---

### 4. 按业务类型查询附件

**GET** `/api/v1/Attachment/GetByBusiness`

#### 查询参数

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `businessType` | string | ✅ | 业务类型 |
| `businessId` | long | ❌ | 业务 ID |

#### 请求示例

```http
GET /api/v1/Attachment/GetByBusiness?businessType=Post&businessId=1001
Authorization: Bearer {access_token}
```

---

### 5. 下载附件

**GET** `/api/v1/Attachment/Download/{id}`

#### 请求示例

```http
GET /api/v1/Attachment/Download/2002696346624065536
Authorization: Bearer {access_token}
```

---

### 6. 软删除附件

**DELETE** `/api/v1/Attachment/Delete/{id}`

#### 请求示例

```http
DELETE /api/v1/Attachment/Delete/2002696346624065536
Authorization: Bearer {access_token}
```

---

### 7. 批量删除

**POST** `/api/v1/Attachment/DeleteBatch`

#### 请求体

```json
{
  "ids": [2002696346624065536, 2002696346624065537]
}
```

---

### 8. 更新业务关联

**PUT** `/api/v1/Attachment/UpdateBusinessAssociation/{id}`

#### 请求体

```json
{
  "businessType": "Post",
  "businessId": 1001
}
```

---

## ⚠️ 错误码说明

| HTTP 状态码 | 说明 |
|-------------|------|
| 400 | 请求参数错误 |
| 401 | 未认证 |
| 403 | 权限不足 |
| 413 | 文件过大 |
| 415 | 文件类型不支持 |
| 507 | 存储空间不足 |
| 503 | 存储服务不可用 |

---

## 🗂️ 文件存储路径

### 本地存储路径

```
DataBases/Uploads/
├── Images/
│   └── 2025/12/
│       ├── original/
│       ├── thumb/
│       ├── small/
│       └── medium/
├── Documents/
│   └── 2025/12/
└── Temp/
```

### 访问 URL

- 原图：`/uploads/{BusinessType}/YYYY/MM/{FileName}`
- 缩略图：`/uploads/{BusinessType}/YYYY/MM/{FileName}_thumb.jpg`
- 小图：`/uploads/{BusinessType}/YYYY/MM/{FileName}_small.jpg`
- 中图：`/uploads/{BusinessType}/YYYY/MM/{FileName}_medium.jpg`
- WebP：`/uploads/{BusinessType}/YYYY/MM/{FileName}.webp`

---

## 📋 相关配置

### FileStorage 配置

详见：`/guide/configuration`

关键配置项：
- `FileStorage:MaxFileSize`
- `FileStorage:AllowedExtensions`
- `FileStorage:ImageProcessing`
- `FileStorage:Watermark`
- `FileStorage:Deduplication`

### Hangfire 配置

详见：`/guide/hangfire-scheduled-jobs`

---

## 📚 相关文档

- [文件上传功能设计](../features/file-upload-design.md)
- [配置管理指南](../guide/configuration.md)
- [Hangfire 定时任务指南](../guide/hangfire-scheduled-jobs.md)

---

**文档状态**：已完成
**最后更新**：2025-12-22
**版本**：v1.0