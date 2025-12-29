# 2025年12月 - 第4周

> 文件上传功能、图片处理

**时间范围**：2025-12-22 至 2025-12-28

---

## 2025-12-29 (周一)

### 论坛：神评/沙发定时统计与评论展示修复

- **feat(comment-highlight)**: 新增神评/沙发历史记录与定时统计
  - 新增 `CommentHighlight` 实体与相关 API（当前神评/沙发、历史记录、趋势、手动触发统计）
  - Hangfire 定时任务：每天凌晨 1 点统计（可配置 Cron）
  - 前端并行加载神评/沙发标识，默认排序置顶展示

- **fix(comment-highlight)**: 修复 SqlSugar Code First 索引解析异常
  - `CommentHighlight` 改为多个单字段索引组合，避免复合索引特性触发异常

- **fix(forum)**: 修复子评论在收起态不可见的问题
  - 无沙发统计数据时，收起态展示“最热一条回复”作为预览
  - 当后端仅返回 `childrenTotal` 时，自动预加载第一页子评论用于展示

---

## 2025-12-25 (周四)

### 工程修复与开发体验

- 修复 SqlSugar AOP SQL 日志输出的 `FormatException`
- 避免 Log 库写入触发 SQL AOP 的递归，解决日志刷屏问题
- 修复 `Client` 授权策略对空格分隔 `scope` 的校验，Scalar 调用受保护 API 不再 403
- 构建阶段自动将 `radish_lib` 原生库（`.dll/.so/.dylib`）复制到应用输出目录

---

## 2025-12-21 (周六)

### 图片上传与处理功能完整实现

#### 1. 文件上传功能增强

- **feat(attachment)**: 实现图片水印和多尺寸生成功能
  - **水印功能**：
    - 支持文字水印（可自定义文本、字体大小、透明度、位置、颜色）
    - 5 种水印位置（左上、右上、左下、右下、居中）
    - 基于 ImageSharp 实现，支持中文字体
  - **多尺寸生成**：
    - Small (400x300)、Medium (800x600)、Large (1200x900)
    - 保持宽高比，自动适配
    - 独立质量控制（默认 85%）
  - **EXIF 移除**：
    - 自动移除图片 EXIF 信息（保护隐私）
    - 可选配置
  - **WebP 支持**：
    - 支持 WebP 格式上传和处理
    - 自动格式识别
  - **文件去重**：
    - 基于 SHA256 哈希值去重
    - 特殊处理选项（水印/多尺寸）时跳过去重
    - 物理文件存在性检查
  - **提交**：
    - `27dfa5f fix: 添加水印和多尺寸参数到上传接口`
    - `cb90730 fix: 实现图片水印功能`
    - `bc93140 fix: 修复文件路径分隔符问题`
    - `897245b fix: 修复图片处理功能并添加 DataBases 到 gitignore`
    - `1a07b4d fix: 修复文件去重机制的物理文件检查`
    - `80c4623 fix: 修复编译错误`
    - `4d057d3 feat: 当用户指定特殊处理选项时跳过去重检查`

#### 2. 前端上传 UI 实现

- **feat(ui)**: FileUpload 组件支持水印和多尺寸选项
  - **水印选项**：
    - 复选框：是否添加水印
    - 文本输入：自定义水印文本（默认 "Radish"）
  - **多尺寸选项**：
    - 复选框：是否生成多尺寸图片
  - **集成到发帖表单**：
    - 在标题输入框下方显示上传选项
    - 用户可选择是否为上传的图片添加水印
    - 用户可选择是否生成多尺寸版本
  - **API 集成**：
    - `attachment.ts` 支持新参数传递
    - `uploadImage` 函数完整实现
    - 重试机制（3 次，指数退避）
  - **提交**：
    - `1cfcc89 feat: 在发帖表单中添加图片水印和多尺寸选项`

#### 3. 临时文件管理优化

- **feat(cleanup)**: 统一临时文件目录和回收站策略
  - **临时文件目录**：
    - 统一使用 `DataBases/Temp` 目录
    - 水印处理、EXIF 移除等操作的临时文件统一存放
    - 使用 GUID 命名避免冲突
  - **回收站策略**：
    - 统一回收站路径：`DataBases/Recycle`
    - 所有删除操作先移动到回收站（数据安全）
    - 按类型分类：deleted（软删除）、temp（临时文件）、orphan（孤立附件）
    - 按日期组织：`{category}/{YYYYMMDD}/{原始路径}`
    - 定时清理：回收站文件保留 90 天后永久删除
  - **定时任务更新**：
    - 软删除文件清理（保留 30 天）
    - 临时文件清理（保留 2 小时）
    - 回收站清理（保留 90 天）
    - 孤立附件清理（保留 24 小时）
  - **提交**：
    - `d0e641e feat: 统一临时文件目录并添加图片处理单元测试`
    - `8ba2a9d fix: 恢复回收站数据安全策略`

#### 4. 单元测试完善

- **test(image-processor)**: 添加图片处理单元测试
  - **测试覆盖**：
    - 文字水印添加（右下角）
    - 缩略图生成（150x150）
    - 多尺寸图片生成（Small/Medium/Large）
    - 水印位置测试（左上角、居中）
    - EXIF 信息移除
  - **测试工具**：
    - xUnit + Shouldly
    - ImageSharp（创建测试图片）
    - 自动生成渐变色测试图片
  - **测试输出**：
    - 测试图片输出到 `TestOutput/` 目录
    - 可视化验证水印效果
  - **提交**：
    - `d27e0f4 fix: 修复 ImageProcessorTest 编译错误`

#### 5. HTTP 测试用例

- **test(attachment)**: 添加图片处理 HTTP 测试用例
  - 测试用例 1.6：上传图片（生成多尺寸）
  - 测试用例 1.7：上传图片（添加文字水印）
  - 测试用例 1.8：上传 WebP 格式图片
  - 测试用例 1.9：上传图片（完整功能测试）
  - 位置：`Radish.Api.Tests/HttpTest/Radish.Api.Attachment.http`

#### 6. 配置和文档更新

- **chore(gitignore)**: 添加 DataBases 目录到 .gitignore
  - 排除数据库文件（*.db, *.db-shm, *.db-wal, *.db-journal）
  - 排除整个 DataBases 目录（包含上传文件和临时文件）
  - 保护敏感数据和用户上传内容

**技术亮点**：

1. **完整的图片处理流程**：
   - 上传 → 去重检查 → 水印处理 → 缩略图生成 → 多尺寸生成 → EXIF 移除 → 数据库保存
   - 每个步骤都有错误处理和日志记录

2. **数据安全设计**：
   - 所有删除操作先移动到回收站
   - 回收站文件按类型和日期组织
   - 定时任务自动清理过期文件
   - 物理文件存在性检查

3. **用户体验优化**：
   - 前端可选水印和多尺寸选项
   - 上传进度显示（XMLHttpRequest）
   - 重试机制（3 次，指数退避）
   - 文件去重（避免重复上传）

4. **代码质量**：
   - 完整的单元测试覆盖
   - HTTP 测试用例
   - 清晰的日志记录
   - 遵循分层架构

**文件变更统计**：
- 新增：`Radish.Api.Tests/ImageProcessorTest.cs` (270 行)
- 修改：`Radish.Service/AttachmentService.cs` (+100 行)
- 修改：`Radish.Service/Jobs/FileCleanupJob.cs` (+20 行)
- 修改：`Radish.Infrastructure/ImageProcessing/CSharpImageProcessor.cs` (+15 行)
- 修改：`Radish.Infrastructure/FileStorage/LocalFileStorage.cs` (+10 行)
- 修改：`radish.client/src/api/attachment.ts` (+15 行)
- 修改：`radish.client/src/apps/forum/components/PublishPostForm.tsx` (+50 行)
- 修改：`radish.client/src/apps/forum/components/PublishPostForm.module.css` (+50 行)
- 修改：`radish.ui/src/components/FileUpload/FileUpload.tsx` (+80 行)
- 修改：`radish.ui/src/components/FileUpload/FileUpload.module.css` (+60 行)
- 修改：`.gitignore` (+5 行)

**今日工作总结**：
- ✅ 实现完整的图片上传和处理功能
- ✅ 支持水印、多尺寸、EXIF 移除
- ✅ 前端 UI 集成（发帖表单）
- ✅ 统一临时文件和回收站管理
- ✅ 添加完整的单元测试
- ✅ 修复多个 bug（路径分隔符、去重机制、编译错误）
- 📊 代码提交：10 个提交
- 📝 功能增强：
  - 图片上传支持水印和多尺寸
  - 数据安全策略完善（回收站）
  - 测试覆盖率提升
- 🎯 用户体验改进：
  - 可选的图片处理选项
  - 上传进度显示
  - 文件去重避免重复上传

---

## 2025-12-22 (周日)

### Bug 修复与文档更新

#### 1. 修复水印功能文件锁问题

- **fix(watermark)**: 解决 Windows 环境下水印处理因文件占用失败
  - **问题原因**：水印和 EXIF 处理时，文件流未正确关闭导致文件锁冲突
  - **解决方案**：
    - 改用内存流处理图片（先读取到内存再处理）
    - 添加文件替换重试机制（最多 3 次，间隔 100ms）
    - 同步修复 EXIF 移除功能
  - **提交**：`517a701 fix: 修复上传水印因文件占用失败`

#### 2. 文档更新

- **docs**: 更新文件上传与 Hangfire 相关文档
  - 新增 `Hangfire 定时任务指南`（/guide/hangfire-scheduled-jobs.md）
  - 新增 `文件上传 API 文档`（/api/file-upload-api.md）
  - 更新配置管理指南（添加 FileStorage 和 Hangfire 配置）
  - 更新部署指南（文件上传目录挂载）
  - 更新文档导航索引
  - **提交**：`3521fce docs: 更新文件上传与Hangfire相关文档`

#### 3. 项目管理优化

- **chore**: 停止跟踪 DataBases 目录文件
  - 使用 `git rm -r --cached DataBases` 移除已跟踪的上传文件
  - 让 .gitignore 生效，避免后续提交包含上传文件
  - **提交**：`4ab9ad9 chore: 停止跟踪 DataBases 目录文件`

---

## 2025-12-23 (周二)

### Rust 扩展架构实现与迁移

#### 1. Rust 扩展架构实现

- **feat(rust)**: 实现统一的 Rust 扩展库架构
  - **项目结构**：
    - 创建 `Radish.Core/radish-lib/` 统一 Rust 扩展项目
    - 模块化设计：image（图片处理）、hash（文件哈希）、benchmark（性能测试）、utils（工具函数）
    - 使用 cdylib 编译为 C 兼容动态库
  - **图片水印功能**：
    - 基于 image + imageproc + rusttype 实现
    - 支持 5 种水印位置（左上、右上、左下、右下、居中）
    - 可自定义字体大小、透明度
    - FFI 导出：`add_text_watermark`
  - **文件哈希功能**：
    - 基于 sha2 实现 SHA256 哈希计算
    - 高性能文件读取和哈希计算
    - FFI 导出：`calculate_file_sha256`
  - **C# FFI 封装**：
    - `RustImageProcessor`：Rust 图片处理器封装
    - `ImageProcessorFactory`：支持配置切换 C# 或 Rust 实现
    - 自动回退机制：Rust 库不可用时自动使用 C# 实现
  - **编译脚本**：
    - `build.sh`（Linux/macOS）和 `build.ps1`（Windows）
    - 自动检测操作系统和 Rust 环境
    - 自动复制编译产物到 API 输出目录
  - **提交**：`6f266e1 feat: 实现 Rust 扩展架构`

#### 2. test_lib 迁移到 radish-lib

- **refactor(rust)**: 统一 Rust 扩展项目，避免维护多个项目
  - **迁移内容**：
    - 将 test_lib 的性能测试功能迁移到 `radish-lib/src/benchmark/math.rs`
    - 保留所有性能测试函数：
      - `calculate_sum_rust`：整数求和
      - `calculate_fibonacci_like_rust`：斐波那契计算
      - `count_primes_sieve_rust`：质数筛法
      - `count_primes_parallel_rust`：并行质数计算（使用 rayon）
    - 添加依赖：rayon（并行计算）、num_cpus（CPU 核心数检测）
  - **C# 代码更新**：
    - 更新 `RustTestController.cs` 中的 DllImport 引用
    - 将库名从 "test_lib" 改为 "radish_lib"
    - 修正错误消息中的库文件名（radish_lib.dll / libradish_lib.so / libradish_lib.dylib）
  - **目录清理**：
    - 删除旧的 `Radish.Core/test_lib/` 目录
    - 统一到 `Radish.Core/radish-lib/` 单一项目
  - **提交**：
    - `4959e28 refactor: 迁移 test_lib 到 radish-lib`
    - `72a0766 fix: 更新 RustTestController 使用 radish_lib`

#### 3. 文档更新

- **docs(rust)**: 更新 Rust 扩展相关文档
  - 更新使用指南（guide/rust-extensions.md）
  - 更新实施总结（features/rust-extension-implementation.md）
  - 更新项目说明（CLAUDE.md）
  - **提交**：`3086701 docs: rust扩展`

**技术亮点**：

1. **统一架构设计**：
   - 单一 Rust 项目管理所有原生扩展
   - 模块化设计，易于扩展新功能
   - 清晰的 FFI 边界和错误处理

2. **配置化切换**：
   - 通过 `appsettings.json` 配置选择实现
   - 自动回退机制保证可用性
   - 无需重新编译即可切换

3. **跨平台支持**：
   - Windows: radish_lib.dll
   - Linux: libradish_lib.so
   - macOS: libradish_lib.dylib
   - 编译脚本自动处理平台差异

4. **性能优化**：
   - Rust 实现提供更高性能
   - 并行计算支持（rayon）
   - Release 模式优化（LTO、strip）

**最终目录结构**：

```
Radish.Core/
└── radish-lib/                    # 统一的 Rust 扩展库
    ├── src/
    │   ├── lib.rs                 # FFI 导出
    │   ├── image/                 # 图片处理
    │   │   ├── mod.rs
    │   │   └── watermark.rs
    │   ├── hash/                  # 文件哈希
    │   │   ├── mod.rs
    │   │   └── file_hash.rs
    │   ├── benchmark/             # 性能测试（从 test_lib 迁移）
    │   │   ├── mod.rs
    │   │   └── math.rs
    │   └── utils/
    │       └── mod.rs
    ├── fonts/
    │   ├── DejaVuSans.ttf
    │   └── README.md
    ├── Cargo.toml
    ├── build.sh
    ├── build.ps1
    ├── .gitignore
    └── README.md
```

**文件变更统计**：
- 新增：`Radish.Core/radish-lib/` 目录（15 个文件，~1500 行）
- 新增：`Radish.Infrastructure/ImageProcessing/RustImageProcessor.cs` (~250 行)
- 新增：`Radish.Infrastructure/ImageProcessing/ImageProcessorFactory.cs` (~70 行)
- 修改：`Radish.Api/Program.cs` (+5 行)
- 修改：`Radish.Api/Controllers/v2/RustTestController.cs` (10 行替换)
- 删除：`Radish.Core/test_lib/` 目录（7 个文件）

**今日工作总结**：
- ✅ 实现完整的 Rust 扩展架构
- ✅ 迁移 test_lib 到统一项目
- ✅ 更新所有 C# 代码引用
- ✅ 完善文档和编译脚本
- 📊 代码提交：4 个提交
- 📝 功能增强：
  - 统一的 Rust 扩展管理
  - 配置化实现切换
  - 自动回退机制
- 🎯 架构改进：
  - 简化项目结构
  - 提高可维护性
  - 支持未来扩展

---

## 本周总结

### 完成功能

1. **文件上传系统**：
   - 图片水印（5种位置，可自定义）
   - 多尺寸生成（Small/Medium/Large）
   - EXIF 信息移除
   - WebP 格式支持
   - 文件去重机制

2. **Rust 扩展架构**：
   - 统一的 Rust 扩展库（radish-lib）
   - 图片水印 Rust 实现
   - 文件哈希 Rust 实现
   - 性能测试功能迁移
   - 配置化实现切换

3. **数据安全**：
   - 统一临时文件目录
   - 回收站机制
   - 定时清理任务

4. **测试完善**：
   - 6个单元测试
   - 4个 HTTP 测试

5. **个人中心功能**（2025-12-28 新增）：
   - 个人资料管理（查看/编辑）
   - 头像上传/更换/移除
   - 附件管理（列表/筛选/删除/复制链接）
   - 积分余额显示（占位）
   - 完整的输入校验（长度/格式/唯一性）
   - 附件下载权限控制

### 下周计划

- [x] 个人中心页面（已完成）
- [ ] 积分系统设计与实现
- [ ] 通知系统
- [ ] 标签功能完善
- [ ] 图片预览和管理

---

## 2025-12-25 (周四)

### radish-lib 跨平台构建修复与文档补充

- **fix(rust)**: 修复 radish-lib 在 Rust 2024 下的编译问题
  - `#[no_mangle]` 迁移为 `#[unsafe(no_mangle)]`
  - `benchmark` 并行计算补齐 `rayon` 依赖与导入
  - `watermark` 改用 `ab_glyph` 以匹配 `imageproc` 的字体/scale 类型
  - 修复水印加载图片时的错误消息格式
- **docs(rust)**: 补充 Linux/WSL 构建依赖说明
  - Ubuntu/Debian/WSL 需要 `build-essential` 以提供 `cc/gcc` 与 `crt*.o`
  - 建议同时安装 `clang`、`pkg-config`
- **提交**：`7ca3a79 fix: 修复 radish-lib Rust 2024 编译`

---

## 2025-12-28 (周六)

### 个人中心功能完善与体验优化

#### 1. 个人中心核心功能实现

- **feat(profile)**: 实现个人中心完整功能
  - **个人资料管理**：
    - 新增 `GetMyProfile` 接口（获取当前用户资料+头像）
    - 新增 `UpdateMyProfile` 接口（更新昵称/邮箱/真实姓名/年龄/地址）
    - 新增 `SetMyAvatar` 接口（绑定头像附件）
    - 新增 `GetMyPoints` 接口（积分余额占位，M6 将接入真实数据）
  - **数据模型**：
    - `UserProfileVo`：个人资料响应（包含头像 URL）
    - `UpdateMyProfileDto`：资料更新请求
    - `SetMyAvatarDto`：头像绑定请求
    - `UserPointsVo`：积分余额响应
  - **前端实现**：
    - `UserInfoCard` 组件：资料卡片+编辑表单+头像上传
    - `UserAttachmentList` 组件：附件列表+筛选+删除+复制链接
    - 新增"我的附件"标签页
  - **HTTP 测试**：
    - 在 `Radish.Api.AuthFlow.http` 中新增 B.5-B.8 测试用例
  - **提交**：`69e4be0 feat: 个人主页新增我的附件列表`、`3919214 feat: 个人中心完善（资料/头像/附件管理）`

#### 2. 资料更新校验增强

- **fix(profile)**: 为个人资料更新增加完整校验
  - **长度限制**：
    - 用户名 ≤ 200 字符
    - 邮箱 ≤ 200 字符
    - 真实姓名 ≤ 50 字符
    - 地址 ≤ 2000 字符
  - **格式校验**：
    - 邮箱格式验证（`System.Net.Mail.MailAddress`）
    - 性别值范围检查
    - 年龄非负数检查
  - **唯一性校验**：
    - 用户名唯一性（排除自己）
    - 邮箱唯一性（排除自己）
  - **架构优化**：
    - 移除 `UserController` 对 `IUserService.Db` 的直接访问
    - 改用 `IUserService.UpdateColumnsAsync` 方法
    - 修复单元测试中缺失的 `IAttachmentService` 依赖
  - **提交**：`c14b61b fix: 个人中心资料更新校验`

#### 3. 用户体验优化

- **feat(ux)**: 个人中心体验优化
  - **前端错误提示**：
    - 编辑资料保存失败时显示后端 `MessageInfo`
    - 错误提示样式（红色背景+边框）
    - 关闭弹窗时自动清空错误提示
  - **清空头像功能**：
    - 后端支持 `attachmentId=0` 表示清空头像
    - 前端增加"移除头像"按钮（仅当有头像时显示）
    - 清空头像时保留 `BusinessType=Avatar`，仅清空 `BusinessId`
  - **头像关联策略**：
    - 同一用户只保留最新头像关联
    - 设置新头像时自动取消旧头像的 `BusinessId`
    - 历史头像仍可在"我的附件"中按 Avatar 类型筛选查看
  - **提交**：`d86653c feat: 个人中心体验优化`

#### 4. 附件下载权限校验

- **feat(security)**: 为非公开附件下载增加权限校验
  - **权限规则**：
    - 公开附件（`IsPublic=true`）：任何人可下载（包括匿名用户）
    - 非公开附件（`IsPublic=false`）：仅上传者本人和 Admin/System 角色可下载
  - **实现方式**：
    - 修改 `IAttachmentService.GetDownloadStreamAsync` 签名
    - 增加 `requestUserId` 和 `requestUserRoles` 参数
    - 在 `AttachmentController.Download` 中传递当前用户信息
    - 在 `AttachmentController.DownloadByToken` 中同样应用权限检查
  - **日志记录**：
    - 记录非授权下载尝试（用户 ID、附件 ID、上传者 ID）
  - **提交**：`0896f3e feat: 附件下载增加权限校验`

#### 5. HTTP 测试脚本清理

- **chore(test)**: 清理 HTTP 测试脚本中的硬编码 token
  - 移除 `Radish.Api.AuthFlow.http` 中的过期 access_token
  - 移除 `Radish.Api.Forum.http` 中的硬编码 token
  - 移除 `Radish.Api.RateLimit.http` 中的硬编码 token
  - 移除 `Radish.Api.Attachment.http` 中的硬编码 token
  - 统一改为占位符提示用户粘贴自己的 token
  - **提交**：包含在 `3919214` 中

**技术亮点**：

1. **完整的个人中心功能**：
   - 资料查看/编辑（昵称、邮箱、真实姓名、年龄、地址）
   - 头像上传/更换/移除
   - 附件管理（列表、筛选、删除、复制链接）
   - 积分余额显示（占位）

2. **数据安全与校验**：
   - 完整的输入校验（长度、格式、唯一性）
   - 附件下载权限控制
   - 头像关联策略（只保留最新）
   - 历史数据可追溯（旧头像保留 BusinessType）

3. **用户体验优化**：
   - 实时错误提示（后端校验失败时显示具体原因）
   - 清空头像功能（一键移除）
   - 附件筛选（按业务类型）
   - 附件搜索（按文件名关键字）
   - 复制链接（一键复制附件 URL）

4. **架构改进**：
   - 移除 Controller 对 `.Db` 的直接访问
   - 统一使用 Service 层方法
   - 权限检查下沉到 Service 层
   - 单元测试覆盖更新

**文件变更统计**：
- 新增：`Radish.Model/ViewModels/UserProfileVo.cs` (~40 行)
- 新增：`Radish.Model/ViewModels/UpdateMyProfileDto.cs` (~15 行)
- 新增：`Radish.Model/ViewModels/SetMyAvatarDto.cs` (~10 行)
- 新增：`Radish.Model/ViewModels/UserPointsVo.cs` (~10 行)
- 新增：`radish.client/src/utils/clipboard.ts` (~25 行)
- 修改：`Radish.Api/Controllers/UserController.cs` (+250 行)
- 修改：`Radish.Api/Controllers/AttachmentController.cs` (+30 行)
- 修改：`Radish.IService/IAttachmentService.cs` (+5 行)
- 修改：`Radish.Service/AttachmentService.cs` (+25 行)
- 修改：`Radish.Api.Tests/Controllers/UserControllerTest.cs` (+80 行)
- 修改：`radish.client/src/apps/profile/components/UserInfoCard.tsx` (+120 行)
- 修改：`radish.client/src/apps/profile/components/UserInfoCard.module.css` (+15 行)
- 修改：`radish.client/src/apps/profile/components/UserAttachmentList.tsx` (+80 行)
- 修改：`radish.client/src/apps/profile/components/UserAttachmentList.module.css` (+50 行)
- 修改：`Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http` (+40 行)
- 修改：`Radish.Api.Tests/HttpTest/Radish.Api.Attachment.http` (+20 行)

**今日工作总结**：
- ✅ 实现个人中心完整功能（资料/头像/附件）
- ✅ 增加完整的输入校验（长度/格式/唯一性）
- ✅ 实现附件下载权限控制
- ✅ 优化用户体验（错误提示/清空头像）
- ✅ 修复架构问题（移除 Controller 对 .Db 的访问）
- ✅ 清理测试脚本中的硬编码 token
- 📊 代码提交：6 个提交
- 📝 功能增强：
  - 个人中心核心功能完成
  - 附件权限控制完善
  - 前端体验优化
- 🎯 架构改进：
  - Service 层职责更清晰
  - 权限检查统一管理
  - 单元测试覆盖提升
