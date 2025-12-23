# Rust 扩展架构使用指南

## 概述

Radish 项目支持使用 Rust 原生扩展来提升图片处理和文件操作的性能。系统提供了 C# (ImageSharp) 和 Rust 两种实现，可以通过配置灵活切换。

## 功能特性

### 当前支持的 Rust 扩展

1. **图片水印** (`add_text_watermark`)
   - 添加文字水印到图片
   - 支持 5 种位置（左上、右上、左下、右下、居中）
   - 可自定义字体大小、透明度

2. **文件哈希计算** (`calculate_file_sha256`)
   - 快速计算文件 SHA256 哈希值
   - 用于文件去重功能

3. **性能测试函数** (从 test_lib 迁移)
   - `calculate_sum_rust`: 整数求和
   - `calculate_fibonacci_like_rust`: 斐波那契计算
   - `count_primes_sieve_rust`: 质数筛法
   - `count_primes_parallel_rust`: 并行质数计算

### 性能优势

根据性能测试，Rust 实现在以下场景中表现更优：
- 大图片处理（> 2MB）
- 批量操作
- CPU 密集型任务（哈希计算）

## 快速开始

### 1. 安装 Rust 工具链

```bash
# Linux / macOS
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# Windows
# 访问 https://rustup.rs/ 下载安装程序

# 验证安装
rustc --version
cargo --version
```

### 2. 编译 Rust 库

```bash
# Linux / macOS
cd Radish.Core/radish-lib
chmod +x build.sh
./build.sh

# Windows PowerShell
cd Radish.Core\radish-lib
.\build.ps1
```

编译成功后，库文件会自动复制到 `Radish.Api/bin/Debug/net10.0/` 目录：
- Linux: `libradish_lib.so`
- macOS: `libradish_lib.dylib`
- Windows: `radish_lib.dll`

### 3. 启用 Rust 扩展

在 `appsettings.json` 或 `appsettings.Local.json` 中配置：

```json
{
  "FileStorage": {
    "ImageProcessing": {
      "UseRustExtension": true  // 设置为 true 启用 Rust 扩展
    }
  }
}
```

### 4. 重启应用

```bash
dotnet run --project Radish.Api
```

启动日志会显示使用的处理器类型：
```
[INF] Image processor initialized: Rust
```

## 配置说明

### 完整配置示例

```json
{
  "FileStorage": {
    "ImageProcessing": {
      "UseRustExtension": false,      // true: 使用 Rust, false: 使用 C# (ImageSharp)
      "GenerateThumbnail": true,
      "ThumbnailSize": {
        "Width": 150,
        "Height": 150
      },
      "CompressQuality": 85,
      "RemoveExif": true
    },
    "Watermark": {
      "Enable": true,
      "Type": "Text",
      "Text": {
        "Content": "Radish",
        "Position": "BottomRight",
        "FontSize": 24,
        "Opacity": 0.5
      }
    }
  }
}
```

### 配置项说明

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `UseRustExtension` | bool | false | 是否使用 Rust 扩展 |

**注意**：
- 如果 `UseRustExtension=true` 但 Rust 库不可用，系统会自动回退到 C# 实现
- 日志会记录回退信息，方便排查问题

## 架构设计

### 组件结构

```
Radish.Core/radish-lib/                 # 统一的 Rust 扩展库
├── src/
│   ├── lib.rs                          # FFI 导出
│   ├── image/                          # 图片处理模块
│   │   ├── mod.rs
│   │   └── watermark.rs                # 水印功能
│   ├── hash/                           # 文件哈希模块
│   │   ├── mod.rs
│   │   └── file_hash.rs                # SHA256 哈希计算
│   ├── benchmark/                      # 性能测试模块（从 test_lib 迁移）
│   │   ├── mod.rs
│   │   └── math.rs                     # 数学计算测试
│   └── utils/                          # 工具函数
│       └── mod.rs
├── fonts/DejaVuSans.ttf                # 字体文件
├── build.sh / build.ps1                # 编译脚本
├── Cargo.toml                          # Rust 项目配置
└── README.md

Radish.Infrastructure/ImageProcessing/  # C# 封装
├── IImageProcessor.cs                  # 接口定义
├── CSharpImageProcessor.cs             # C# 实现
├── RustImageProcessor.cs               # Rust FFI 封装
└── ImageProcessorFactory.cs            # 工厂类（配置切换）
```

### 工作流程

1. **启动时**：`ImageProcessorFactory` 根据配置创建处理器实例
2. **运行时**：`AttachmentService` 通过 `IImageProcessor` 接口调用
3. **Rust 调用**：`RustImageProcessor` 通过 P/Invoke 调用 Rust 函数
4. **自动回退**：如果 Rust 库不可用或出错，自动使用 C# 实现

## 性能测试

### 运行性能测试

```bash
# 运行所有性能测试
dotnet test Radish.Api.Tests --filter "FullyQualifiedName~ImageProcessorPerformanceTest"

# 运行特定测试
dotnet test --filter "FullyQualifiedName~WatermarkPerformance_CSharpVsRust"
```

### 测试项目

1. **单张图片水印** (`WatermarkPerformance_CSharpVsRust`)
   - 测试 2000x1500 图片的水印处理时间
   - 对比 C# 和 Rust 的性能差异

2. **文件哈希计算** (`FileHashPerformance_CSharpVsRust`)
   - 测试 10MB 文件的 SHA256 哈希计算时间
   - 验证哈希值一致性

3. **批量处理** (`BatchWatermarkPerformance_CSharpVsRust`)
   - 测试 10 张图片的批量水印处理
   - 计算平均每张图片的处理时间

### 预期性能提升

根据初步测试（具体数据需要实际运行测试获得）：
- 图片水印：Rust 预计比 C# 快 1.5-3x
- 文件哈希：Rust 预计比 C# 快 2-4x
- 批量操作：性能优势更明显

## 故障排查

### 问题 1：Rust 库未找到

**症状**：
```
[WRN] Rust library not found, falling back to C# implementation
```

**解决方案**：
1. 确认已编译 Rust 库：`cd Radish.Core/radish-lib && ./build.sh`
2. 检查库文件是否存在于 `Radish.Api/bin/Debug/net10.0/` 目录
3. 确认文件名正确：
   - Linux: `libradish_lib.so`
   - macOS: `libradish_lib.dylib`
   - Windows: `radish_lib.dll`

### 问题 2：字体文件缺失

**症状**：
```
[ERR] Rust watermark error: Failed to load font
```

**解决方案**：
1. 确认 `Radish.Core/radish-lib/fonts/DejaVuSans.ttf` 存在
2. 参考 `fonts/README.md` 获取字体文件
3. Linux 系统可以从 `/usr/share/fonts/truetype/dejavu/` 复制

### 问题 3：编译失败

**症状**：
```
error: could not compile `radish-lib`
```

**解决方案**：
1. 确认 Rust 工具链已安装：`rustc --version`
2. 更新 Rust：`rustup update`
3. 清理并重新编译：
   ```bash
   cargo clean
   cargo build --release
   ```

### 问题 4：运行时错误

**症状**：
```
[ERR] Rust watermark failed with code -1
```

**解决方案**：
1. 检查日志中的详细错误信息
2. 确认输入文件路径正确且文件存在
3. 确认输出目录有写入权限
4. 系统会自动回退到 C# 实现，不影响功能

## 开发指南

### 添加新的 Rust 函数

1. **在 Rust 中实现**：
   ```rust
   // src/lib.rs
   #[no_mangle]
   pub extern "C" fn my_new_function(param: i32) -> i32 {
       // 实现逻辑
       param * 2
   }
   ```

2. **在 C# 中声明**：
   ```csharp
   // RustImageProcessor.cs
   [DllImport(LibraryName, EntryPoint = "my_new_function")]
   private static extern int MyNewFunctionNative(int param);
   ```

3. **添加公共方法**：
   ```csharp
   public int MyNewFunction(int param)
   {
       try
       {
           return MyNewFunctionNative(param);
       }
       catch (DllNotFoundException)
       {
           // 回退到 C# 实现
           return param * 2;
       }
   }
   ```

4. **重新编译 Rust 库**：
   ```bash
   cd Radish.Core/radish-lib
   ./build.sh
   ```

### 性能优化建议

1. **选择合适的实现**：
   - 小文件（< 1MB）：C# 和 Rust 性能差异不大
   - 大文件（> 2MB）：Rust 优势明显
   - 批量操作：优先使用 Rust

2. **配置策略**：
   - 开发环境：使用 C# 实现（无需编译 Rust）
   - 生产环境：使用 Rust 实现（更高性能）

3. **监控和调优**：
   - 定期运行性能测试
   - 根据实际负载调整配置
   - 关注日志中的性能指标

## 最佳实践

1. **渐进式启用**：
   - 先在开发环境测试 Rust 扩展
   - 确认功能正常后再在生产环境启用
   - 保持 C# 实现作为后备方案

2. **版本管理**：
   - Rust 库版本与项目版本保持一致
   - 更新 Rust 代码后及时重新编译
   - 在 CI/CD 中自动化编译流程

3. **错误处理**：
   - 始终提供 C# 实现作为回退
   - 记录详细的错误日志
   - 监控 Rust 扩展的可用性

4. **性能监控**：
   - 定期运行性能测试
   - 对比 C# 和 Rust 的实际性能
   - 根据业务需求选择合适的实现

## 参考资料

- [Rust 官方文档](https://www.rust-lang.org/learn)
- [ImageSharp 文档](https://docs.sixlabors.com/articles/imagesharp/index.html)
- [P/Invoke 指南](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
- [Radish 文件上传设计文档](./file-upload-design.md)

## 更新日志

### 2025-12-23
- ✅ 实现统一的 Rust 扩展架构（radish-lib）
- ✅ 添加图片水印功能（Rust 实现）
- ✅ 添加文件哈希计算（Rust 实现）
- ✅ 迁移 test_lib 性能测试功能到 radish-lib
- ✅ 实现 ImageProcessorFactory（配置切换）
- ✅ 添加自动回退机制
- ✅ 完善编译脚本（build.sh / build.ps1）
- ✅ 更新所有 C# 代码引用（test_lib → radish_lib）
- ✅ 完善文档和使用指南

---

**维护者**：Radish Team
**最后更新**：2025-12-23
