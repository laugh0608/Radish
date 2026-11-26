# Noto 字体供应链记录

Radish Flutter Native 安装包继续携带 Noto 官方简体中文区域子集变量 TTF，并在离线运行时直接使用包内字体。字形覆盖沿用上游区域子集。

| 文件 | 上游版本 | 字节数 | 上游路径 | SHA-256 |
| --- | --- | ---: | --- | --- |
| `NotoSansSC-VF.ttf` | `Sans2.004` | 17,773,132 | `Sans/Variable/TTF/Subset/NotoSansSC-VF.ttf` | `d68bafcb48a2707749396aa12bbbd833cb70401f3a9a689fd2902c7e0d295964` |
| `NotoSerifSC-VF.ttf` | `Serif2.003` · `9b0f1436e455d902de067a2501422e5dc71ad16b` | 25,125,232 | `Serif/Variable/TTF/Subset/NotoSerifSC-VF.ttf` | `5326cfb097e3ab26fcb39329752b5c0a439bf8d5c4649520e4b492939c352a09` |

- 上游仓库：<https://github.com/notofonts/noto-cjk>
- 首次核对日期：2026-08-23（Asia/Shanghai）。
- 字体资产总计：42,898,364 字节。
- 许可证：SIL Open Font License 1.1；Sans 与 Serif 的原始文本分别取自上游仓库 `main` 分支的 `Sans/LICENSE` 与 `Serif/LICENSE`，保存在 `licenses/OFL-NotoSansCJK.txt` 与 `licenses/OFL-NotoSerifCJK.txt`。
- 两份许可证文件的 SHA-256 均为 `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`。

## 构建前准备

`NotoSansSC-VF.ttf` 随仓库提供，Flutter 和后端水印共用。Serif 在首次构建或缓存失效时由 Node.js 24+ 脚本准备，校验完成后写入忽略提交的 `generated/`。构建后字体仍和两份 OFL 一起进入安装包。

在 `Clients/radish.flutter` 运行以下命令。有效缓存可直接离线复用。

```bash
node tool/prepare-serif-font.mjs
```

已有字体原件时，用 `node tool/prepare-serif-font.mjs --from <TTF 路径>` 校验并导入。只校验现有缓存时用 `node tool/prepare-serif-font.mjs --verify`。

需要经代理访问上游时，Node.js 24.14+ 会读取当前进程的 `HTTPS_PROXY` 与 `NO_PROXY` 设置。[Node.js 代理说明](https://nodejs.org/docs/latest-v24.x/api/http.html#httpsetglobalproxyfromenvproxyenv)
