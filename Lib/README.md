# Lib

该目录用于存放 Radish 的原生扩展库。

## 子项目

- `radish.lib`：Rust 原生扩展库（`radish_lib`）。

## 常用命令

```bash
cd Lib/radish.lib
cargo build --release
```

## 说明

- 构建产物位于 `Lib/radish.lib/target/`。
- .NET 项目构建时会按配置自动尝试拷贝原生库到输出目录（仅复制已存在产物，不会自动触发 cargo 编译）。
