# Noto 字体供应链记录

本目录固定随 Radish Flutter Native 安装包交付 Noto 官方简体中文区域子集变量 TTF，不在运行时下载字体，也不根据当前 UI 字符串二次裁字。

| 文件 | 上游版本 | 字节数 | 上游路径 | SHA-256 |
| --- | --- | ---: | --- | --- |
| `NotoSansSC-VF.ttf` | `Sans2.004` | 17,773,132 | `Sans/Variable/TTF/Subset/NotoSansSC-VF.ttf` | `d68bafcb48a2707749396aa12bbbd833cb70401f3a9a689fd2902c7e0d295964` |
| `NotoSerifSC-VF.ttf` | `Serif2.003` | 25,125,232 | `Serif/Variable/TTF/Subset/NotoSerifSC-VF.ttf` | `5326cfb097e3ab26fcb39329752b5c0a439bf8d5c4649520e4b492939c352a09` |

- 上游仓库：<https://github.com/notofonts/noto-cjk>
- 下载日期：2026-08-23（Asia/Shanghai）
- 字体资产总计：42,898,364 字节。
- 许可证：SIL Open Font License 1.1；Sans 与 Serif 的原始文本分别取自上游仓库 `main` 分支的 `Sans/LICENSE` 与 `Serif/LICENSE`，保存在 `licenses/OFL-NotoSansCJK.txt` 与 `licenses/OFL-NotoSerifCJK.txt`。
- 两份许可证文件的 SHA-256 均为 `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`。
- 完整性：下载后必须以 `shasum -a 256` 校验上表哈希；不一致时不得构建或提交。
