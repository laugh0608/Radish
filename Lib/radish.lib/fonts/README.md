# DejaVu Sans 字体

`DejaVuSans.ttf` 是后端文字水印的构建输入。`Scripts/build-native-images.mjs` 将它复制到原生产物的 `fonts/`，并将字体哈希写入 manifest。后端加载原生产物时会校验该哈希。

## 构建字体

- 文件：`DejaVuSans.ttf`
- 字节数：`759720`
- SHA-256：`ae7b7855e115a5966d8b1b3f80f254ccc117ec86f9965e202ee2940453837280`

需要恢复时，从[已验证的仓库快照](https://github.com/laugh0608/Radish/blob/520c8bbf81535d09470f3a950ef586b31502eece/Lib/radish.lib/fonts/DejaVuSans.ttf)取回原文件，并用 `shasum -a 256 DejaVuSans.ttf` 校验。官方 2.37 归档解出的同名字体哈希与本构建输入不同，系统字体也不能直接替换该文件。

## 上游归档与许可

完整的 `dejavu-fonts-ttf-2.37.tar.bz2` 可从[官方发行附件](https://github.com/dejavu-fonts/dejavu-fonts/releases/download/version_2_37/dejavu-fonts-ttf-2.37.tar.bz2)下载。它与[仓库历史副本](https://github.com/laugh0608/Radish/blob/520c8bbf81535d09470f3a950ef586b31502eece/Lib/radish.lib/fonts/dejavu-fonts-ttf-2.37.tar.bz2)均为 `5429777` 字节，SHA-256 为 `fa9ca4d13871dd122f61258a80d01751d603b4d3ee14095d65453b4e846e17d7`。

[DejaVu 许可与版权声明](./LICENSE-DejaVu.txt)随原生产物分发，涵盖 Bitstream Vera 条款、DejaVu 公有领域改动和适用的 Arev 声明。[上游许可说明](https://dejavu-fonts.github.io/License.html) · [Rust 图片替代评估](../../../Docs/features/rust-image-processing-replacement-assessment.md)
