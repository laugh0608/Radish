# Rust 图片替代 R0 依赖与执行清单

> 2026-10-20：项目所有者已确认继续 .NET，先替代图片库并恢复正常 PR，其他依赖专题后置。本清单列出进入 R0 的精确输入，本任务已按精确清单获得安装与验证授权。
>
> 对应[实施方案](./rust-image-processing-implementation-plan.md)。以下依赖已进入锁文件并完成本机编译；平台交付仍按实施方案逐项验收。

## 1. 精确输入

| 依赖 | 拟定版本 | 用途 / feature |
| --- | --- | --- |
| Rust toolchain | `1.96.0` | 与本机已有版本一致，固定 toolchain；本机不安装新 Rust 版本 |
| image | `=0.25.10` | 现有解析版本；关闭默认 feature，保留 jpeg / png / gif / webp / bmp / ico |
| imageproc | 已移除 | 新处理链不再使用；按本清单允许的退出条件删除旧水印实现及无用依赖 |
| ab_glyph | `=0.2.32` | 复用字形 raster；变量字体样式能力先验证 |
| png / gif | `=0.18.1` / `=0.14.2` | 当前已缓存版本，直接控制 APNG / GIF 与容器元数据 |
| libwebp-sys | `=0.14.4` | 新增，有损 / 动画 WebP；使用随包 C 源码，不启用 system-dylib / generate-bindings / CPU 专用指令 |
| lcms2 / lcms2-sys | `=6.2.0` / `=4.0.7` | 新增，ICC 转换；指定 static，验证实际使用随包 Little CMS 源码 |
| rustybuzz | `=0.20.1` | 新增，字体 shaping；保留 std，不启用 wasm-shaper |
| unicode-bidi | `=0.3.18` | 新增，双向文本分段 |
| unicode-segmentation | `=1.13.3` | 缓存中已有，按 grapheme 分段 |
| ico | `=0.6.0` | 新增，多 entry ICO 容器 |
| serde / serde_json | `=1.0.229` / `=1.0.151` | 缓存中已有；serde derive，受限 ABI 控制消息 |

现有 sha2 / rayon / anyhow 仅按当前锁文件解析结果保留，不做无关升级。提交 Cargo.lock，锁定本清单依赖及解析出的传递依赖；锁文件不是只冻结直接版本。

实际内嵌版本为 libwebp `1.6.0` 与 Little CMS `2.19.1`；构建按锁文件收集第三方 LICENSE、libwebp PATENTS 和字体声明，生成 `dependencies.json` 与校验 manifest。`rustybuzz 0.20.1`、`ttf-parser 0.25.1` 已有停止维护提示；属于后续维护风险，不伪装成 CVE 或忽略已知漏洞。安全审计结果与平台验证边界见[实施记录](../records/rust-image-replacement-2026-10-20.md)。

## 2. 字体

- 复用现有 `Lib/radish.lib/fonts/DejaVuSans.ttf`。
- 中文复用仓库已有 `Clients/radish.flutter/assets/fonts/noto/NotoSansSC-VF.ttf`，SHA256：`d68bafcb48a2707749396aa12bbbd833cb70401f3a9a689fd2902c7e0d295964`；随包保留已有 OFL 正文。
- 构建时从仓库资产复制到后端产物，不从操作系统字体目录抓取，不额外下载字体，不修改 Flutter 代码或原字体资产。
- 该字体为变量 TTF，R0 必须验证 shaping 与 raster 的 weight 一致性；不支持的样式不得无声变成 Regular。若需换用静态字体或增加新的字形 raster 依赖，另列精确范围确认。

## 3. 命令与影响

获批后在 `Lib/radish.lib` 更新 Cargo.toml 与忽略规则，并执行：

```sh
cargo generate-lockfile --manifest-path Lib/radish.lib/Cargo.toml
cargo fetch --locked --manifest-path Lib/radish.lib/Cargo.toml
```

随后使用 `cargo test / build / clippy --locked` 做 R0 验证；必要时因沙盒网络 / Cargo 缓存权限申请宿主机执行。构建会运行所选 crate 的 build script、编译 vendored C 库，并写入 Cargo 缓存及项目 target / native artifacts；不修改系统库、不开放监听端口、不启动业务服务。

通过能力验证后，移除两个直接 Six Labors PackageReference 并清除其代码使用，再执行：

```sh
dotnet restore Radish.slnx --disable-parallel
```

该还原只为退出 ImageSharp / Drawing 及其 Fonts / PolygonClipper 传递链，保留此前 XML / Pkcs 安全修复，不升级其他 NuGet / npm 依赖。不执行 npm install / ci。

Cargo cache 保留供后续可复现构建；测试临时目录由用例清理，target / artifacts 保持忽略。服务 / PostgreSQL / Docker 运行、下载额外平台工具链及镜像构建环境在具备具体命令和用途后分别说明，不由本清单提前授权。
