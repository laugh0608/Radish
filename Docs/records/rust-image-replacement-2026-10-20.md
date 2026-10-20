# Rust 图片处理替代实施与验证记录

> 日期：2026-10-20。状态：开发实现与本机验证推进中，尚未创建 PR，R4 / R5 未全部关闭。方案见[完整替代实施方案](../features/rust-image-processing-implementation-plan.md)，精确授权见[依赖清单](../features/rust-image-processing-dependency-proposal.md)。

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
| 最终 Linux amd64 / arm64 API 镜像 | 本机拉取 MCR .NET 基础镜像 TLS handshake timeout，尚未执行到项目构建；待 runner 补验 |

能力测试覆盖实际格式 / 尺寸、16-bit PNG、EXIF 八方向、JPEG 隐私清除不重编码、APNG / GIF / WebP 帧时序与透明合成、ICC、中文字体 shaping、取消、畸形输入、句柄错误边界和批次失败清理。发现并修复动画 WebP 解码背景导致透明残留的问题。旧 watermark FFI 测试改为真实新 ABI，不跳过必需原生项。

固定 256×192 输入、20 次预热、单并发 1000 次 PNG 缩放 / JPEG / WebP 压缩 / 中文水印混合调用通过：p50 6.01 ms、p95 8.41 ms、最大 15.82 ms；每 100 次采样 RSS，从 140.0 MB 增至 146.0 MB 后保持稳定，最终四个输出共 11,448 字节、无暂存子目录。macOS 的 PeakWorkingSet64 返回 0，记录为 unavailable，不冒充真实峰值；采样最大 RSS 146,046,976 字节。该样本不代表大图或高并发性能。

## 依赖与分发核对

- 实际 vendored C：libwebp `1.6.0`；lcms2-sys README 标明 Little CMS `2.19.1`，不能用其 `2190` 宏遗漏补丁号。
- 构建为实际目标解析依赖，收集 crate LICENSE / COPYING、libwebp PATENTS、DejaVu 与 Noto 字体正文及 Radish 根 LICENSE；清单写入 `dependencies.json`。这不改变 Radish 根 Source-Available 许可。
- [RUSTSEC-2026-0206](https://osv.dev/vulnerability/RUSTSEC-2026-0206)：rustybuzz 0.20.1 停止维护提示。
- [RUSTSEC-2026-0192](https://osv.dev/vulnerability/RUSTSEC-2026-0192)：ttf-parser 0.25.1 停止维护提示。
- 上述两条为 informational / unmaintained，无漏洞严重级别；保持显式风险记录。切换 harfrust / skrifa 涉及已冻结字体依赖范围，需后续单独评估与授权。
- imageproc 移除后，其传递 paste 停止维护提示也随依赖图退出。首轮 947 条许可盘点作为迁移前快照保留，不将旧数量冒充当前图。

## 待关闭的验收

1. 最终候选完整 baseline、依赖安全、全量后端及 identity 检查。
2. 五宿主平台 CI、Linux 双架构最终镜像真实自检与发布产物核验。
3. 方案要求的独立 sanitizer / fuzz 与混合负载性能证据；已有 768 个独立子进程变异样本通过；本机 Apple Clang / Rust ASan 运行库不兼容导致链接失败。已获批仅 CI 使用 nightly-2026-10-13 + runner clang 的联合内存检查，等待实际结果。
4. 本任务已授权并启动隔离服务，正在执行 PC / mobile 的头像、帖子图片、表情上传 Smoke。
5. 所有必需项通过后提交最终变更、创建 `dev → master` PR 并确认 CI。不得以本页文档或本机成功代替未完成门禁。

本轮 PostgreSQL 容器、Docker 自检与清理均按任务中的精确授权执行，不访问生产。Docker 构建缓存保留，测试容器与镜像清理；最终状态在后续验证结束时补齐。

## 首轮远端预检与修正

候选 `74a8f8f9` 已推送 dev，并手动运行 [Repo Quality](https://github.com/laugh0608/Radish/actions/runs/39444567644)。Windows x64、macOS arm64 / x64、Linux x64 原生作业与联合 AddressSanitizer 作业通过；包括独立子进程 768 个固定变异样本。停止维护通告保持记录。

首轮 arm64 lint 发现 c_char 在 Linux arm64 为 unsigned，使旧 SHA256 指针强转触发 unnecessary_cast；改用显式指针 cast 方法。两个 Docker 作业在 .NET publish 阶段发现 node 缺失，已将同一已授权 Node 镜像二进制复制到 SDK 构建阶段。验证入口另显式指定 cargo +1.96.0，避免仓库根目录执行时使用 runner 默认工具链。修正后需再次远端预检。

隔离浏览器服务已启动；初次开发 Authority 路径出现 401，测试环境使用已有 RADISH_PUBLIC_URL / 本地开发签名证书配置恢复验签，不修改认证实现。论坛透明 PNG 上传已返回 200；完整 PC/mobile 矩阵仍进行中。
