# Rust 图片处理替代实施与验证记录

> 日期：2026-10-20–21。状态：原生交付、图片能力、隔离浏览器验收及修正后完整预检均通过，已由 [PR #75](https://github.com/laugh0608/Radish/pull/75) 合并为 `4a8589b6` 并完成 dev 回灌。方案见[完整替代实施方案](../features/rust-image-processing-implementation-plan.md)，精确授权见[依赖清单](../features/rust-image-processing-dependency-proposal.md)。

## 本批范围

继续 .NET，图片处理改为 Rust 单实现，删除 Six Labors ImageSharp / Drawing 直接引用及 Fonts / PolygonClipper 传递链，保留 XML / Pkcs 安全修复。不调整 AutoMapper、ORM、Redis、npm 声明与锁文件，不新增 SVG / AVIF / HEIC / TIFF，不改 Flutter 运行时、不迁移历史附件。

实现包含版本化 ABI、受限解码、六格式编码、动画、方向 / ICC、隐私元数据、中文与图片水印、多尺寸输出、取消、输出回滚，以及 C# Service / storage / mocks 对接。旧 `add_text_watermark` 与 `imageproc` 已无调用，按授权清单移除。`UseRustExtension` 的迁移要求见[运行指南](../guide/rust-extensions.md)。

构建固定 Rust 1.96.0 / Cargo.lock，按 RID 与 Debug / Release 隔离原生产物；manifest 校验源输入、动态库、字体与 ABI，发布包含依赖清单和 notices。新增五宿主平台及 Linux 双架构 CI，API 自检在业务宿主创建之前运行。

## 已取得的本机证据

环境：macOS arm64，本机 .NET 10 / Rust 1.96.0。测试数量仅代表对应执行时的工作区；最终候选仍需核对。

| 检查 | 结果 |
| --- | --- |
| 移除 Six Labors 后 NuGet restore | 通过 |
| Debug / Release 严格构建 | 最终 Debug / Release 均通过，零 warning / error |
| 最新 Rust fmt / clippy / 单测 / Release | 通过；17 项库测试 + 7 项能力测试，零跳过 |
| C# 图片 / 原生日志 / 附件 / 表情定向测试 | 133 项通过，零跳过 |
| 扩展图片与附件测试 | 54 项通过，包含中文路径、批量写入失败恢复、四种样式及 1000 次连续调用 |
| API `--check-native-images` | 本机 Debug 与 Release publish 目录均通过；无业务监听与数据库连接 |
| PostgreSQL 全量首轮 | 4002 项中 4001 通过；1 项既有 HTTP/3 fixture 清理超时，零跳过 |
| HTTP/3 失败组独立复测 | 12 项全部通过；保留首轮失败事实，不将首轮记成全绿 |
| 最终工作区全量后端回归 | 4008 项通过，零失败 / 跳过；一次性 PostgreSQL 容器已停止并删除 |
| 最新 Rust 锁文件 OSV 审计 | 84 包，无漏洞通告；2 条停止维护提示，见下节 |
| Linux arm64 native Docker 构建 | 早期候选原生阶段编译通过；不替代最终 API 容器运行证据 |
| Linux amd64 / arm64 API 镜像 | 本机 MCR TLS 超时；已在 GitHub runner 完成真实双架构构建、自检与 Trivy 漏洞策略检查 |

能力测试覆盖实际格式 / 尺寸、16-bit PNG、EXIF 八方向、JPEG 隐私清除不重编码、APNG / GIF / WebP 帧时序与透明合成、ICC、中文字体 shaping、取消、畸形输入、句柄错误边界和批次失败清理。发现并修复动画 WebP 解码背景导致透明残留的问题。旧 watermark FFI 测试改为真实新 ABI，不跳过必需原生项。

固定 256×192 输入、20 次预热、单并发 1000 次 PNG 缩放 / JPEG / WebP 压缩 / 中文水印混合调用通过：p50 6.01 ms、p95 8.41 ms、最大 15.82 ms；每 100 次采样 RSS，从 140.0 MB 增至 146.0 MB 后保持稳定，最终四个输出共 11,448 字节、无暂存子目录。macOS 的 PeakWorkingSet64 返回 0，记录为 unavailable，不冒充真实峰值；采样最大 RSS 146,046,976 字节。该样本不代表大图或高并发性能。

## 依赖与分发核对

- 实际 vendored C：libwebp `1.6.0`；lcms2-sys README 标明 Little CMS `2.19.1`，不能用其 `2190` 宏遗漏补丁号。
- 构建为实际目标解析依赖，收集 crate LICENSE / COPYING、libwebp PATENTS、DejaVu 与 Noto 字体正文及 Radish 根 LICENSE；清单写入 `dependencies.json`。这不改变 Radish 根 Source-Available 许可。
- [RUSTSEC-2026-0206](https://osv.dev/vulnerability/RUSTSEC-2026-0206)：rustybuzz 0.20.1 停止维护提示。
- [RUSTSEC-2026-0192](https://osv.dev/vulnerability/RUSTSEC-2026-0192)：ttf-parser 0.25.1 停止维护提示。
- 上述两条为 informational / unmaintained，无漏洞严重级别；保持显式风险记录。切换 harfrust / skrifa 涉及已冻结字体依赖范围，需后续单独评估与授权。
- imageproc 移除后，其传递 paste 停止维护提示也随依赖图退出。首轮 947 条许可盘点作为迁移前快照保留，不将旧数量冒充当前图。

## 当前集成状态

HTTP/3 测试夹具修正后的完整 Repo Quality 已通过；R4 / R5 合并前验收关闭。后续 [PR #75](https://github.com/laugh0608/Radish/pull/75) 于 2026-10-21 合并为 `4a8589b6`，dev 已完成回灌，随后开始 Mapperly 批次。合并结果与 PR 的 dev 侧父提交 `726d2a84` 树一致；此次集成没有新增实现差异，也没有创建 tag、发布或部署。以下保留各轮预检及失败修正事实。

## 首轮远端预检与修正

候选 `74a8f8f9` 已推送 dev，并手动运行 [Repo Quality](https://github.com/laugh0608/Radish/actions/runs/39444567644)。Windows x64、macOS arm64 / x64、Linux x64 原生作业与联合 AddressSanitizer 作业通过；包括独立子进程 768 个固定变异样本。停止维护通告保持记录。

首轮 arm64 lint 发现 c_char 在 Linux arm64 为 unsigned，使旧 SHA256 指针强转触发 unnecessary_cast；改用显式指针 cast 方法。两个 Docker 作业在 .NET publish 阶段发现 node 缺失，已将同一已授权 Node 镜像二进制复制到 SDK 构建阶段。验证入口另显式指定 cargo +1.96.0，避免仓库根目录执行时使用 runner 默认工具链。修正后第二轮五宿主平台与双架构容器检查均通过。

隔离浏览器服务已启动；初次开发 Authority 路径出现 401，测试环境使用已有 RADISH_PUBLIC_URL / 本地开发签名证书配置恢复验签，不修改认证实现。随后完成下述完整 PC/mobile 矩阵；测试配置仅用于一次性环境。

## 双架构漏洞门禁与后端夹具修正

候选 `d9922284` 的[第三轮预检](https://github.com/laugh0608/Radish/actions/runs/39449930294)中，五宿主原生平台、两个 Linux 最终 API 镜像构建 / 自检 / Trivy 策略、联合 AddressSanitizer、前端 Lint、Repo Hygiene、Baseline Quick、依赖安全和 Identity Guard 全部通过。Backend Guard 仍为 4007 通过、1 失败、零跳过，失败为 `InvalidControlStream_ShouldCloseConnectionWithProtocolCode(false, "Production")` 的关闭等待超时，不能把该次候选写成全绿。

检查内存 transport 发现，原 `DisposeAsync` 在生命周期锁内启动 async 方法，方法可能同步进入 Kestrel 关闭回调；回调注册又以 Dispose 完成为边界，存在锁内回调及关闭中追加列表的问题。修正为先发布唯一 disposal task、锁外释放和调用回调、关闭时冻结回调列表；异常同步传给等待方，避免真实释放错误被十秒超时掩盖。增加并发与迟到注册测试。关闭回调修正后的 13 项 HTTP/3 定向测试连续 20 轮通过；补齐连接级任务发布与异常传播后再次严格构建、13 项通过。没有延长超时、跳过测试或更改生产协议处理。

候选 `bca8fd00` 的[第四轮完整预检](https://github.com/laugh0608/Radish/actions/runs/39454063161)全部成功，包含 Backend Guard（4009 通过、零失败 / 跳过，7 分 52 秒）、五宿主平台、双架构镜像和聚合 Candidate Quality。原生测试使用生产 Rust 1.96.0；仅内存检查使用获批 nightly-2026-10-13 和 runner clang，同时检测 Rust 与 vendored C。768 个固定变异样本各自在独立进程执行，这是确定性变异测试，不宣称覆盖引导式 fuzz 的覆盖率。

## 照片质量与压力边界

以 NASA [Earth / PIA18033](https://science.nasa.gov/photojournal/earth/) 的 1536×1536 图片为公开照片样本，输入 SHA-256 为 `641045d61f05eb114ec38316c5e1e5261de9e333ddfbee3aba32d3dcd7c76826`。经当前 Release ABI 解码到相同 sRGB 参考，再以 quality 85 编码、解码比较：JPEG SSIM 0.99735（477,412 字节），WebP SSIM 0.99197（367,672 字节），均超过 0.98 初始目标。

计算方法为 8×8 不重叠亮度窗口均值、总体方差 / 协方差，K1=0.01、K2=0.03、L=255；不是高斯加权 SSIM，也不代表完整照片语料。透明边缘另由透明 PNG / 动画自动化与页面视觉核对覆盖。本机报告为 `/private/tmp/radish-image-photo-quality/report.json`，复现脚本为 `/private/tmp/radish-image-photo-quality.py`。压力结果见上文 1000 次混合调用；未以小图结果推导高并发吞吐。

## 隔离浏览器与存储验收

使用本轮获批的一次性 SQLite / 上传目录和临时管理员，通过 Gateway `https://localhost:5000` 访问 Web / Console。PC 为 1920×1080；移动操作覆盖 390×844 布局，并以独立真实 `deviceScaleFactor=3` context 补验三个结果页，截图实际为 1170×2532。早期 CLI 截图仅 390×844，未将其当作 DPR 3 证据。

| 路径 | 操作与结果 |
| --- | --- |
| PC / mobile 个人信息 | PNG / JPEG 头像上传、保存与重新显示成功 |
| PC / mobile 论坛 | 透明 PNG 与中文文件名上传、发帖及详情展示成功；透明半区正常 |
| 中文水印 | mobile 上传“萝卜图片验收”水印，原图及缩略图生成成功，详情可见中文 |
| 图片水印 | 同一隔离 API 改用图片水印测试配置，PC 上传与预览成功，输出右下角具有宽度 25% 的水印 |
| PC / mobile 表情管理 | 两个独立 GIF 样本上传及创建成功；原图 64×64、两帧 / 300 ms，实际静态缩略图为 PNG 150×150 |
| 失败反馈 | 文本伪装 `invalid.png` 被拒绝，页面显示“不支持该文件类型”，没有新增附件记录 |
| 存储核对 | 文件大小与 Attachment.FileSize 一致；图片 / 缩略图格式可解码；输入测试 tEXt 隐私内容未残留 |
| 宿主前置与健康 | 隔离库 `DbMigrate doctor` / `verify` 和 `npm run check:host-runtime` 全部通过 |

截图保留在本机忽略目录 `output/playwright/rust-images-*`；存储核对报告为 `/private/tmp/radish-browser-storage-report.json`。初始隔离种子的默认头像没有对应业务文件，曾出现该资源 404；上传测试头像后恢复，不伪造既有业务文件。

验收后已关闭本轮浏览器、停止 API / Auth / Gateway / Client / Console，并确认 5000、5001、5100、5200、3000、3100 不再监听。已删除本轮临时目录及其中 SQLite、测试账号、上传文件、密钥和分块目录；PostgreSQL 与 Docker 测试容器已清理，构建缓存保留。清理报告为 `/private/tmp/radish-image-browser-cleanup.json`。未访问生产或迁移历史附件。

全仓原始 `check:repo-hygiene` 仍报告 91 个既有文本问题及篇幅建议，不记为零问题；CI 使用的 candidate 预算扫描与本轮 changed-only 检查单独验证，未通过无关全仓格式化扩大范围。
