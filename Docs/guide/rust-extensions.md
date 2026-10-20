# Rust 图片处理与原生产物

后端图片处理使用 `Lib/radish.lib` 单一实现，C# `RustImageProcessor` 负责调度、流所有权、临时文件和最终输出提交。旧 `CSharpImageProcessor`、选择工厂和 Six Labors 引用已删除。能力约定见[实施方案](../features/rust-image-processing-implementation-plan.md)，本轮证据与未完成项见[实施记录](../records/rust-image-replacement-2026-10-20.md)。

## 构建前提

- Node.js 24、.NET SDK 10、固定 Rust 1.96.0；Rust 工具链配置在 `Lib/radish.lib/rust-toolchain.toml`。
- 对应平台 C 编译工具链：Linux GCC / libc 开发文件、macOS Xcode Command Line Tools、Windows MSVC C++ Build Tools。
- libwebp 与 Little CMS 从锁定 crate 的 vendored 源码静态编译；禁止通过 `LCMS2_LIB_DIR` 隐式换用系统库。
- 使用仓库已有 DejaVu Sans 与 Noto Sans SC 字体，构建不搜索系统字体目录。
- 安装或更新工具、依赖仍需按当前任务授权；以下构建命令不代表安装授权。

在仓库根目录执行：

```sh
node Scripts/build-native-images.mjs --debug
dotnet build Radish.slnx -c Debug --no-restore --warnaserror
node Scripts/build-native-images.mjs
dotnet build Radish.slnx -c Release --no-restore --warnaserror
```

`Lib/radish.lib/build.sh` 与 `build.ps1` 转发到同一脚本。构建输出位于 `artifacts/native/<rid>/<Debug|Release>`，包含动态库、字体、第三方 notices、`dependencies.json` 和 `manifest.json`。源代码、锁文件、字体、许可文件或构建脚本变化后需重建；MSBuild 拒绝缺失或过期产物。

构建必须在目标平台执行，支持 RID 为 `linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`、`win-x64`。Linux Docker 原生阶段使用 Debian bookworm；macOS 最低目标为 13；Windows 使用 MSVC。矩阵配置不等于所有平台已经实测通过，结果以当批记录为准。

## 配置迁移

从所有共享、宿主、本地与环境变量配置中删除 `FileStorage:ImageProcessing:UseRustExtension`（环境变量形式 `FileStorage__ImageProcessing__UseRustExtension`）。旧值无论 true / false 均导致明确迁移错误；没有 C# 回退。

原有缩略图、多尺寸、质量、EXIF 去除及水印开关继续生效。默认字体优先级为 `Noto Sans SC`、`DejaVu Sans`；自定义字体使用 `FileStorage:Watermark:Text:Font:CustomFontPath` 指定可信本机文件。字体缺字或不支持的参数返回失败，不无声跳过文字。Regular / Bold / Italic / BoldItalic 均有明确实现。

启动先检查当前 RID、ABI、原生库与字体 SHA256。只有测试可通过绝对路径 `RADISH_TEST_NATIVE_LIBRARY` 指定包含 manifest 的同一产物目录；不要把它作为部署时跨平台加载的绕过手段。

## 接口与图片语义

`IImageProcessor` 保留缩略图、缩放、压缩、文字 / 图片水印、去除隐私元数据、多尺寸、信息与有效性八类操作。处理方法接受显式输出格式、动画策略与取消；`RemoveExifAsync` 返回 `ImageProcessResult`。

- JPEG / PNG / WebP / GIF / BMP / ICO 按内容识别，上传同时检查扩展名一致性；不新增 SVG / AVIF / HEIC / TIFF。
- thumbnail 默认首帧静态预览，其余操作默认保留动画。GIF / BMP / ICO 及动画 WebP 的上传派生预览使用 PNG，并返回实际路径与 MIME。
- JPEG / WebP 的 quality 控制有损编码，PNG 不承诺体积缩小；JPEG 在白底合成透明像素。
- EXIF orientation 先应用到像素，ICC 转为 sRGB；不支持的 CMYK 输入明确失败。16-bit PNG 静态处理保留精度，8-bit 输出显式量化。
- `RemoveExifAsync` 去除 EXIF / ICC / XMP / IPTC / 私有文本等元数据；无需方向 / 色彩变换时 JPEG / PNG / WebP 使用容器重封装，避免无意义再次有损编码。
- 默认输入 5 MiB、单帧 40 MP、最多 300 帧、总 120 MP、工作预算 512 MiB；单输出 32 MiB、批次 96 MiB。宿主全局并发 2、排队 8、等待 5 秒、协作截止 30 秒。预算属于预检与协作取消，不能当作进程级隔离。
- 输入流不关闭，可定位流恢复原位置；所有输出先写私有临时目录，整批成功再提交，失败恢复既有目标并清理暂存。

业务上传先验证完整内容，再处理原图隐私元数据、水印和派生尺寸，最终大小按实际文件回填。此变更不批量重写历史附件，也不改变存储架构。

## 验证与交付

```sh
node Scripts/validate-native-images.mjs
node Scripts/check-native-image-security.mjs
dotnet test Radish.Api.Tests -c Release --filter 'FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~OutboxNativeLoggingTests'
dotnet publish Radish.Api -c Release --no-restore -o artifacts/image-publish
dotnet artifacts/image-publish/Radish.Api.dll --check-native-images
```

`--check-native-images` 在业务宿主创建之前执行中文水印、WebP 输出与读回测试，不监听端口、不连接业务数据库。Dockerfile 在 Linux 目标架构构建 Rust，再把原生产物随 API 发布；经任务授权后运行 `node Scripts/check-native-image-docker.mjs` 验证 amd64 / arm64，完成后删除测试镜像，不推送镜像。

`.github/workflows/native-images.yml` 覆盖五个宿主目标和 Linux 双架构镜像，纳入候选检查。安全审计读取锁文件的实际依赖图；停止维护提示单独记录，不与可利用漏洞混为一类。字体及第三方 notices 必须随产物分发。
