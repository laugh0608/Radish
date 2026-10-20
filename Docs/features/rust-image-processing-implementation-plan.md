# Rust 图片处理完整替代实施方案

> 状态：2026-10-20 待项目所有者确认；本轮只细化方案，尚未修改实现、依赖、CI 或镜像。
>
> 目标：补齐 `Lib/radish.lib`，使正式图片处理、测试和交付完全退出 Six Labors 四包链。C# 保留宿主适配职责，Rust 成为唯一图片实现；采用现有开源 codec，不自行编写通用解码器。
>
> 本文是后续实施设计的真相源；[源码评估](./rust-image-processing-replacement-assessment.md)保留迁移前事实，[许可治理专题](./dependency-license-governance.md)维护跨依赖范围。

## 1. 本次确认的范围

| 范围 | 建议决策 |
| --- | --- |
| 应用边界 | 保留 `IImageProcessor` 的八类操作及 Service 调用层级；补充取消、明确格式、元数据与失败语义 |
| 实现路线 | Rust 解码、变换、文字 / 图片水印、元数据、编码；C# 参数适配、受限调度、临时文件和最终提交 |
| 退出对象 | ImageSharp、Drawing、Fonts、PolygonClipper，包括测试造图、传递依赖、publish / 镜像内容 |
| 兼容范围 | 保留现有接收格式；修复伪扩展名输出、动画丢帧、EXIF 方向和派生图隐私风险，不追求旧编码器逐字节一致 |
| 必要接线 | Attachment / Sticker、配置 / DI、日志、原生构建、候选门禁、Docker 双架构及相关文档 |
| 其他依赖的状态 | AutoMapper 是独立评估对象；ORM / Redis 只做许可与实际使用范围复核，尚未决定替换；均不进入本图片方案 |
| 明确排除，未安排实施 | 文件存储架构重构、历史附件批量重写、Flutter 内嵌 Rust、增加 SVG / AVIF / HEIC / TIFF 支持；这些不是后续必做清单 |
| 切换策略 | 开发阶段先完成能力与交付，最后一次性移除 C# 图片实现；缺库 / 错误 ABI 必须失败，不自动回退 |

这是包含接口和运行时行为变化的方案，确认前不实施。确认方案不等于批准未知版本的包安装、项目启动、镜像发布或生产部署；第 10 节明确后续执行边界。

## 2. 现有能力与补齐任务

源码核对范围：`Radish.Infrastructure/ImageProcessing/`、`Lib/radish.lib/src/`、`AttachmentService`、`StickerService`、`LocalFileStorage`、`FileStorageOptions`、图片测试、`Directory.Build.props` 及候选 / 镜像 workflow。

| 操作 | 当前缺口 | Rust 交付要求 |
| --- | --- | --- |
| `GenerateThumbnailAsync` | 完全委托 ImageSharp，接口默认值与实现默认值不同 | 统一默认 150×150、quality 85；等比装入目标框，不裁切；允许小图放大以延续现有语义 |
| `ResizeAsync` | 完全委托；宽高都为 0 没有明确约束 | 一边为 0 时按比例计算；两边均正时 fit 或 stretch；两边为 0 / 负数拒绝 |
| `CompressAsync` | 完全委托；未知扩展名默认 JPEG | 明确 codec / quality；不会保证每次文件更小；禁止未识别格式落到 JPEG |
| `AddWatermarkAsync`：文字 | 固定字体 / 白色 / padding，按 UTF-8 字节估宽，成功后仍调用 C# 读尺寸 | 参数全覆盖、真实字形布局、中文、source-over 合成；Rust 返回产物信息 |
| `AddWatermarkAsync`：图片 | 完全走 C#；Service 未填写图片路径和 Scale | 补齐图片配置接线、比例缩放、五位置 / 边距 / 透明度；无图或非法参数明确失败 |
| `RemoveExifAsync` | C# 只清 ExifProfile；在派生图生成后执行 | 先处理 orientation，再按隐私策略清理原图与派生图；返回明确处理结果 |
| `GenerateMultipleSizesAsync` | 每个尺寸重复解码，结果可能部分成功 | 同一请求解码复用，按输入顺序返回；整批成功才交给 Service 提交，失败清除本批新产物 |
| `GetImageInfoAsync` | 完全委托，格式 / 动画 / 方向信息不足 | 内容识别，原始与显示尺寸、格式、帧数、时长、EXIF / ICC、源位深和文件大小 |
| `IsValidImageAsync` | 只识别头部，不证明完整可解码 | 在预算内完整解码并验证帧；文件头可识别但主体截断必须失败 |

`.tmp` 输入统一按文件内容识别；输出编码器由显式格式选择，不能从 staging 文件后缀猜测。Rust SHA256 和历史 benchmark 不属于图片替代必要条件；上传哈希继续沿用现有 .NET BCL 语义。

## 3. 接口与调用责任

### 3.1 .NET 接口调整

不新增通用插件框架。继续使用 `IImageProcessor` / `RustImageProcessor`，替换内部实现，并对当前源码内调用方一次性编译迁移：

| 项目 | 拟定契约 |
| --- | --- |
| 八个操作 | 现有主要参数和默认值保留；统一追加 `CancellationToken cancellationToken = default` |
| EXIF 返回值 | `Task<bool>` 改为 `Task<ImageProcessResult>`，能区分损坏输入、编码失败和写入失败；更新所有 mocks |
| `ImageProcessResult` | 保留现有字段；增加稳定 `ErrorCode`、实际 `Format`、`FrameCount`；失败不返回可当成已提交文件的路径 |
| `ImageInfo` | 保留 Width / Height 为存储像素尺寸；增加 DisplayWidth / DisplayHeight、Orientation、FrameCount、DurationMs、HasIccProfile；BitsPerPixel 表示源解码色型，不用 RGBA8 的 32 位伪造源信息 |
| 格式参数 | 处理操作追加可选 `ImageOutputFormat? outputFormat = null`；仅明确 `.jpg/.jpeg/.png/.webp/.gif/.bmp/.ico` 可推断；未知后缀必须显式传入 |
| 动画参数 | 变换操作增加 `ImageAnimationPolicy`（Preserve / FirstFrame），默认 Preserve；缩略图默认 FirstFrame；Service 为静态派生图显式选择 FirstFrame |
| 多尺寸 | `ImageSize` 增加可选 OutputFormat 与 AnimationPolicy；`baseOutputPath` 明确定义为含扩展名的基础路径；尺寸 Name 禁止分隔符、`..`、重复 / 大小写碰撞 |
| Stream | 不关闭调用方流；可 seek 流从 0 读取并在 finally 恢复原 Position；不可 seek 流从当前位置读取，消费一次；流 Length 不是必需条件 |
| 无效内容 | GetInfo 对无效图片返回 null，IsValid 返回 false；IO、缺库 / ABI、内部错误不能折叠为无效图片 |
| 取消 | 抛出 `OperationCanceledException`，不转换成普通 Fail / false；取消后不提交产物 |

所有新增可选参数位于现有参数之后，CancellationToken 最后；多尺寸的格式 / 动画参数按 ImageSize 传递。此次不改外部 HTTP DTO / Vo 字段、数据库结构或附件 URL 规则。缩略图路径和 FileSize / MimeType 必须以实际生成文件更新，不能保留处理前数值。

参数校验规则：

- quality 为 1–100；不再把任意负数视为默认。默认来自参数 / 配置适配层，Rust 收到的是解析后的值。
- 尺寸计算采用整数安全检查，自动尺寸向下取整且最小 1；fit 的两个正维度均不得越过目标框；stretch 精确命中目标尺寸。
- Opacity 为有限数 0–1、Scale 为有限数 `(0,1]`、Padding 非负；颜色支持规范的 `#RRGGBB` / `#RRGGBBAA`，最终 alpha 为颜色 alpha × Opacity。
- 文字空白、无效枚举、非法字体、过大水印均给出稳定错误；不默默成功输出未加水印的图片。
- 旧缩放 / 编码器像素差异以第 8 节语义与视觉标准验收，不要求文件 hash 相同。

### 3.2 Service 与存储接线

`LocalFileStorage` 保留基础大小 / 扩展名 / magic 校验；图片完整验证由图片处理器负责。统一 `FileStoragePolicy` 与 Service 图片识别，修复 `.ico` 接收后不进入处理分支的差异。

上传处理建议顺序：

1. 复用现有上传存储结果，在附件记录创建前按真实内容确认格式、完整性与预算；图片业务即使未请求变换，也执行完整验证。变换输入 / 输出使用请求专有 staging，不改变 `IFileStorage.UploadAsync` 的公共契约。
2. 若请求 RemoveExif，先完成方向归一与隐私清理；其他变换也归一方向，避免旋转标签与实际像素脱节。
3. 添加文字 / 图片水印；读取正确的 Text 或 Image 配置，不将 Text 的位置错误用于 Image。图片不透明度仍沿用现有有效默认，若新增独立配置键，在配置迁移表中明示。
4. 从处理后的图生成缩略图和多尺寸，确保水印与隐私策略覆盖全部派生图。既有 150×150、400×300、800×600、1200×900 调用默认保留，并接通已有对应配置。
5. 全部处理成功后提交新文件并写附件记录；任一步失败按现有上传清理契约删除本次拥有的文件，不触碰已存在的历史附件。

现有 `ReplaceFileWithRetry` 的“先删除目标再移动”改为同目录 staging + 平台支持的同文件系统替换；不把跨盘 move 描述为原子操作。每个文件可原子提交，多文件与数据库不是一个原子事务：继续保留失败补偿，记录清理失败，进程崩溃后孤立文件回收机制作为存储独立议题，不宣称本批实现分布式事务。

文件名、ContentType 和数据库尺寸 / 大小与最终编码保持一致。原图 hash 仍表示上传原始字节，用于现有去重；不改为处理后 hash，也不改变特殊处理跳过去重的规则。不重写历史图片与已存记录。

Sticker 的 96×96 预览接入同一处理器，新建预览建议 PNG 以保留透明度；不重写历史 JPG 预览。现有允许预览失败后降级的业务边界保留；底层仍返回可审计失败，不伪造成功。

### 3.3 Native ABI v1

建议采用**受限 UTF-8 JSON 请求 + 文件输入 / staging 输出 + 小型 JSON 结果**。不通过 JSON / Base64 传图片，也不将全量解码像素跨语言复制。`serde / serde_json` 只处理受限控制消息。

| 导出（拟定） | 职责 |
| --- | --- |
| `radish_image_abi_version()` | 返回 ABI 主 / 次版本；不依赖尝试执行业务判断可用性 |
| `radish_image_capabilities_v1(...)` | 返回格式、操作、动画、codec / build 标识；结果采用与 execute 相同的 response 所有权 |
| `radish_image_cancel_create_v1(...)` / `cancel_v1(...)` / `cancel_free_v1(...)` | 每请求取消句柄；原子标志，只在 execute 活跃期间允许并发 cancel |
| `radish_image_execute_v1(request_ptr, request_len, cancel_handle, out_response)` | 同步执行一个受限请求，返回稳定整数状态；图片输出仅写调用方分配的 staging 路径 |
| `radish_image_response_data_v1(handle, out_ptr, out_len)` / `response_free_v1(handle)` | 借用只读结果字节，调用方复制后释放 Rust 持有的 response |

契约要求：

- C ABI 使用明确 `u32/i32/u64`、字节长度和 opaque handle，C# 使用 `LibraryImport` / Cdecl 与 SafeHandle；不传 Rust String / Vec / bool 的内部布局。
- 请求包含 schemaVersion、operation、输入路径、明确输出格式 / 路径、已解析水印参数 / 字体路径、limits；所有路径由宿主生成或来自管理员配置，不接收 HTTP 任意路径。
- JSON 上限 64 KiB，结果上限 64 KiB，多尺寸最多 8 项；未知主版本、未知字段 / 操作和超限在写输出前拒绝。
- UTF-8 严格解析，禁止 NUL；检查 null、长度范围、溢出与句柄生命周期。原始指针必须由可信 wrapper 提供有效内存，FFI 无法通用证明任意非空指针可读，不能宣传“可安全接收任意野指针”。
- response、cancel handle 各自只释放一次；C# finally 先解除取消回调，再等待 execute 返回，最后释放句柄。异步取消不能释放正在使用的输入或临时文件。
- 所有导出统一捕获可展开 Rust panic，返回 InternalError；明确 `panic=unwind`。这不隔离段错误、abort 或进程 OOM；本方案仍是进程内原生库，不具有子进程沙盒的崩溃隔离。
- Rust 不输出原始路径、文本或 codec 异常到 stderr / 日志；响应只有稳定错误码和白名单诊断分类。详细私密内容不穿越到外部错误消息。
- 原有文字水印 ABI 在迁移期间仅供既有测试定位；完成切换后移除其生产接线并更新直接调用测试。SHA256 / benchmark 导出保留，不混入新图片 ABI 版本。

内部错误码至少覆盖 InvalidArgument、InvalidImage、UnsupportedFormat、UnsupportedFeature、LimitExceeded、FontUnavailable、MissingGlyph、IoRead、IoWrite、Cancelled、InternalError；缺库 / ABI 不匹配属于宿主能力错误。Service 沿用现有附件错误 envelope 和日志所有权；原生不重复写业务终态日志。

## 4. 格式、动画与元数据契约

### 4.1 格式矩阵

| 输入 | 原图变换 / 保存 | 缩略图与多尺寸默认 | 必验内容 |
| --- | --- | --- | --- |
| JPEG / JPG | JPEG，quality 1–100 | JPEG | progressive、灰度 / RGB、EXIF 8 种方向、CMYK 颜色路径 |
| PNG | PNG；无损，quality 不作有损承诺 | PNG | palette、alpha、灰度、8 / 16 位、ICC |
| WebP 静态 | WebP，默认有损 quality，alpha 保留 | WebP | 有损 / 无损输入、透明边缘 |
| GIF 动态 / 静态 | GIF；动态保留帧时长、循环和显示结果 | PNG 第一张完成合成的可显示帧 | disposal、局部帧、透明索引、调色板量化 |
| WebP 动态 | WebP 动态，不静默取首帧覆盖原图 | PNG 第一张完成合成的可显示帧 | blend / dispose、时长、循环、动画重新编码 |
| APNG（PNG 容器） | APNG，识别动画并保留；属于已接收 PNG 的处理边界 | PNG 第一张可显示帧 | default image 与动画首帧区别、帧时序 |
| BMP | BMP，保持真实扩展名与格式 | PNG | 行方向、色型、透明能力明确；不伪称全 BMP 子格式支持 |
| ICO | 原图容器保留全部 entry；变换逐 entry，受 ICO 尺寸约束 | 选最大有效 entry 生成 PNG | 多 entry、内嵌 PNG / BMP、透明掩码、1–256 尺寸约束 |

普通显式 `Resize/Compress/Watermark` 请求输出 GIF / WebP / APNG 时默认保留动画；输出 JPEG / 静态 PNG 且输入为动画时，只有缩略图 / 派生图请求可明确选择 FirstFrame，其余必须显式选择动画策略，不能无声丢帧。动画图片作为水印素材本批拒绝，静态图片水印应用到目标所有帧。

派生图扩展名变化通过实际 OutputPath 回传，并同步更新 LocalFileStorage 的预分配路径、失败清理集合、Service 的路径映射和相关 mocks；不靠“把 `.gif` 文件里写 JPEG”维持旧路径。外部继续通过附件 URL / variant 访问。

未知编码 / 不支持的子类型返回稳定失败；不得只移除格式白名单来宣称替代完成。R0 必须证实上述矩阵可达；不能支持其中一项时停止切换，提交具体降级范围待重新确认。

### 4.2 色彩、透明度和 EXIF

- 普通变换先按 EXIF orientation 归一像素并重置 / 移除方向标签；GetInfo 同时保留 raw / display 尺寸，不在探测时改文件。
- `RemoveExif` 在本批定义为上传隐私清理策略：清 EXIF（含 GPS / 缩略图）、XMP、IPTC、可携带用户文本的注释 / PNG text；保留动画结构、时序与显示必要字段。该语义比旧方法更强，须随方案明确批准。
- 未请求隐私清理时，支持容器中已识别的元数据按白名单保留，方向字段按归一结果调整；不承诺保留未知私有块，响应 / 验证记录说明实际策略。
- 推荐经受审计的色彩管理路径将嵌入 ICC 的图转为 sRGB 后编码，绝不“丢 ICC 但原样保留非 sRGB 数值”。无 profile 的标准 RGB 视为 sRGB；损坏 profile、无法可靠解释的 CMYK 输入失败，禁止静默偏色。
- PNG 的 16 位无损缩放 / 清理路径保留精度；水印与输出 JPEG / WebP 的 8 位量化须显式标识并验证。GIF 的调色板转换以显示结果与透明度验收。
- alpha 缩放使用预乘 alpha 避免黑边；水印使用 source-over，文本字形覆盖率参与 alpha。输出 JPEG 前明确合成到白色背景；其他支持 alpha 的输出保留透明。
- 仅删除 metadata 且无需方向 / 色彩变换时，优先安全的容器重封装以避免有损重编码；需要变换时走明确 decode → transform → encode，不能声称“无损清理”。

## 5. 字体与水印

建议分发可再分发的固定字体资产：Latin 复用 DejaVu，中文增加 Noto Sans CJK 的合适 TTF 字体。记录精确上游版本 / hash / 字体许可，禁止改名混入 Arial 或从开发机复制无分发依据的系统字体。

| 能力 | 实施约束 |
| --- | --- |
| 字体来源 | CustomFontPath 优先；PreferredFonts 在明确的字体目录 / 清单中解析，最终使用随包字体；不采用“系统第一款字体” |
| 样式 | Regular / Bold / Italic / BoldItalic 均需测试；优先真实字面，无字面时可使用经快照验证的确定性合成样式，并记录该策略 |
| 字号 | 保留固定字号、相对图片宽度、Min / Max clamp；向 Rust 传已解析字号 |
| 排版 | `rustybuzz` 候选负责 shaping，字形 raster 复用 `ab_glyph`；按实际 glyph bounds / advances 定位，处理换行与组合字符 |
| 中文覆盖 | 中文 / 英文混排、数字标点、扩展汉字按实际字库校验；无 glyph 返回 MissingGlyph，不生成方框冒充成功 |
| Unicode 边界 | 按 grapheme / script 分段选字体；需要 bidi 的文本必须有明确排版支持或 UnsupportedFeature；彩色 emoji 不列为本批支持能力 |
| 超界 | 五位置、Padding；多行按 `\\n`，不自动截断或任意缩字号；总 bounds 超出可用矩形返回 LimitExceeded / InvalidArgument |
| 图片水印 | 按原图宽度 × Scale 等比缩放，最小 1 像素；超出有效矩形拒绝，源 alpha × Opacity 后复合；素材必须完整验证 |
| 颜色 | 颜色 alpha 与 opacity 相乘；在 alpha=0、0.5、1 以及半透明背景上做精确小图数学测试 |

新字体 / shaping 不以“系统可显示中文”作为交付证据。R0 输出代表图片供复核，字体输出的可重复性优先于不同机器上的系统字体近似一致。

## 6. 资源、并发与故障边界

以下为拟定默认值，属于本次待确认行为；R0 / R5 用固定样本验证，若需扩大限额再记录依据，不把测得数值冒充现有能力：

| 限额 | 建议初值 |
| --- | --- |
| 上传字节 | 继承当前按业务大小：头像 2 MiB、普通图片 5 MiB；内部调用同样有显式输入上限 |
| 单边 / 单帧像素 | 最大 16,384 px；最大 40,000,000 px，乘法 checked |
| 动画 | 最多 300 帧，总解码像素最多 120,000,000；逐帧处理，不能无界 collect 全帧 |
| 元数据 / 字体 / 水印文本 | metadata 合计 4 MiB、字体单文件 32 MiB、文字 UTF-8 8 KiB；另限制 glyph 数与布局 bounds |
| 单请求工作预算 | 估算工作集上限 512 MiB，输入 / 输出 / 中间像素与 codec scratch 都计入；超预算提前拒绝 |
| 每输出文件 | 32 MiB，批量总输出 96 MiB，防止小压缩输入生成无界 staging |
| 调度 | 宿主全局并发 2，等待队列最多 8，等待 5 秒；和容器内存预算共同配置，不按请求无限 Task.Run |
| 时间 | 处理期限 30 秒；解码 / 每帧 / 变换 / 编码阶段检查取消，支持的 codec 使用进度 hook |

`image::Limits` 只是部分解码器的限制工具，不能替代统一预算；codec 分配及 C 库未必支持硬中断。30 秒是合作式截止时间，超时先标记取消，直到 native 返回才能释放资源；不能谎称已杀死后台执行。若验证证明进程内方案无法满足安全 / 资源边界，停止切换并另提 worker 进程方案，本批不悄悄扩建服务。

请求临时目录由 wrapper 拥有，随机名称、限定权限，不用用户文件名；输入复制有大小上限和取消。生成文件不得覆盖输入 / 水印 / 字体；输出预留为本请求独占，拒绝路径穿越、重复输出和符号链接逃逸。成功 / 失败 / 取消均释放句柄并清理本次 staging；磁盘满、权限失败、清理失败须有可验证的安全错误。

## 7. 依赖选择与构建交付

### 7.1 Rust 依赖提案

| 依赖 | 决策 / 用途 | 实施前检查 |
| --- | --- | --- |
| `image` | 复用当前解析的 0.25.10 作为验证起点，关闭 default-features，仅保留所需格式 | 精确版本安全审计、decoder limits、orientation、ICC 与 16 位路径 |
| `imageproc / ab_glyph` | 复用已解析 0.25.1 / 0.2.32；只保留真实使用能力 | 不以 imageproc 默认文字绘制代替完整排版 / alpha 验证 |
| `gif / png` | 需要直接使用动画 / metadata API 时显式声明当前受支持版本 | GIF / APNG 编码、disposal / blend、metadata 清理能力 |
| `libwebp-sys` + libwebp | 首选补齐有损 / 动画 WebP 的 codec 路径；wrapper 将 unsafe 集中隔离 | crate 与实际 vendored C 源码版本 / 许可证 / CVE，静态链接与双架构构建；不是仅审计 Rust 包名 |
| `rustybuzz`、必要的 Unicode 分段 / bidi crate | 排版；确有需要才加入对应 Unicode 组件 | 版本冻结、许可与字体解析兼容；不默认声称 rustybuzz 自动完成字体 fallback / bidi |
| `lcms2` + Little CMS | ICC → sRGB 候选，优先固定源码静态链接 | wrapper / C 库许可、安全、构建输入、CMYK / 高位深准确性 |
| `serde / serde_json` | 有界请求 / 结果协议 | 精确版本，递归深度、未知字段、输入长度限制 |

上游已确认 `image 0.25.10` 的 [WebPEncoder](https://docs.rs/image/0.25.10/image/codecs/webp/struct.WebPEncoder.html)只支持无损编码；因此不能用它的存在宣称 quality / 有损已实现。[libwebp-sys API](https://docs.rs/libwebp-sys/0.14.4/libwebp_sys/)包含动画编解码 / mux 接口，是候选能力证据，不等于本项目已验证。色彩与排版入口分别见 [lcms2](https://docs.rs/lcms2/6.2.0/lcms2/) 和 [rustybuzz](https://docs.rs/rustybuzz/latest/rustybuzz/)。

上述新增依赖仍是候选，不是已批准安装单。R0 开始前提交**精确 crate / C 库版本、feature、字体来源 / hash、命令、Cargo.lock 变化**供批准；不得为了走 Rust 路线换入另一条需要 key / 不适配许可的依赖链。此前已授权的 XML / Pkcs 安全修复保留，与本方案分开验收。

### 7.2 平台和产物

| 平台 | 目标 / 交付要求 |
| --- | --- |
| 正式 Linux x64 | `x86_64-unknown-linux-gnu`，对应现有 linux/amd64 镜像 |
| 正式 Linux arm64 | `aarch64-unknown-linux-gnu`，对应现有 linux/arm64 镜像；不是编译成功即可免运行测试 |
| 开发 macOS arm64 / x64 | 对应 aarch64 / x86_64 apple-darwin，与 .NET RID 匹配；至少各有 CI / 真实 runner 的加载测试 |
| 开发 Windows x64 | x86_64-pc-windows-msvc；验证 DLL / CRT 依赖、Unicode 路径、文件锁与替换 |
| 本批不宣称支持 | musl / Alpine、Windows arm64、移动端原生打包；需要时另定工具链与验收 |

这里的 Windows / macOS 是后端开发宿主，与 Flutter P8 平台 readiness 分属不同边界；不能复用 Flutter 的 Go / 未完成状态替代后端 native 验证。R0 同时固定各 target 的最低 OS / glibc / CRT 版本，写入 manifest 与构建矩阵，不仅列 CPU 架构。

交付落点：

1. 跟踪 `Lib/radish.lib/Cargo.lock`，调整该目录忽略规则；新增固定 `rust-toolchain.toml`，冻结 target / profile / features，正式构建使用 `--locked`。不运行无边界 `rustup update`。
2. `build.sh / build.ps1` 接受明确 RID / target / profile，产物按 `artifacts/native/<rid>/<profile>/` 隔离；缓存 key 包含 toolchain、lock、features、源码、C toolchain 和字体 hash。不得混用本机架构缓存。
3. 产物含库、字体、LICENSE / NOTICE、依赖清单、manifest（ABI、target、profile、源码 / 输入 hash、库 sha256）；不提交二进制到源码树。
4. `Directory.Build.props` / 专门 targets 改为按目标 RID 和配置精确消费 manifest；Release 不回退 Debug。API 和真实 native 测试显式声明必需，缺产物构建 / publish 失败；其他宿主不因共享程序集引用就全部强制加载图片库。
5. 开发入口采用显式“先构建 native，再 dotnet build/test”脚本并接入后端验证入口；CI 安装冻结工具链。普通 restore 不偷偷安装 Rust，日志必须给出准确补建命令。
6. `Radish.Api/Dockerfile` 增加 target 对应 Rust / C codec 构建 stage，以匹配最终 .NET 基础镜像的 glibc 下限构建；只复制最终库与必要资产。Auth / Gateway / DbMigrate 仅在真实执行图片能力时才增加运行依赖，否则确认发布输出不含无用 native 包。
7. `.dockerignore` 排除 `Lib/radish.lib/target`、本机 native artifacts / 临时输入，显式保留交付许可证和字体。当前排除根 LICENSE 的规则需核对 NOTICE 复制路径，不能使声明随构建丢失。
8. `.github/workflows/candidate-quality.yml` 和其调用入口增加 native build → Rust tests → C# ABI tests → 现有候选门禁；设置 `RADISH_TEST_NATIVE_LIBRARY` 指向本轮产物，缺失 / 错架构不得 skip。平台 jobs 的结果必须汇入已有 required `Candidate Quality`。
9. Docker workflow 已发布双架构，但现有扫描仅 amd64；新增 arm64 产物检查、运行自检和漏洞评估。PR 候选先执行不 push 的 API 镜像构建 / 自检，避免到 tag 发布才发现缺 `.so`。
10. 核对动态链接依赖（Linux `ldd/readelf`、macOS `otool`、Windows 对应工具）及最低 OS / libc；生成包含 vendored C codec 和字体的 SBOM / notices，不能只依赖 cargo 包清单或镜像扫描识别静态链接组件。

### 7.3 加载与配置迁移

Rust 单实现切换时移除 `UseRustExtension`、`CSharpImageProcessor`、选择 / 回退工厂和多处重复 DI 接线，统一注册。旧 key 若仍存在且值为 true / false，均明确报告配置迁移错误；文档提供删除键的迁移说明，避免旧 false 被悄悄忽略。无旧 key 使用新的单实现默认。

API 启动时验证目标架构、ABI、必需 capability、随包字体和受信任资源目录；缺任一项阻止就绪，并给出不泄漏路径内容的诊断分类。只从受控应用目录 / 显式测试路径加载，防止进程工作目录中的同名库被优先解析。

不需要图片处理的宿主不做该启动探测。自动化测试若使用 mock 不加载 native；正式 ABI 套件必须实际加载。字体资产允许只读部署，不要求容器安装系统字体。

## 8. 验证矩阵与通过标准

### 8.1 样本与测试层级

建立有来源 / 许可 / sha256 的固定 fixture 集：正常与截断 JPEG、灰度 / CMYK / EXIF orientation 1–8、PNG palette / 16bit / alpha / ICC、静态与动态 WebP、GIF 局部帧 / disposal、APNG、BMP、ICO 多 entry、伪后缀 / `.tmp`、超尺寸声明、畸形 metadata。测试造图使用简单自有 fixture / Rust 工具，不再引用 Six Labors。

| 层级 | 核心断言 | 通过标准 |
| --- | --- | --- |
| Rust 单元 / 属性 | 尺寸计算、参数范围、合成、元数据过滤、预算 / 错误 | 边界与正常样本全部通过；整数溢出 / panic 不穿 ABI |
| 格式 corpus | 内容识别、完整解码、真实格式与后缀、输出独立读回 | 每项格式矩阵有正 / 负样本；不能把相同库自编自解作为唯一正确性证据 |
| 水印 | 五位置、文字样式 / 中文 / 换行、颜色 / alpha、图片 Scale | 简单像素合成精确；bounds ≤1 px 取整误差；代表图人工复核无缺字 / 黑边 / 截断 |
| 动画 | 帧合成、循环、时长、静态预览 | 所有帧显示序列保留，时长误差不超过对应格式的单个时间量化单位；不靠原始压缩帧计数判断显示等价 |
| 色彩 / 隐私 | orientation、ICC、GPS / XMP / IPTC / 文本块 | 方向正确、参考色卡无未解释偏色；清理后所有原图和派生图均无禁止 metadata |
| .NET ↔ Rust ABI | 真库加载、操作全覆盖、Unicode 路径、取消 / 句柄 | Linux 两架构及支持的开发目标通过；必需测试 skip=0；测试 manifest 对上当前构建 |
| 故障注入 | 缺库、错 ABI、磁盘满 / 只读、坏字体、批量第 N 项失败 | 无成功假象，无泄漏 / 越界覆盖；失败时已存在目标保持完整，本次 staging 被清理 |
| 上传 / Sticker 集成 | 全选项组合、命名 / MIME / 大小、DB 失败补偿、权限 | 现有外部错误 envelope、租户 / 附件归属、去重规则不退化；两类水印均真正生效 |
| 日志 | Native 错误到 Service 的所有权 | 不含路径 / 文本 / codec 原始异常，无重复终态事件；旧 fallback 测试改为明确失败 |
| 压力 / fuzz | 并发 / 等待上限、畸形输入、取消、资源回收 | 固定 runner 上达到第 6 节预算；有效 corpus 不 panic，独立进程 fuzz 无可复现崩溃 / 超界 |
| 交付 | 无本机 target 缓存的 build / publish / Docker | 双架构镜像可实际加载并处理 fixture；缺字体 / 缺库镜像必须失败 |
| 退出审计 | 源码、resolved graph、输出包、镜像 | 六实验室四包在正式直接 / 传递 / 测试 / 发布物中均为 0；历史 docs 文字不作误报 |

无损像素参考用可独立解释的小 fixture 与公开格式样本；有损 JPEG / WebP 在代表性照片、默认 quality 85 下初始目标为参考图 SSIM ≥0.98（同尺寸、统一 sRGB），同时检查透明边缘 / 视觉样本。quality 1 / 100 用于参数与编码边界，不套用相同画质阈值。不同算法不能用逐字节 hash 比较；阈值无法满足时记录具体样本与差异并重新评审，禁止无记录降低门槛。不把相对旧 ImageSharp 的相似度当作唯一质量指标。

性能不承诺“Rust 必然更快”。R0 / R5 固定 CPU、内存、样本、并发和 warm-up，记录 p50 / p95、峰值 RSS、临时 IO、产物体积；建议 1000 次混合请求后 native 活跃句柄归零，稳定段 RSS 不持续增长，正常验收样本在 30 秒内完成。内存估算之外还测进程 RSS / 容器限额；不能用只测 Rust allocator 的结果代表 C codec 分配。

### 8.2 候选命令与运行证据

实施后新增并接通一个 native 验证入口，内部至少包括 `cargo fmt --check`、`cargo clippy --locked --all-targets -- -D warnings`、`cargo test --locked`、Release build 和 C# 真实 ABI 套件；独立 sanitizer / fuzz job 使用固定工具版本并保存失败输入，不必在每次普通文档修改运行。

合并候选执行当前[验证基线](../guide/validation-baseline.md)中的仓库质量、前端、后端严格构建、全量测试 / PostgreSQL、identity、依赖审计，并加 native / publish / 不 push 的镜像检查。Rust / vendored C / 字体的安全与许可审计追加到候选材料，不能以 npm / NuGet 的零漏洞覆盖。

成组验收需一次真实 PC / mobile 上传 Smoke：头像、帖子图片、表情，覆盖缩略图、中文水印、图片水印、透明图、动图和失败反馈。启动服务 / 容器的命令、端口和清理由实施阶段另列申请；此前 3993 项通过、真实 native 项跳过的结果不继承为本批证据。

## 9. 分阶段实施和停止线

| 阶段 | 交付 | 阶段门槛 |
| --- | --- | --- |
| R0 能力验证 / 冻结输入 | 依赖精确清单、codec / 字体 spike、格式 fixtures、平台构建样例、性能预算 | WebP 有损 / 动画、GIF / APNG、中文、水印 alpha、ICC / orientation 和目标构建均可达；任何未达项必须上报，不缩范围冒充通过 |
| R1 ABI / 基础处理 | versioned ABI、错误 / 取消 / staging、探测 / 验证 / 缩放 / 编码 / 限额 | Rust 与 .NET 真库契约测试通过；不切正式 DI |
| R2 完整图片语义 | 两类水印、字体、动画、元数据、多尺寸 | 第 4–6 节矩阵全部覆盖，输出代表图复核 |
| R3 应用与退出 | Service / storage / mocks / DI / 配置迁移，移除 Six Labors | 无 C# 委托 / 回退，严格 Debug / Release 可在无 Six Labors key 环境通过 |
| R4 构建交付 | 开发脚本、RID 隔离、lock / toolchain、Docker、CI、notices / SBOM | 干净构建与真实双架构运行，必需 native 测试零跳过 |
| R5 批次验收 | 全量候选、压力与故障、PC / mobile Smoke、最终清单 / 记录 | 代码、能力、分发、漏洞 / 许可和运行证据一致，再评估恢复 dev → master PR |

R1–R4 的构建接线可以随能力开发同步推进；表中顺序是验收关卡，不能等 R3 删库之后才首次尝试 Linux 构建。R0 未过不得切换生产路径，R3 / R4 不得以“以后补原生平台”为理由宣称完整替代。

建议代码落点：Rust 按 `ffi / image::decode / encode / transform / watermark / metadata / limits` 的真实职责分文件；C# 按 native binding、请求适配 / 调度、产物提交分工。只在需要复用时抽取，不建立通用跨语言 RPC 系统。

如果迁移候选不合格，保留在 dev 修复且继续暂缓 PR；已移除库的版本不能通过重新打开开关恢复 C#。部署回滚只能选经过独立验收、许可 / 安全可用的完整历史制品；不把当前已知漏洞旧版或没有 key 的升级版当成默认可回滚制品。

## 10. 确认清单与本轮边界

项目所有者确认本方案时，需明确接受这些实质性变化：

1. Rust 单实现以及必需 native / 字体能力的启动门禁，移除 `UseRustExtension` 和静默回退。
2. 内部接口的取消、结果和格式扩展；EXIF 返回类型迁移，不扩外部 API / 数据库。
3. 动态原图保留动画、派生图静态预览，BMP / ICO 等派生 PNG；修复图片水印配置与文件信息接线。
4. orientation 归一、明确色彩转换、全链路隐私清理、固定字体以及第 6 节有限额的处理。
5. Rust crate 之外可能增加 libwebp / Little CMS 与字体资产；精确版本、许可和构建方案先验再批准下载。
6. Linux 双架构及已列开发平台的 native 构建 / 测试进入交付；完成证据前继续暂缓 PR。

本轮交付仅为设计与文档检查，不新增 Rust / C# 实现，不修改项目依赖 / Cargo.lock / 配置 / CI，不运行新服务或申请许可。下一步是确认方案，再整理 R0 精确依赖和执行清单；不能把“方案已写完”记为“图片替代已完成”。
