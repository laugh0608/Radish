# Rust 图片处理替代评估

> 2026-10-20 迁移前源码审计与候选方案；本页保留历史基线，当前实现与验收见[实施记录](../records/rust-image-replacement-2026-10-20.md)。隶属[第三方依赖许可治理](./dependency-license-governance.md)。
>
> 结论：既有 Rust 文字水印与哈希是真实实现，但目前仍是依赖 ImageSharp 的混合处理器。优先评估补齐 Rust，不能通过修改一个配置开关完成 Six Labors 移除。

项目所有者已确认按补齐 Rust 的方向推进；后续接口、能力、构建和验收方案以[完整替代实施方案](./rust-image-processing-implementation-plan.md)为准，当前已进入交付与验收。本文保留迁移前源码事实与候选比较，不表示原生能力已补齐。

## 1. 已有资产与调用链

| 资产 | 源码位置 | 真实职责 |
| --- | --- | --- |
| 应用接口 | `Radish.Infrastructure/ImageProcessing/IImageProcessor.cs` | 缩略图、缩放、压缩、水印、EXIF、多尺寸、图片信息、有效性八类操作 |
| 默认 C# 实现 | `CSharpImageProcessor.cs` | 当前主要图片能力，使用 Six Labors |
| Rust wrapper | `RustImageProcessor.cs` | 文字水印 P/Invoke + C# 委托 / 回退，不是完整原生实现 |
| 工厂 | `ImageProcessorFactory.cs` | `UseRustExtension` 且库可用才选 Rust；否则选 C# |
| Rust FFI | `Lib/radish.lib/src/lib.rs` | 业务 ABI 为 `add_text_watermark`、`calculate_file_sha256`；另有历史 benchmark 导出 |
| 原生实现 | `src/image/watermark.rs`、`src/hash/file_hash.rs` | Rust 图片解码 / RGBA 文字绘制 / 保存及文件 SHA256 |
| 复制接线 | `Directory.Build.props`、`build.sh / build.ps1` | 复制已存在的库；不等于所有候选都自动构建原生库 |
| 运行配置 | `Radish.Api/appsettings.json` | 源码默认 `UseRustExtension: false`，不代表生产实例当前值 |

应用上传主路径 `AttachmentService` 当前使用 .NET `SHA256.Create / ComputeHashAsync`；Rust 哈希存在不意味着所有上传依赖该 ABI。

## 2. 能力矩阵

| 能力 | 当前 Rust wrapper 实际路径 | 移除 Six Labors 前需要完成 |
| --- | --- | --- |
| 生成缩略图 | C# 委托 | 原生缩放策略、默认尺寸、质量和输出格式 |
| 调整大小 | C# 委托 | 保比例 / 指定宽高、自动尺寸、插值与边界 |
| 压缩 | C# 委托 | JPEG / WebP 质量、PNG 语义、无损 / 有损与文件大小契约 |
| 文字水印 | 尝试 Rust，失败回退 C#，成功后仍用 C# 读尺寸 | 完整参数、字体 / 布局 / alpha、原生元信息返回与失败契约 |
| 图片水印 | 正常 C# 路由 | 解码、缩放、位置、透明度和复合 |
| 移除 EXIF | C# 委托 | EXIF / orientation / ICC / 其他元数据策略及隐私契约 |
| 多尺寸输出 | C# 委托 | 输出命名、解码复用、部分失败和产物清理 |
| 图片信息 | C# 委托 | 宽高、格式、大小、EXIF、位深等元数据 |
| 图片有效性 | C# 委托 | 内容识别、损坏输入、格式与资源边界 |
| 文件 SHA256 | 有独立 Rust ABI | 决定是否保留；不是移除 ImageSharp 的必要条件 |

不得在原生替代后保留 `new CSharpImageProcessor(...)` 或测试中的 Six Labors 图像构造依赖，然后宣布退出完成。可以保留 C# 作为宿主适配层，但它不能继续引用目标图片库。

## 3. 静态确认的差异与风险

### 3.1 `.tmp` 输入与格式识别

wrapper 将流写为 `radish_input_<guid>.tmp`，而 Rust 使用 `image::open(input_path)`。本机 lock 解析的 `image 0.25.10` 源码明确按扩展名确定格式，该入口没有调用 `with_guessed_format`。因此该组合存在有效图片也因 `.tmp` 扩展名失败并回退的机制性问题。

此结论来自 wrapper、Rust 和当前依赖源码联查（[image 0.25.10 上游源码](https://github.com/image-rs/image/blob/v0.25.10/src/images/dynimage.rs)），不冒充本轮动态复现。后续应比较内容识别、流 / 字节 ABI 与带真实后缀临时文件的边界，不能只把所有错误转成成功回退。输出 `.tmp` 路径同样应核对：`AttachmentService` 的临时文件、编码器选择和提交最终文件需要一并定义。

### 3.2 文字水印尚不等价

- C# 接口包含颜色、padding 和字体配置；现有原生 ABI 没有传入这些信息。
- Rust 固定白色、固定 20 像素 padding，内嵌 DejaVu Sans；未读取用户自定义字体、字体样式或字体优先级。
- 位置计算使用 `text.len() * font_size * 0.6` 估算宽度；`text.len()` 是 UTF-8 字节数，无法代替字形度量、中文覆盖与实际布局。
- opacity 虽传入 RGBA alpha，但依赖 `imageproc` 的绘制算法对通道做字形覆盖加权，不能据此证明与既有 source-over 透明混合一致；必须做像素级验证。
- 字体缺字、小图、大字号、多行、超长文本、空文本和边界裁剪均需明确结果，不能以输出文件存在替代视觉正确。

### 3.3 格式与元数据

配置允许 `.jpg / .jpeg / .png / .gif / .webp / .bmp / .ico`；部分业务代码的识别列表与配置不同，替代前必须先冻结实际接收 / 处理矩阵。

当前 Rust 水印无差别转 RGBA 并按输出路径保存，不证明动图帧 / 时序、透明通道、EXIF orientation、ICC、位深和不同编码参数等价。PNG、JPEG、WebP 的输出能力分别验证；不要从 `image` 的默认 feature 推定全部格式均能按所需质量编码。

受限解码、像素上限、内存预算、压缩炸弹、损坏 / 截断输入和大并发属于图片基础设施的必要边界，必须先写入契约，再验证。

### 3.4 ABI 与错误传播

当前 ABI 使用原始 C 字符串指针、整数错误码；入口没有统一的 null / 长度检查与 panic 边界。补齐原生能力时需同时确认：

- 所有指针 / 字节长度的有效性、UTF-8、平台路径编码和参数范围；不依赖调用方永远传对。
- panic 不穿越 FFI、错误码 / 安全失败分类稳定；底层路径和水印文本不直接输出日志。
- 输出缓冲 / native handle 的所有权、释放、异常与取消；若使用流 / 字节 ABI，避免不受控的全量复制。
- 临时文件唯一性、权限、原子替换、并发、取消和失败清理；不吞掉权威写入失败。

这些是待确认的 ABI 设计要求，不在本轮修改既有导出接口或容错架构。

## 4. 原生构建与分发缺口

- `Directory.Build.props` 只复制 `target/{release|debug}` 中已存在的 `.dll / .so / .dylib`，不会运行 cargo；Release 优先再回退 Debug 的当前复制策略，也不能直接作为正式原生候选规则。
- API / Auth / DbMigrate Dockerfile 使用 .NET SDK build，不含 Rust 编译阶段；当前 Docker workflow 也没有建立对应 Rust 编译步骤。本机缓存产物不能替代 Linux 候选。
- `Lib/radish.lib/Cargo.lock` 在该目录 `.gitignore` 中且当前未跟踪。本次 154 项 Cargo 统计来自本机 lock 快照，不能宣称新 checkout 与 CI 会解析完全相同版本。正式原生依赖应先确认 lock / feature / toolchain 冻结策略。
- 原生库是服务端依赖；服务器镜像目标架构与开发主机的 Windows / Linux / macOS 验证分开，不因为 Flutter 支持多个平台就推定 Flutter 必须打包该服务端库。
- DejaVu 字体通过 `include_bytes!` 内嵌，字体许可与 notices 必须随产物分发；现有文档建议将 Arial 改名使用的做法不适合作为可复现、可分发方案，已移除该建议。
- 缺少库、错误架构、错误 ABI / 导出符号、共享系统库缺失，应有明确启动 / 能力检查结果；退出 Six Labors 后不能再以它作隐式回退。

正式候选需要在干净构建输入上为实际支持的目标建立构建、测试、SBOM、漏洞审计、许可材料和发布校验。具体架构矩阵在实现方案中确认，不在此提前承诺所有平台 Go。

## 5. 对候选路线的判断

| 路线 | 初步判断 | 进入实施的条件 |
| --- | --- | --- |
| 补齐既有 Rust | 优先研究，现有资产有复用价值 | 八类图片操作完整、ABI 明确、字体 / 格式契约可达、原生产物交付可验证 |
| Rust + 另一套 C# 图片库 | 仅在有明确功能或平台收益时考虑 | 不能只是为回退而保留两套完整实现；必须解释双栈长期成本 |
| 统一 SkiaSharp | 原生栈替代候选 | 核对格式、动画 / 元数据与字体，验证 Linux native assets 和全部传递许可 |
| 统一 Magick.NET | 格式 / 元数据能力候选 | 核对实际 delegates、资源限制、体积、CVE 和多平台原生产物 |
| 保留 Six Labors | 许可管理备选 | 先明确适用条款与 key 交付；不作为本次替代专题默认方案 |

建议先做有明确验收输入的 Rust 能力验证，再选定单一主实现。现有 `image 0.25.10` 为 `MIT OR Apache-2.0`，`imageproc 0.25.1` 为 MIT，`ab_glyph 0.2.32` 为 Apache-2.0；这不是自行编写解码器，而是控制适配层与交付。传递 crate、原生 codec 和字体仍须逐项审核。

## 6. 验证矩阵与退出条件

1. **图片行为**：真实 JPEG / PNG / WebP 以及允许的 GIF / BMP / ICO 样本；尺寸 / 比例、质量、透明通道、帧 / 时序与错误输入；明确保持和拒绝的行为。
2. **水印**：中文 / 英文 / Unicode、五种位置、颜色、padding、字体覆盖、字号、透明度；以像素 / 图像差异和代表输出复核，不仅断言 `Success`。
3. **元数据与安全**：EXIF 移除与 orientation、ICC 策略、文件真实格式、像素 / 内存 / 并发上界、取消、故障清理。
4. **ABI**：成功 / 失败码、null / 越界参数、Unicode 路径、缓冲和句柄所有权、panic 隔离；通过 C# 调用本轮真正构建的库。
5. **生产接线**：上传、压缩、缩略图、文字 / 图片水印、多尺寸、EXIF 临时文件与最终提交、结果尺寸回传；保持现有错误和日志所有权。
6. **交付**：无本机 `target` 缓存的干净构建，正式服务目标 Release / publish / Docker，缺库和不兼容库错误；不使用开发者私有 license key。
7. **退出 Six Labors**：直接 / 传递图、测试造图、输出目录、publish / 容器及文档清单均无四包；配套字体 / codec 许可材料完整。

上轮 3993 项通过的 .NET 测试仅说明升级候选行为回归；真实 Rust ABI 项因缺少显式动态库路径跳过。该结果不能替代本专题的原生覆盖。现有 `OutboxNativeLoggingTests` 有直接原生导出和 wrapper 失败回退验证资产，但不能据此证明所有图片能力已原生化。

本轮只读核对，不新增原生测试结论、不安装 crate、不构建镜像、不修改 FFI、运行时开关或业务接口。
