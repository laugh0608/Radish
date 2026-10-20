# Radish Rust 原生扩展

本库提供后端完整图片处理 ABI 和既有 SHA256 接口。当前实现、构建与配置以 [Rust 扩展指南](../../Docs/guide/rust-extensions.md)为准，接口与验收边界见[实施方案](../../Docs/features/rust-image-processing-implementation-plan.md)。

- `src/imaging/`：内容识别、受限解码、方向 / 色彩转换、缩放、编码、水印和隐私元数据处理。
- `src/imaging/ffi.rs`：版本化 JSON 控制 ABI、错误边界、取消与返回值所有权。
- `src/hash/`：保留既有 SHA256 功能；上传主路径仍使用 .NET BCL。
- `tests/image_capabilities.rs`：codec / 字体 / ICC 能力测试。
- `Cargo.lock`、`rust-toolchain.toml`：精确依赖与工具链。

在仓库根目录执行：

```sh
node Scripts/validate-native-images.mjs
node Scripts/build-native-images.mjs --debug
node Scripts/build-native-images.mjs
```

需要目标平台 C 编译器；构建脚本打包动态库、字体、依赖及许可声明到 `artifacts/native/<rid>/<configuration>`。不再使用旧 `src/image/watermark.rs` 或 `add_text_watermark` ABI，不依赖系统字体或 C# 图片回退。构建矩阵与本轮已通过的平台必须区分，结果见[实施记录](../../Docs/records/rust-image-replacement-2026-10-20.md)。
